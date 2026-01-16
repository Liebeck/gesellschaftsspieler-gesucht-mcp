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

public class VwMcpGameAlternativeName
{
    public int GameId { get; set; }
    public string AlternativeName { get; set; } = "";
    public int? Language { get; set; } // matches your enum storage
    public DateTime AddedOn { get; set; }
}

public class VwMcpGameAuthor
{
    public int GameId { get; set; }
    public int GameAuthorId { get; set; }
    public string GameAuthorHashId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string AuthorDisplayName { get; set; } = "";
}

public class VwMcpGamePublisher
{
    public int GameId { get; set; }
    public int GamePublisherId { get; set; }
    public string GamePublisherHashId { get; set; } = "";
    public string PublisherName { get; set; } = "";
}

public class VwMcpGameCategory
{
    public int GameId { get; set; }
    public int GameCategoryId { get; set; }
    public string GameCategoryHashId { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int Ranking { get; set; }
}

public class VwMcpGameRatingAggregate
{
    public int GameId { get; set; }
    public int RatingsRowCount { get; set; }
    public int LikesCount { get; set; }
    public int FavoritesCount { get; set; }
    public int RatingsCount { get; set; }
    public double? AvgRating { get; set; }
    public DateTime? LastRatingUpdate { get; set; }
}

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

    public string? GamePlayTime { get; set; }   // <-- add this
}


public sealed class McpInMemoryStore
{
    private volatile Dictionary<int, GameReadModel> _games = new();
    private volatile Dictionary<string, int> _gameIdByHash = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<int, GameReadModel> Games => _games;

    public bool TryGetByHash(string hashId, out GameReadModel? game)
    {
        game = null;

        if (_gameIdByHash.TryGetValue(hashId, out var id) && _games.TryGetValue(id, out var g))
        {
            game = g;
            return true;
        }

        return false;
    }

    public DateTimeOffset LastRefreshUtc { get; private set; } = DateTimeOffset.MinValue;

    public void ReplaceAll(Dictionary<int, GameReadModel> newGames)
    {
        var newHashIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in newGames)
        {
            var game = kvp.Value;
            if (!string.IsNullOrWhiteSpace(game.GameHashId))
                newHashIndex[game.GameHashId] = kvp.Key;
        }

        _games = newGames;
        _gameIdByHash = newHashIndex;
        LastRefreshUtc = DateTimeOffset.UtcNow;
    }
}


