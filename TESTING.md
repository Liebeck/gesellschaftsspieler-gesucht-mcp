# Testing Guide — Write Confirmation, Enforcement, SDK 2.0 Spike

Public repo: this guide uses configuration **key names and placeholders only** — no connection
strings, schema, or credentials.

## 0. What is already verified (automated, in this repo)

- `dotnet build Gesellschaftsspieler.MCPServer.csproj` → 0 errors.
- `dotnet test Tests/Gesellschaftsspieler.MCPServer.Tests.csproj` → **31 passing** unit tests:
  caller-level resolution, profile selection, usage/quota store (atomic, day rollover, prune),
  the enforcement DoD behaviors (rate limit, quota, blocklist, admin role gate), the tool-level
  write-confirmation gate (both paths), and the unconfirmed-preview-does-not-consume-quota rule.

Everything below needs a **running server + a real MCP client** and is for the human tester.

## 1. Prerequisites — the authentication stack (READ FIRST)

Which tools need what:
- **Anonymous read tools** (`search`, `fetch`, `list_games`, `get_top_games`, …) work against the
  live data with **no token** — test these immediately.
- **Personal + write tools** (`whoami`, `get_my_play_stats`, `add_game_to_collection`, `rate_game`)
  require a **valid OAuth token**. The auth check runs *before* the confirm logic, so an
  unauthenticated call returns `{"authenticated": false, ...}` regardless of the `confirm` flag.

