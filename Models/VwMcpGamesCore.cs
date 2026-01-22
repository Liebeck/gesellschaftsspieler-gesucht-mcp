namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGamesCore
{
    public int GameId { get; set; }
    public Guid GameGuid { get; set; }
    public string GameHashId { get; set; } = "";
    public string Name { get; set; } = "";
    public int? ReleaseYear { get; set; }
    public int? PlayerCountMin { get; set; }
    public int? PlayerCountMax { get; set; }
    public int? GamePlayTime { get; set; } // only if your column is int; otherwise adjust
    public int? GSGRank { get; set; }
    public DateTime CreateDate { get; set; }
}
