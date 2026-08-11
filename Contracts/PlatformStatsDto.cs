namespace Gesellschaftsspieler.MCPServer.Contracts;

/// <summary>
/// Admin platform stats. Contains only counts, tool names and subject GUIDs — never internal
/// primary-key integers.
/// </summary>
public class PlatformStatsDto
{
    public DateOnly Date { get; set; }
    public string Profile { get; set; } = string.Empty;
    public int DistinctUsersToday { get; set; }
    public int TotalToolCallsToday { get; set; }
    public Dictionary<string, int> CallsByLevel { get; set; } = new();
    public List<ToolCountDto> TopTools { get; set; } = new();
    public List<CallerCountDto> TopCallers { get; set; } = new();
    public int GamesInCatalog { get; set; }
}

public class ToolCountDto
{
    public string Tool { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CallerCountDto
{
    /// <summary>The caller's subject (a GUID from the token), never an internal id.</summary>
    public string Subject { get; set; } = string.Empty;
    public int Count { get; set; }
}
