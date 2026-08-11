namespace Gesellschaftsspieler.MCPServer.Contracts;

// Client-side shapes for the web app's /api/mcp/* responses (the *MCPDO contracts).
// Deserialized case-insensitively (web JSON defaults).

public class GameRefDto
{
    public string GameHashId { get; set; }
    public string Name { get; set; }
    public int PlayCount { get; set; }
}

public class PlayStatsDto
{
    public int Year { get; set; }
    public int TotalPlays { get; set; }
    public int UniqueGames { get; set; }
    public double TotalHours { get; set; }
    public GameRefDto? MostPlayedGame { get; set; }
    public string? CoPlayerFilter { get; set; }
    public int? PlaysWithCoPlayer { get; set; }
}

public class LastPlayedDto
{
    public string GameHashId { get; set; }
    public string GameName { get; set; }
    public DateTime? LastPlayed { get; set; }
    public int? DaysAgo { get; set; }
    public int TotalPlays { get; set; }
}

public class GameSuggestionDto
{
    public string GameHashId { get; set; }
    public string Name { get; set; }
    public int? PlayerMin { get; set; }
    public int? PlayerMax { get; set; }
    public DateTime? LastPlayedEver { get; set; }
}

public class GamePerformanceDto
{
    public string GameHashId { get; set; }
    public string GameName { get; set; }
    public int Plays { get; set; }
    public int Wins { get; set; }
    public double? WinRatePercent { get; set; }
    public double? AveragePlacement { get; set; }
    public decimal? BestScore { get; set; }
}

public class MeetupNearbyDto
{
    public Guid MeetupGuid { get; set; }
    public string Title { get; set; }
    public DateTime MeetupDate { get; set; }
    public string City { get; set; }
    public string Zip { get; set; }
    public double DistanceKm { get; set; }
    public string OrganizerName { get; set; }
    public List<string> Games { get; set; } = new();
}

public class CollectionChangeDto
{
    public bool Success { get; set; }
    public bool AlreadyOwned { get; set; }
    public string GameHashId { get; set; }
    public string GameName { get; set; }
    public string Message { get; set; }
}

public class RatingChangeDto
{
    public bool Success { get; set; }
    public string GameHashId { get; set; }
    public string GameName { get; set; }
    public bool? Liked { get; set; }
    public bool? Favorite { get; set; }
    public decimal? Rating { get; set; }
    public string Message { get; set; }
}
