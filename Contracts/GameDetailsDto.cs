namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class GameDetailsDto
{
    public string GameId { get; set; }
    public string Name { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }
    public int? PlayerMin { get; set; }
    public int? PlayerMax { get; set; }
    public int? GsgRank { get; set; }

    public int Likes { get; set; }
    public int Favorites { get; set; }
    public int RatingsCount { get; set; }
    public double? AvgRating { get; set; }

    public List<string> AlternativeNames { get; set; } = new();
    public List<string> Authors { get; set; } = new();
    public List<string> Publishers { get; set; } = new();
    public List<string> Categories { get; set; } = new();

    public string? GamePlayTime { get; set; } // one field only
}
