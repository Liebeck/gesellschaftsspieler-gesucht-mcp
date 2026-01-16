namespace Gesellschaftsspieler.MCPServer;

using Microsoft.EntityFrameworkCore;
using System.Net;


public sealed class McpCacheWarmupService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly McpInMemoryStore _store;
    private readonly ILogger<McpCacheWarmupService> _logger;

    public McpCacheWarmupService(IServiceProvider sp, McpInMemoryStore store, ILogger<McpCacheWarmupService> logger)
    {
        _sp = sp;
        _store = store;
        _logger = logger;
    }

    private static string? MapGamePlayTimeToText(int? value) => value switch
    {
        1 => "1 - 15 Minuten",
        2 => "15 - 30 Minuten",
        3 => "30 - 60 Minuten",
        4 => "1 - 2 Stunden",
        5 => "2 - 4 Stunden",
        6 => "4 - 8 Stunden",
        7 => "> 8 Stunden",
        null => null,
        _ => null
    };


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task RefreshAsync(CancellationToken ct)
    {
        _logger.LogInformation("Refreshing MCP in-memory cache...");

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<McpReadDbContext>();

        // Load all views (no tracking)
        var core = await db.GamesCore.AsNoTracking().ToListAsync(ct);
        var alt = await db.AlternativeNames.AsNoTracking().ToListAsync(ct);
        var authors = await db.GameAuthors.AsNoTracking().ToListAsync(ct);
        var publishers = await db.GamePublishers.AsNoTracking().ToListAsync(ct);
        var categories = await db.GameCategories.AsNoTracking().ToListAsync(ct);
        var ratings = await db.RatingAggregates.AsNoTracking().ToListAsync(ct);

        // Build dictionary
        var dict = core.ToDictionary(
    g => g.GameId,
    g => new GameReadModel
    {
        GameId = g.GameId,
        GameHashId = g.GameHashId,
        Name = WebUtility.HtmlDecode(g.Name),
        ReleaseYear = g.ReleaseYear,
        PlayerMin = g.PlayerCountMin,
        PlayerMax = g.PlayerCountMax,
        GsgRank = g.GSGRank,
        GamePlayTime = MapGamePlayTimeToText(g.GamePlayTime) // <-- here
    });


        foreach (var a in alt)
            if (dict.TryGetValue(a.GameId, out var game))
                game.AlternativeNames.Add(WebUtility.HtmlDecode(a.AlternativeName));

        foreach (var a in authors)
            if (dict.TryGetValue(a.GameId, out var game))
                game.Authors.Add(WebUtility.HtmlDecode(a.AuthorDisplayName));

        foreach (var p in publishers)
            if (dict.TryGetValue(p.GameId, out var game))
                game.Publishers.Add(WebUtility.HtmlDecode(p.PublisherName));

        foreach (var c in categories)
            if (dict.TryGetValue(c.GameId, out var game))
                game.Categories.Add(WebUtility.HtmlDecode(c.CategoryName));

        foreach (var r in ratings)
            if (dict.TryGetValue(r.GameId, out var game))
            {
                game.Likes = r.LikesCount;
                game.Favorites = r.FavoritesCount;
                game.RatingsCount = r.RatingsCount;
                game.AvgRating = r.AvgRating;
            }

        // Precompute SearchText (simple v1)
        foreach (var g in dict.Values)
        {
            g.SearchText = NormalizeForSearch(
                string.Join(" | ",
                    new[] { g.Name }
                    .Concat(g.AlternativeNames)
                    .Concat(g.Authors)
                    .Concat(g.Publishers)
                )
            );
        }

        _store.ReplaceAll(dict);

        _logger.LogInformation("MCP cache refreshed. Games loaded: {Count}", dict.Count);
    }

    private static string NormalizeForSearch(string input)
        => input.ToLowerInvariant();
}
