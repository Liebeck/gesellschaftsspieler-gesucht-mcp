using System.ComponentModel;
using System.Text.Json;
using Gesellschaftsspieler.MCPServer.Contracts;
using Gesellschaftsspieler.MCPServer.Enforcement;
using Gesellschaftsspieler.MCPServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Tools;

/// <summary>
/// User-scoped tools. They require an authenticated caller (OAuth) and delegate to the web app's
/// /api/mcp/* API, forwarding the user's token. Writes require an elicited confirmation.
/// </summary>
[McpServerToolType]
public static class UserTools
{
    private static readonly object AuthRequired = new
    {
        authenticated = false,
        message = "Bitte melde dich mit deinem Gesellschaftsspieler-gesucht-Konto an (OAuth), um dieses Tool zu nutzen."
    };

    [McpServerTool(Name = "get_my_play_stats")]
    [Description("Deine Spielstatistik für ein Jahr: Anzahl Partien, meistgespieltes Spiel, optional gefiltert nach Mitspieler.")]
    public static async Task<object> GetMyPlayStats(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        [Description("Jahr (Standard: aktuelles Jahr).")] int? year = null,
        [Description("Optional: Name eines Mitspielers, um nur Partien mit dieser Person zu zählen.")] string? withPlayer = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var query = $"api/mcp/play-stats?year={year?.ToString() ?? string.Empty}";
        if (!string.IsNullOrWhiteSpace(withPlayer))
        {
            query += $"&coPlayer={Uri.EscapeDataString(withPlayer)}";
        }

        return await api.GetAsync<PlayStatsDto>(query, ct) ?? (object)AuthRequired;
    }

    [McpServerTool(Name = "when_last_played")]
    [Description("Wann hast du ein bestimmtes Spiel zuletzt gespielt?")]
    public static async Task<object> WhenLastPlayed(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        McpInMemoryStore store,
        [Description("Name oder Id des Spiels.")] string game,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var hash = ResolveGameHash(store, game);
        if (hash is null) return GameNotFound(game);

        var result = await api.GetAsync<LastPlayedDto>($"api/mcp/last-played?gameHashId={Uri.EscapeDataString(hash)}", ct);
        return result ?? (object)new { message = $"Du hast '{game}' noch nie gespielt." };
    }

    [McpServerTool(Name = "suggest_from_collection")]
    [Description("Schlägt ein Spiel aus deiner Sammlung vor, das für die gewünschte Spielerzahl passt und das du dieses Jahr noch nicht gespielt hast.")]
    public static async Task<object> SuggestFromCollection(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        [Description("Anzahl Spieler.")] int players,
        [Description("Jahr, das als 'dieses Jahr' gilt (Standard: aktuelles Jahr).")] int? year = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var query = $"api/mcp/collection/suggestion?players={players}&max=5";
        if (year.HasValue) query += $"&year={year.Value}";

        return await api.GetAsync<List<GameSuggestionDto>>(query, ct) ?? new List<GameSuggestionDto>();
    }

    [McpServerTool(Name = "my_performance_in_game")]
    [Description("Wie gut bist du in einem Spiel? Partien, Siege, Siegquote und durchschnittliche Platzierung.")]
    public static async Task<object> MyPerformanceInGame(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        McpInMemoryStore store,
        [Description("Name oder Id des Spiels.")] string game,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var hash = ResolveGameHash(store, game);
        if (hash is null) return GameNotFound(game);

        var result = await api.GetAsync<GamePerformanceDto>($"api/mcp/performance?gameHashId={Uri.EscapeDataString(hash)}", ct);
        return result ?? (object)new { message = $"Keine erfassten Partien für '{game}'." };
    }

    [McpServerTool(Name = "find_meetups_nearby")]
    [Description("Findet öffentliche Spieltreffen in der Nähe einer Postleitzahl.")]
    public static async Task<object> FindMeetupsNearby(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        [Description("Postleitzahl.")] string zip,
        [Description("Radius in Kilometern (Standard 50).")] int radiusKm = 50,
        [Description("Länder-Id (Standard: Deutschland).")] int? countryId = null,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var query = $"api/mcp/meetups/nearby?zip={Uri.EscapeDataString(zip ?? string.Empty)}&radiusKm={radiusKm}";
        if (countryId.HasValue) query += $"&countryId={countryId.Value}";

        return await api.GetAsync<List<MeetupNearbyDto>>(query, ct) ?? new List<MeetupNearbyDto>();
    }

    [McpServerTool(Name = "add_game_to_collection")]
    [Description("Adds a game to the user's collection. WRITE — requires confirmation: called without " +
                "confirm=true it writes nothing and returns a confirmation prompt; ask the user, then call " +
                "again with confirm=true to actually add the game.")]
    public static async Task<object> AddGameToCollection(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        McpInMemoryStore store,
        McpServer server,
        IOptionsMonitor<WriteConfirmationOptions> writeConfirmation,
        ILoggerFactory loggerFactory,
        [Description("Name oder Id des Spiels.")] string game,
        [Description("Set to true to actually perform the write. Omitted/false only previews (confirmation prompt).")] bool confirm = false,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var hash = ResolveGameHash(store, game);
        if (hash is null) return GameNotFound(game);

        var name = store.TryGetByHash(hash, out var model) ? model.Name : game;

        var block = await RequireConfirmationAsync(
            writeConfirmation, server, loggerFactory, confirm,
            toolLevelAction: $"add '{name}' to your collection",
            protocolQuestion: $"'{name}' zu deiner Sammlung hinzufügen?", ct);
        if (block is not null) return block;

        return await api.PostAsync<CollectionChangeDto>("api/mcp/collection/add", new { GameHashId = hash }, ct)
            ?? (object)new { message = "Aktion fehlgeschlagen." };
    }

