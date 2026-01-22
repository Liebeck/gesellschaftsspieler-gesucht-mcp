using Gesellschaftsspieler.MCPServer.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Gesellschaftsspieler.MCPServer.Tools;

[McpServerToolType]
public static class GameTools
{
    [McpServerTool(Name = "search")]
    [Description("Search for board games in Gesellschaftsspieler-gesucht.")]
    public static GameListResponseDto Search(
        McpInMemoryStore store,
        [Description("Search query (name, alternative names, authors, publishers).")] string query,
        [Description("Max results (default 10, max 50).")] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var needle = (query ?? string.Empty).Trim().ToLowerInvariant();

        var items = store.Games.Values
            .Where(g => g.SearchText.Contains(needle))
            .OrderBy(g => g.GsgRank ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .Take(limit)
            .Select(ToDetails)
            .ToList();

        return new GameListResponseDto { Total = items.Count, Items = items };
    }

    [McpServerTool(Name = "fetch")]
    [Description("Fetch full details for a board game by id (GameHashId).")]
    public static GameDetailsDto? Fetch(
        McpInMemoryStore store,
        [Description("The id returned from search (GameHashId).")] string id)
    {
        if (!TryGetByHash(store, id, out var g))
        {
            return null;
        }

        return ToDetails(g);
    }

    [McpServerTool(Name = "list_games")]
    [Description("List games with full details. Useful for debugging and demos.")]
    public static GameListResponseDto ListGames(
        McpInMemoryStore store,
        [Description("Max results (default 10, max 50).")] int limit = 10,
        [Description("Sort by: 'id', 'rank', 'name', 'year' (default 'id').")] string sort = "id")
    {
        limit = Math.Clamp(limit, 1, 50);

        IEnumerable<GameReadModel> query = store.Games.Values;

        query = sort?.ToLowerInvariant() switch
        {
            "rank" => query.OrderBy(g => g.GsgRank ?? int.MaxValue).ThenBy(g => g.Name),
            "name" => query.OrderBy(g => g.Name),
            "year" => query.OrderByDescending(g => g.ReleaseYear ?? int.MinValue).ThenBy(g => g.Name),
            _ => query.OrderBy(g => g.GameId)
        };

        var items = query
            .Take(limit)
            .Select(ToDetails)
            .ToList();

        return new GameListResponseDto { Total = items.Count, Items = items };
    }

    [McpServerTool(Name = "get_game")]
    [Description("Get full game details by GameHashId.")]
    public static GameDetailsDto? GetGame(
        McpInMemoryStore store,
        [Description("GameHashId.")] string gameId)
    {
        if (!TryGetByHash(store, gameId, out var g))
        {
            return null;
        }

        return ToDetails(g);
    }

    [McpServerTool(Name = "find_games_by_player_count")]
    [Description("Find games that support a given player count.")]
    public static GameListResponseDto FindGamesByPlayerCount(
        McpInMemoryStore store,
        [Description("Number of players.")] int players,
        [Description("Max results (default 10, max 50).")] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        var items = store.Games.Values
            .Where(g =>
                (!g.PlayerMin.HasValue || g.PlayerMin.Value <= players) &&
                (!g.PlayerMax.HasValue || g.PlayerMax.Value >= players))
            .OrderBy(g => g.GsgRank ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .Take(limit)
            .Select(ToDetails)
            .ToList();

        return new GameListResponseDto { Total = items.Count, Items = items };
    }

    [McpServerTool(Name = "get_top_games")]
    [Description("Get top games by community rank (GSGRank). Optional player filter.")]
    public static GameListResponseDto GetTopGames(
        McpInMemoryStore store,
        [Description("Optional: filter by player count.")] int? players = null,
        [Description("Max results (default 10, max 50).")] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        IEnumerable<GameReadModel> query = store.Games.Values;

        if (players.HasValue)
        {
            var p = players.Value;
            query = query.Where(g =>
                (!g.PlayerMin.HasValue || g.PlayerMin.Value <= p) &&
                (!g.PlayerMax.HasValue || g.PlayerMax.Value >= p));
        }

        var items = query
            .OrderBy(g => g.GsgRank ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .Take(limit)
            .Select(ToDetails)
            .ToList();

        return new GameListResponseDto { Total = items.Count, Items = items };
    }

    [McpServerTool(Name = "get_random_games")]
    [Description("Return random games")]
    public static GameListResponseDto GetRandomGames(
        McpInMemoryStore store,
        [Description("How many games (default 3, max 10).")] int count = 3)
    {
        count = Math.Clamp(count, 1, 10);

        IEnumerable<GameReadModel> query = store.Games.Values;

        var pool = query.ToList();
        if (pool.Count == 0)
        {
            return new GameListResponseDto { Total = 0, Items = [] };
        }

        var rng = Random.Shared;

        var items = pool
            .OrderBy(_ => rng.Next())
            .Take(count)
            .Select(ToDetails)
            .ToList();

        return new GameListResponseDto { Total = items.Count, Items = items };
    }

    private static bool TryGetByHash(McpInMemoryStore store, string? hashId, out GameReadModel game)
    {
        game = null!;
        if (string.IsNullOrWhiteSpace(hashId))
            return false;

        var g = store.Games.Values.FirstOrDefault(x => x.GameHashId.Equals(hashId, StringComparison.OrdinalIgnoreCase));
        if (g is null) return false;
        game = g;
        return true;
    }

    private static GameDetailsDto ToDetails(GameReadModel g) => new()
    {
        GameId = g.GameHashId,
        Name = g.Name,
        Url = BuildGameUrl(g.GameHashId),
        ReleaseYear = g.ReleaseYear,
        PlayerMin = g.PlayerMin,
        PlayerMax = g.PlayerMax,
        GsgRank = g.GsgRank,
        GamePlayTime = g.GamePlayTime,
        Likes = g.Likes,
        Favorites = g.Favorites,
        RatingsCount = g.RatingsCount,
        AvgRating = g.AvgRating,
        AlternativeNames = [.. g.AlternativeNames],
        Authors = [.. g.Authors],
        Publishers = [.. g.Publishers],
        Categories = [.. g.Categories]
    };

    private static string BuildGameUrl(string hashId) => $"https://gesellschaftsspieler-gesucht.de/spiele/{hashId}";
}