A valid token exists ONLY when the **web app with the OAuth changes is running and reachable** —
the Authorization Server (`/connect/authorize`, `/connect/token`, `/connect/register`) AND the
`/api/mcp/*` endpoints (a different repo's work). **The live gs-gesucht environment does not have
these yet**, so authenticated MCP tools cannot be exercised end-to-end until one of:

- **Option A — full local stack:** run the web app locally with the OpenIddict changes (EF
  migration applied to a test DB; dev signing/encryption certs are used automatically in a Debug
  build), then point the MCP server at it via `Oidc:Authority`, `Oidc:Audience`,
  `GsGesucht:ApiBaseUrl`.
- **Option B — deploy:** deploy the web-app branch (OAuth + `/api/mcp`) to a test/staging
  environment and point the MCP config there.

Audience alignment (must match or token validation fails): the token's `aud` = the MCP server URL
the client connects to; that same URL must be in the web app's `OpenIddict:AllowedResources` and in
the MCP server's `Oidc:Audience`. For a local stack that's your `http://localhost:<mcp-port>`.

### 1a. RECOMMENDED: everything live + your LOCAL Inspector (no whitelisting needed)

When both the web app (AS + `/api/mcp`) and the MCP server are deployed, your **local** MCP
Inspector can test the full flow with **no config whitelisting**:
- The `resource`/audience is the live MCP URL, already in `AllowedResources`.
- The only localhost element is the Inspector's **redirect URI**, already permitted by the DCR
  localhost exception.
- The live AS 302-redirects your browser back to `http://localhost:<port>` (standard native-app
  pattern; works from a remote AS).

Steps: set the live MCP `Mcp:RequireAuthentication = true`; connect the Inspector to
`https://<live-mcp>/mcp`; complete OAuth (register → log in → consent → token). Then run §3–§9.
Notes: for enforcement (§6) set `Mcp:Enforcement:Profile = Demo` on live for the test; writes hit
real data; `get_platform_stats` needs your account to have the Admin role.

### 1b. Local MCP + local Inspector against a DEPLOYED Authorization Server

You can point a local MCP at the live AS, but two URL gates apply:
- **Redirect URI (DCR):** the Inspector's `http://localhost:<port>/…/callback` is accepted — the
  DCR validator allows http localhost/127.0.0.1/::1 on any port.
- **Resource/audience whitelist (RFC 8707):** the AS only mints a token whose `aud` is in
  `OpenIddict:AllowedResources`, and the live `/api/mcp` only accepts those audiences. Your local
  MCP URL is not in that list by default, so the token is refused (`invalid_target`).

To enable it:
1. On the **live web app**, add your local MCP URL to `OpenIddict:AllowedResources`
   (e.g. `http://localhost:5074`). A restart re-seeds the `mcp` scope's resources — no code change.
   Prefer adding it temporarily, or run the web app locally so localhost stays out of live config.
2. On the **local MCP**: `Oidc:Authority` = live AS, `GsGesucht:ApiBaseUrl` = live web app,
   `Oidc:Audience` = the exact URL you whitelisted, `Mcp:RequireAuthentication = true`.
3. The MCP advertises that `Oidc:Audience` via `/.well-known/oauth-protected-resource`; the
   Inspector requests a token for it; the AS checks it against the whitelist; the forwarded token
   validates at both the local MCP and the live `/api/mcp`.

## 2. Run the MCP server and authenticate in the Inspector

MCP server config (env vars / user-secrets — never commit values):
- `KeyVaultUri` — DB connection + telemetry.
- `Oidc:Authority` / `Oidc:Audience` / `GsGesucht:ApiBaseUrl` — point at the web app from §1.

```bash
dotnet run --project Gesellschaftsspieler.MCPServer.csproj    # MCP endpoint: /mcp
npx @modelcontextprotocol/inspector                            # Streamable HTTP → http://localhost:<port>/mcp
```

**To get a token (required for personal/write tools):** set `Mcp:RequireAuthentication = true`.
Then `/mcp` returns `401` with the resource-metadata pointer, and the Inspector runs the OAuth flow
(dynamic registration → login with a real gs-gesucht account → consent → token). Once authenticated,
`whoami` shows your user and the personal/write tools work. With `RequireAuthentication = false`
(default), `/mcp` is anonymous, the Inspector is never prompted to log in, and personal tools return
`authenticated: false` — which is exactly the response you saw.

## 3. Phase 1 — tool-level confirmation (DEFAULT, must work everywhere)

Config: `Mcp:WriteConfirmation = ToolLevel` (default).

1. Call `add_game_to_collection` with `{ "game": "Wingspan" }` (no `confirm`).
   - **Expect:** nothing is written; result contains `confirmationRequired: true` and the message
     *"Confirmation required: add 'Wingspan' to your collection? Call this tool again with
     confirm=true to proceed."* (DoD #2)
2. Call it again with `{ "game": "Wingspan", "confirm": true }`.
   - **Expect:** the game is written (the web-app API is called with the forwarded token). (DoD #2)
3. Repeat both for `rate_game` (e.g. `{ "game": "Wingspan", "like": true }` then with `confirm: true`).
4. **Quota check (DoD #3):** switch `Mcp:Enforcement:Profile = Demo` (quota 20). Make several
   *unconfirmed* `add_game_to_collection` calls, then call `get_platform_stats` (admin token) —
   the unconfirmed previews must **not** have incremented the caller's daily count (they do count
   toward the per-minute rate limit).

## 4. Phase 0 — diagnose the elicitation confirmation (Protocol mode)

Config: `Mcp:WriteConfirmation = Protocol`. Watch the server console/log.

1. Call `add_game_to_collection` (no confirm arg — Protocol mode ignores it).
2. Read the log lines from category `Mcp.WriteConfirmation`:
   - `Elicitation: sending confirmation request` **appears** + `client responded (...)` → the
     server sent it; the client's answer tells you accept/decline/empty. **Client-side story.**
   - `sending...` appears + a **warning with an exception** → the client does not support
     elicitation. **Client-side story.**
   - `sending...` **never appears** → the server never reached the elicitation call
     (code/injection path). **Server-side story.**
3. Record, per client (Inspector, and if testable ChatGPT/Claude), which of the three you see.
   That is the slide-worthy diagnosis. (DoD #1)

## 5. Phase 2 — Criterion B: MRTR confirmation on SDK 2.0 (Inspector proof)

The server is now on **ModelContextProtocol 2.0** (stateless core; elicitation rides on Multi
Round-Trip Requests). Config: `Mcp:WriteConfirmation = Protocol`, current MCP Inspector.

1. Call `add_game_to_collection`.
   - **Expect:** the Inspector shows a confirmation prompt (the elicitation/MRTR round trip).
2. **Decline** → nothing is written.
3. Call again and **accept** → the game is written.
   - Both outcomes = Criterion B green. Note the exact Inspector behavior for the report.

## 6. Enforcement smoke (unchanged semantics under 2.0)

Config: `Mcp:Enforcement:Profile = Demo` (user 3/min, admin 20/min, quota 20).

- **Rate (User):** with a user token, call any tool 3× within a minute; the **4th** returns
  *"Rate limit exceeded (3 calls per minute)..."*. After 60s it works again. (DoD-enforcement)
- **Admin:** with an admin token, 20 calls in the same window all succeed.
- **Two users:** two different user tokens have independent limits.
- **Anonymous:** public read tools (e.g. `search`) share one limit; `initialize`/`tools/list`
  are never limited.
- **Quota:** exhaust the daily quota (20 in Demo) → *"Daily quota exceeded..."*.
- **Blocklist:** put a `sub` in `Mcp:Enforcement:BlockedSubjects` → that account is
  suspended immediately, even with a valid token.
- **Admin tool:** `get_platform_stats` is listed and callable with an admin token; with a user
  token it is neither listed nor callable (role error).

## 7. Backward-compatibility (2025 client line)

- Connect with an **older MCP Inspector** (pin a 2025-era version) or force the older protocol
  version. Confirm it can still `initialize`, `tools/list`, and call a read tool. Note whether
  the SDK handles this transparently (don't assume — verify). (Phase 2 DoD #8)

## 8. Human client test — the confirmation beat (ChatGPT / Claude)

Use the **default** `Mcp:WriteConfirmation = ToolLevel` (guaranteed everywhere):

1. Connect the MCP server to ChatGPT or Claude (as a remote MCP/connector), authenticate.
2. Prompt: *"Add Wingspan to my collection."*
   - **Expect:** the assistant calls `add_game_to_collection` once, gets the confirmation prompt,
     and **asks you to confirm** before doing anything.
3. Confirm ("yes").
   - **Expect:** the assistant calls again with `confirm=true` and reports success.
4. (Optional) Repeat with `Mcp:WriteConfirmation = Protocol` to compare the elicitation/MRTR
   experience per client.

## 9. Smoke checks (should be unchanged by the SDK upgrade)

- `GET /.well-known/oauth-protected-resource` returns the resource metadata (RFC 9728).
- An unauthenticated call to a protected tool (with `Mcp:RequireAuthentication = true`) returns
  `401` with a `WWW-Authenticate: Bearer resource_metadata="..."` header.
- `whoami` returns the caller's `preferred_username`/`sub`/`role` from the token.
- Token-forwarding: a personal read tool (e.g. `get_my_play_stats`) returns the caller's data.

## Notes

- Default `Mcp:WriteConfirmation` stays `ToolLevel`. Flip to `Protocol` only after §4/§7 confirm
  the MRTR path with your target clients.
- Profile switch (`Demo` ↔ `Production`) is config-only; on Azure App Service an app-setting
  change restarts the app and picks up the new profile.
