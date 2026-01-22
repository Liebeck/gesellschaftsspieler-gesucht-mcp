namespace Gesellschaftsspieler.MCPServer;

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
