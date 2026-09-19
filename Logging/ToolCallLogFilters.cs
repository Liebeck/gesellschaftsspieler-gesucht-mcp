using System.Diagnostics;
using Gesellschaftsspieler.MCPServer.Enforcement;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>
/// Records every tool call and every session start in one place, so a new tool is logged without
/// anyone remembering to instrument it. Logging must never break the call it observes.
/// </summary>
public static class ToolCallLogFilters
{
    /// <summary>Set by the enforcement filter when it rejects a call.</summary>
    private const string DeniedItemKey = "Gesellschaftsspieler.ToolCallLog.Denied";

    private const string InitializeMethod = "initialize";

    /// <summary>Lets the outer logging filter tell an enforcement rejection apart from a tool error.</summary>
    public static void MarkDenied(MessageContext context) => context.Items[DeniedItemKey] = true;

    /// <summary>
    /// The server runs the SDK's stateless HTTP transport, so for clients on protocols before
    /// 2026-07-28 <see cref="McpServer.ClientInfo"/> is null on tools/call. Falling back to the
    /// HTTP User-Agent still tells "which client" apart, even without the MCP client name.
    /// </summary>
    public static string? ResolveClientName(string? mcpClientName, string? userAgent)
    {
        if (!string.IsNullOrWhiteSpace(mcpClientName))
        {
            return mcpClientName;
        }

        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }

    /// <summary>Must be registered as the first CallToolFilter (outermost), so denials are logged too.</summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallTool(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, ct) =>
        {
            var calledAt = GermanTime.Now();
            var stopwatch = Stopwatch.StartNew();
            CallToolResult? result = null;
            Exception? failure = null;

            try
            {
                result = await next(context, ct);
                return result;
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                Record(context, () => new ToolCallObservation(
                    ToolName: context.Params?.Name ?? "(unknown)",
                    Arguments: context.Params?.Arguments,
                    Result: result,
                    Failure: failure,
                    Denied: context.Items.TryGetValue(DeniedItemKey, out var denied) && denied is true,
                    Caller: CallerLevelResolver.Resolve(context.User),
                    SessionId: context.Server?.SessionId,
                    ClientName: ResolveClientName(context.Server?.ClientInfo?.Name, GetUserAgent(context)),
                    ClientVersion: context.Server?.ClientInfo?.Version,
                    CalledAt: calledAt,
                    DurationMs: (int)stopwatch.ElapsedMilliseconds));
            }
        };

    /// <summary>
    /// Logs session starts ("initialize"), which tells "a client connected" apart from "a tool was
    /// used". There is no request filter for initialize, so this sits in the message pipeline and
    /// passes every other message straight through.
    /// </summary>
    public static McpMessageHandler Initialize(McpMessageHandler next)
        => async (context, ct) =>
        {
            if (context.JsonRpcMessage is not JsonRpcRequest { Method: InitializeMethod })
            {
                await next(context, ct);
                return;
            }

            var calledAt = GermanTime.Now();
            var stopwatch = Stopwatch.StartNew();
            Exception? failure = null;

            try
            {
                await next(context, ct);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                // Client details exist only after the handler has read them off the request.
                Record(context, () => new ToolCallObservation(
                    ToolName: InitializeMethod,
                    Arguments: null,
                    Result: null,
                    Failure: failure,
                    Denied: false,
                    Caller: CallerLevelResolver.Resolve(context.User),
                    SessionId: context.Server?.SessionId,
                    ClientName: ResolveClientName(context.Server?.ClientInfo?.Name, GetUserAgent(context)),
                    ClientVersion: context.Server?.ClientInfo?.Version,
                    CalledAt: calledAt,
                    DurationMs: (int)stopwatch.ElapsedMilliseconds));
            }
        };

    /// <summary>
    /// Reads the HTTP User-Agent header for the client-name fallback. Called only from inside
    /// <see cref="Record"/>'s try block (via the observation factory), so a missing/disposed
    /// <see cref="IHttpContextAccessor"/> or request never breaks the call it observes.
    /// </summary>
    private static string? GetUserAgent(MessageContext context)
    {
        var userAgent = context.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Headers["User-Agent"].ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }

    private static void Record(MessageContext context, Func<ToolCallObservation> observe)
    {
        try
        {
            var queue = context.Services?.GetService<IToolCallLogQueue>();
            var options = context.Services?.GetService<IOptions<ToolCallLogOptions>>()?.Value;
            if (queue is null || options is null)
            {
                return;
            }

            queue.Enqueue(ToolCallLogEntryFactory.Create(observe(), options));
        }
        catch
        {
            // Never let bookkeeping break the call it observes.
        }
    }
}
