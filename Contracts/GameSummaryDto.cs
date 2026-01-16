namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class GameSummaryDto
{
    public string GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ReleaseYear { get; set; }
    public int? PlayerMin { get; set; }
    public int? PlayerMax { get; set; }
    public int? GsgRank { get; set; }

    public string? GamePlayTime { get; set; } // one field only
}
