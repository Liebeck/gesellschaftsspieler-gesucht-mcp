using System.ComponentModel;
using System.Net;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Tools;

[McpServerToolType]
public static class GameTools
{
    // -----------------------------
    // Connector-style tools
    // -----------------------------

    [McpServerTool(Name = "search")]
    [Description("Search for board games in Gesellschaftsspieler-gesucht.")]
    public static object Search(
        McpInMemoryStore store,
        [Description("Search query (name, alternative names, authors, publishers).")] string query,
        [Description("Max results (default 10, max 50).")] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var needle = (query ?? string.Empty).Trim().ToLowerInvariant();

        var results = store.Games.Values
            .Where(g => g.SearchText.Contains(needle))
            .OrderBy(g => g.GsgRank ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .Take(limit)
            .Select(g => new
            {
                id = g.GameHashId,
                title = g.Name, // already HtmlDecoded in warmup; if not, wrap with HtmlDecode
                url = BuildGameUrl(g.GameHashId)
            })
            .ToList();

        return new { results };
    }

    [McpServerTool(Name = "fetch")]
    [Description("Fetch full details for a board game by id (GameHashId).")]
    public static object Fetch(
        McpInMemoryStore store,
        [Description("The id returned from search (GameHashId).")] string id)
    {
        if (!TryGetByHash(store, id, out var g))
        {
            return new
            {
                id = id ?? "",
                title = "",
                text = "Not found.",
                url = BuildGameUrl(id ?? "")
            };
        }

        // For connector-style fetch, return a rich text payload.
        // Here we return JSON string in "text" to keep it self-contained.
        var payload = new
        {
            g.GameHashId,
            g.Name,
            g.ReleaseYear,
            PlayerMin = g.PlayerMin,
            PlayerMax = g.PlayerMax,
            g.GamePlayTime,
            g.GsgRank,
            Ratings = new
            {
                g.Likes,
                g.Favorites,
                g.RatingsCount,
                g.AvgRating
            },
            AlternativeNames = g.AlternativeNames,
            Authors = g.Authors,
            Publishers = g.Publishers,
            Categories = g.Categories
        };

        return new
        {
            id = g.GameHashId,
            title = g.Name,
            text = System.Text.Json.JsonSerializer.Serialize(payload),
            url = BuildGameUrl(g.GameHashId)
        };
    }

    // -----------------------------
    // Domain-specific tools (your previous REST endpoints)
    // -----------------------------

    [McpServerTool(Name = "list_games")]
    [Description("List games (summary). Useful for debugging and demos.")]
    public static object ListGames(
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
            .Select(ToSummary)
            .ToList();

        return new { items, count = items.Count };
    }

    [McpServerTool(Name = "get_game")]
    [Description("Get full game details by GameHashId.")]
    public static object GetGame(
        McpInMemoryStore store,
        [Description("GameHashId.")] string gameId)
    {
        if (!TryGetByHash(store, gameId, out var g))
            return new { found = false, gameId = gameId ?? "" };

        return new
        {
            found = true,
            game = new
            {
                Id = g.GameHashId,
                g.Name,
                g.ReleaseYear,
                PlayerMin = g.PlayerMin,
                PlayerMax = g.PlayerMax,
                g.GamePlayTime,
                g.GsgRank,
                Url = BuildGameUrl(g.GameHashId),
                Ratings = new
                {
                    g.Likes,
                    g.Favorites,
                    g.RatingsCount,
                    g.AvgRating
                },
                AlternativeNames = g.AlternativeNames,
                Authors = g.Authors,
                Publishers = g.Publishers,
                Categories = g.Categories
            }
        };
    }

    [McpServerTool(Name = "find_games_by_player_count")]
    [Description("Find games that support a given player count.")]
    public static object FindGamesByPlayerCount(
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
            .Select(ToSummary)
            .ToList();

        return new { players, items, count = items.Count };
    }

    [McpServerTool(Name = "get_top_games")]
    [Description("Get top games by community rank (GSGRank). Optional player filter.")]
    public static object GetTopGames(
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
            .Select(ToSummary)
            .ToList();

        return new { players, items, count = items.Count };
    }

    [McpServerTool(Name = "get_random_games")]
    [Description("Return random games. Optional player filter.")]
    public static object GetRandomGames(
        McpInMemoryStore store,
        [Description("How many games (default 3, max 10).")] int count = 3,
        [Description("Optional: filter by player count.")] int? players = null)
    {
        count = Math.Clamp(count, 1, 10);

        IEnumerable<GameReadModel> query = store.Games.Values;

        if (players.HasValue)
        {
            var p = players.Value;
            query = query.Where(g =>
                (!g.PlayerMin.HasValue || g.PlayerMin.Value <= p) &&
                (!g.PlayerMax.HasValue || g.PlayerMax.Value >= p));
        }

        var pool = query.ToList();
        if (pool.Count == 0)
            return new { players, items = new List<object>(), count = 0 };

        var rng = Random.Shared;

        var items = pool
            .OrderBy(_ => rng.Next())
            .Take(count)
            .Select(ToSummary)
            .ToList();

        return new { players, items, count = items.Count };
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private static bool TryGetByHash(McpInMemoryStore store, string? hashId, out GameReadModel game)
    {
        game = null!;
        if (string.IsNullOrWhiteSpace(hashId))
            return false;

        // Prefer your store's hash index if you added it:
        // return store.TryGetByHash(hashId, out game);

        // Fallback (works, but O(n)):
        var g = store.Games.Values.FirstOrDefault(x => x.GameHashId.Equals(hashId, StringComparison.OrdinalIgnoreCase));
        if (g is null) return false;
        game = g;
        return true;
    }

    private static object ToSummary(GameReadModel g) => new
    {
        id = g.GameHashId,
        g.Name,
        g.ReleaseYear,
        PlayerMin = g.PlayerMin,
        PlayerMax = g.PlayerMax,
        g.GamePlayTime,
        g.GsgRank,
        url = BuildGameUrl(g.GameHashId)
    };

    private static string BuildGameUrl(string hashId)
        => $"https://gesellschaftsspieler-gesucht.de/spiele/{hashId}";
}
