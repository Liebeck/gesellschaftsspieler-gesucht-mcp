using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>
/// Central enforcement logic invoked by the tools/call filter. Order: blocklist → rate limit →
/// role gate (admin-only tools) → daily quota (User level). Denials return a spoken tool-error
/// result (never a protocol break) and are logged at Warning (never Error).
/// </summary>
public class McpEnforcementService
{
    public const string PlatformStatsToolName = "get_platform_stats";

    private static readonly HashSet<string> AdminOnlyTools = new(StringComparer.Ordinal)
    {
        PlatformStatsToolName
    };

    private readonly McpRateLimiter _rateLimiter;
    private readonly InMemoryMcpUsageStore _usage;
    private readonly IOptionsMonitor<McpEnforcementOptions> _options;
    private readonly IOptionsMonitor<WriteConfirmationOptions> _writeConfirmation;
    private readonly ILogger<McpEnforcementService> _logger;

    public McpEnforcementService(
        McpRateLimiter rateLimiter,
        InMemoryMcpUsageStore usage,
        IOptionsMonitor<McpEnforcementOptions> options,
        IOptionsMonitor<WriteConfirmationOptions> writeConfirmation,
        ILogger<McpEnforcementService> logger)
    {
        _rateLimiter = rateLimiter;
        _usage = usage;
        _options = options;
        _writeConfirmation = writeConfirmation;
        _logger = logger;
    }

    public McpCaller ResolveCaller(ClaimsPrincipal? user) => CallerLevelResolver.Resolve(user);

    public bool IsAdminOnly(string tool) => AdminOnlyTools.Contains(tool);

    /// <summary>Returns a denial result to short-circuit the call, or null if the call is allowed.</summary>
    public async ValueTask<CallToolResult?> CheckAsync(McpCaller caller, string tool, CancellationToken ct)
    {
        var options = _options.CurrentValue;
        var profile = options.ActiveProfile();

        // 1. Blocklist — independent of the token's remaining lifetime.
        if (caller.Subject is not null &&
            options.BlockedSubjects.Contains(caller.Subject, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("MCP enforcement: blocked subject {Subject} attempted tool {Tool}.", caller.Subject, tool);
            return Error("Access for this account is currently suspended.");
        }

        // 2. Rate limit (per level).
        using var lease = await _rateLimiter.AcquireAsync(caller, ct);
        if (!lease.IsAcquired)
        {
            var limit = caller.Level switch
            {
                CallerLevel.Admin => profile.AdminPerMinute,
                CallerLevel.User => profile.UserPerMinute,
                _ => profile.AnonymousSharedPerMinute
            };
            var waitSeconds = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
                ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                : 60;

            _logger.LogWarning("MCP enforcement: rate limit hit (level={Level}, subject={Subject}, tool={Tool}).",
                caller.Level, caller.Subject ?? CallerLevelResolver.AnonymousPartitionKey, tool);

            return Error($"Rate limit exceeded ({limit} calls per minute). Please wait {waitSeconds} seconds before trying again.");
        }

        // 3. Role gate for admin-only tools.
        if (AdminOnlyTools.Contains(tool) && caller.Level != CallerLevel.Admin)
        {
            _logger.LogWarning("MCP enforcement: admin tool {Tool} denied (level={Level}, subject={Subject}).",
                tool, caller.Level, caller.Subject);
            return Error("This tool requires the Admin role.");
        }

        // 4. Daily quota — User level only (Admins unlimited, anonymous has no quota).
        if (caller.Level == CallerLevel.User && caller.Subject is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (_usage.GetDailyCount(caller.Subject, today) >= profile.UserDailyQuota)
            {
                _logger.LogWarning("MCP enforcement: daily quota exhausted (subject={Subject}, tool={Tool}).",
                    caller.Subject, tool);
                return Error($"Daily quota exceeded ({profile.UserDailyQuota} calls per day). Quota resets at midnight UTC.");
            }
        }

        return null;
    }

    /// <summary>
    /// Counts a successful tool execution. When <paramref name="countTowardQuota"/> is false
    /// (an unconfirmed write preview), tool/level stats are still recorded but the per-subject
    /// daily quota is not incremented.
    /// </summary>
    public void RecordSuccess(McpCaller caller, string tool, bool countTowardQuota = true)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _usage.RecordCall(countTowardQuota ? caller.Subject : null, caller.Level, tool, today);
    }

    /// <summary>
    /// Whether a successful call should count toward the daily quota. An unconfirmed first call to
    /// a write tool (ToolLevel confirmation) is a preview and does not.
    /// </summary>
    public bool CountsTowardQuota(string tool, IDictionary<string, System.Text.Json.JsonElement>? arguments)
        => !WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            _writeConfirmation.CurrentValue.WriteConfirmation, tool, arguments);

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };
}
