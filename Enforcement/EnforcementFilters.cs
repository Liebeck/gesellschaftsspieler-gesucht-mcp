using Gesellschaftsspieler.MCPServer.Logging;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>
/// MCP request filters wiring enforcement into the SDK's central pipeline. Only tools/call and
/// tools/list are touched — initialize, discovery, etc. are never rate-limited or counted.
/// </summary>
public static class EnforcementFilters
{
    /// <summary>Rate limit / quota / blocklist / role enforcement around every tools/call.</summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallTool(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, ct) =>
        {
            var enforcement = context.Services!.GetRequiredService<McpEnforcementService>();
            var caller = enforcement.ResolveCaller(context.User);
            var tool = context.Params?.Name ?? "unknown";

            var denial = await enforcement.CheckAsync(caller, tool, ct);
            if (denial is not null)
            {
                ToolCallLogFilters.MarkDenied(context);
                return denial;
            }

            var result = await next(context, ct);

            // Count successful executions. An unconfirmed write preview still consumed a rate-limit
            // permit (in CheckAsync) but must not burn the daily quota.
            if (result?.IsError != true)
            {
                var countsTowardQuota = enforcement.CountsTowardQuota(tool, context.Params?.Arguments);
                enforcement.RecordSuccess(caller, tool, countsTowardQuota);
            }

            return result;
        };

    /// <summary>Hides admin-only tools (e.g. get_platform_stats) from non-admins in tools/list.</summary>
    public static McpRequestHandler<ListToolsRequestParams, ListToolsResult> ListTools(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
        => async (context, ct) =>
        {
            var result = await next(context, ct);

            var enforcement = context.Services!.GetRequiredService<McpEnforcementService>();
            var caller = enforcement.ResolveCaller(context.User);

            if (caller.Level != CallerLevel.Admin && result?.Tools is { Count: > 0 })
            {
                result.Tools = result.Tools.Where(tool => !enforcement.IsAdminOnly(tool.Name)).ToList();
            }

            return result;
        };
}
