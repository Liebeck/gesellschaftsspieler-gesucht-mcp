using Gesellschaftsspieler.MCPServer.Contracts;

namespace Gesellschaftsspieler.MCPServer;

public sealed class GameReadModel
{
    public int GameId { get; init; }
    public string GameHashId { get; set; }
    public string Name { get; set; } = "";
    public int? ReleaseYear { get; set; }
    public int? PlayerMin { get; set; }
    public int? PlayerMax { get; set; }
    public int? GsgRank { get; set; }

    public string SearchText { get; set; } = "";

    public List<string> AlternativeNames { get; } = new();
    public List<string> Authors { get; } = new();
    public List<string> Publishers { get; } = new();
    public List<string> Categories { get; } = new();

    public int Likes { get; set; }
    public int Favorites { get; set; }
    public int RatingsCount { get; set; }
    public double? AvgRating { get; set; }

    public PlayTimeRange? GamePlayTime { get; set; }
}
