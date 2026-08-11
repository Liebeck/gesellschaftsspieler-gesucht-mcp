using System.ComponentModel;
using Gesellschaftsspieler.MCPServer.Contracts;
using Gesellschaftsspieler.MCPServer.Enforcement;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Tools;

[McpServerToolType]
public static class PlatformStatsTools
{
    // Access control is enforced centrally by the tools/call filter (role gate) and the tool is
    // hidden from non-admins in tools/list — this method is only reached by admins.
    [McpServerTool(Name = McpEnforcementService.PlatformStatsToolName)]
    [Description("Platform-Statistiken (nur Admins): Nutzer heute, Tool-Aufrufe gesamt und pro Stufe, Top-Tools.")]
    public static PlatformStatsDto GetPlatformStats(
        InMemoryMcpUsageStore usage,
        McpInMemoryStore store,
        IOptionsMonitor<McpEnforcementOptions> options)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = usage.Snapshot(today);

        return new PlatformStatsDto
        {
            Date = today,
            Profile = options.CurrentValue.Profile,
            DistinctUsersToday = snapshot.DistinctUsers,
            TotalToolCallsToday = snapshot.TotalCalls,
            CallsByLevel = new Dictionary<string, int>(snapshot.PerLevel),
            TopTools = snapshot.TopTools.Select(t => new ToolCountDto { Tool = t.Tool, Count = t.Count }).ToList(),
            TopCallers = snapshot.TopCallers.Select(c => new CallerCountDto { Subject = c.Subject, Count = c.Count }).ToList(),
            GamesInCatalog = store.Games.Count
        };
    }
}