    [McpServerTool(Name = "rate_game")]
    [Description("Rates a game (like, favorite and/or score). WRITE — requires confirmation: called without " +
                "confirm=true it writes nothing and returns a confirmation prompt; ask the user, then call " +
                "again with confirm=true to save the rating.")]
    public static async Task<object> RateGame(
        IHttpContextAccessor httpContextAccessor,
        GsGesuchtApiClient api,
        McpInMemoryStore store,
        McpServer server,
        IOptionsMonitor<WriteConfirmationOptions> writeConfirmation,
        ILoggerFactory loggerFactory,
        [Description("Name oder Id des Spiels.")] string game,
        [Description("Optional: als 'gefällt mir' markieren.")] bool? like = null,
        [Description("Optional: als Favorit markieren.")] bool? favorite = null,
        [Description("Optional: Note/Bewertung.")] int? rating = null,
        [Description("Set to true to actually perform the write. Omitted/false only previews (confirmation prompt).")] bool confirm = false,
        CancellationToken ct = default)
    {
        if (!IsAuthenticated(httpContextAccessor)) return AuthRequired;

        var hash = ResolveGameHash(store, game);
        if (hash is null) return GameNotFound(game);

        var name = store.TryGetByHash(hash, out var model) ? model.Name : game;

        var block = await RequireConfirmationAsync(
            writeConfirmation, server, loggerFactory, confirm,
            toolLevelAction: $"save your rating for '{name}'",
            protocolQuestion: $"Bewertung für '{name}' speichern?", ct);
        if (block is not null) return block;

        var body = new { GameHashId = hash, Like = like, Favorite = favorite, Rating = (decimal?)rating };
        return await api.PostAsync<RatingChangeDto>("api/mcp/rating", body, ct)
            ?? (object)new { message = "Aktion fehlgeschlagen." };
    }

    /// <summary>
    /// Returns null to proceed with the write, or a response object to return instead (the
    /// confirmation prompt in ToolLevel mode, or a cancellation in Protocol mode).
    /// </summary>
    private static async Task<object?> RequireConfirmationAsync(
        IOptionsMonitor<WriteConfirmationOptions> writeConfirmation,
        McpServer server,
        ILoggerFactory loggerFactory,
        bool confirm,
        string toolLevelAction,
        string protocolQuestion,
        CancellationToken ct)
    {
        var mode = writeConfirmation.CurrentValue.WriteConfirmation;

        if (mode == WriteConfirmationMode.ToolLevel)
        {
            var decision = WriteConfirmationGate.Evaluate(confirm, toolLevelAction);
            return decision.Proceed ? null : new { confirmationRequired = true, message = decision.PromptMessage };
        }

        // Protocol mode: elicitation (2025 SDK) / MRTR (2.0). Currently under diagnosis (Phase 0).
        var confirmed = await ConfirmAsync(server, loggerFactory.CreateLogger("Mcp.WriteConfirmation"), protocolQuestion, ct);
        return confirmed ? null : new { confirmed = false, message = "Abgebrochen." };
    }

    private static bool IsAuthenticated(IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    private static object GameNotFound(string game)
        => new { error = "game_not_found", message = $"Kein Spiel zu '{game}' gefunden." };

    private static string? ResolveGameHash(McpInMemoryStore store, string game)
    {
        if (string.IsNullOrWhiteSpace(game)) return null;

        var trimmed = game.Trim();
        if (store.TryGetByHash(trimmed, out _)) return trimmed; // already a hash

        var needle = trimmed.ToLowerInvariant();
        var match = store.Games.Values
            .Where(g => g.SearchText.Contains(needle))
            .OrderBy(g => g.GsgRank ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .FirstOrDefault();

        return match?.GameHashId;
    }

    // Protocol-level confirmation (Elicitation). Phase 0 diagnostics: logs whether the request is
    // sent and what the client answers. Fails CLOSED (no write) on any error — e.g. a client that
    // does not support elicitation. Superseded by the tool-level confirm parameter by default.
    private static async Task<bool> ConfirmAsync(McpServer server, ILogger logger, string message, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Elicitation: sending confirmation request. message={Message}", message);

            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = message,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["confirm"] = new ElicitRequestParams.BooleanSchema()
                    },
                    Required = new List<string> { "confirm" }
                }
            }, ct);

            logger.LogInformation("Elicitation: client responded. isAccepted={IsAccepted}, hasContent={HasContent}",
                result.IsAccepted, result.Content is not null);

            if (!result.IsAccepted || result.Content is null)
            {
                return false;
            }

            return !result.Content.TryGetValue("confirm", out var value) || value.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Elicitation: request failed (client may not support elicitation) — treating as not confirmed.");
            return false;
        }
    }
}
