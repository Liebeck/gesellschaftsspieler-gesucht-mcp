namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class GameDetailsDto
{
    public string GameId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }
    public int? PlayerMin { get; set; }
    public int? PlayerMax { get; set; }
    public int? GsgRank { get; set; }
    public PlayTimeRange? GamePlayTime { get; set; }

    public int Likes { get; set; }
    public int Favorites { get; set; }
    public int RatingsCount { get; set; }
    public double? AvgRating { get; set; }

    public List<string> AlternativeNames { get; set; } = [];
    public List<string> Authors { get; set; } = [];
    public List<string> Publishers { get; set; } = [];
    public List<string> Categories { get; set; } = [];
}
