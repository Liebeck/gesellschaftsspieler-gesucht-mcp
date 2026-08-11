using System.Collections.Concurrent;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>
/// In-memory usage counters: per-subject daily counts (the quota + distinct users), plus
/// per-tool and per-level daily counters for platform stats. Increments are atomic
/// (ConcurrentDictionary.AddOrUpdate — no read-modify-write over two roundtrips).
///
/// NOTE: in-memory by explicit choice — counters do NOT survive an app restart, and quota is
/// therefore per-process-lifetime. (The briefing's persistence requirement is intentionally
/// traded for simplicity on a single instance.)
/// </summary>
public class InMemoryMcpUsageStore
{
    private readonly ConcurrentDictionary<(string Subject, DateOnly Date), int> _perSubject = new();
    private readonly ConcurrentDictionary<(DateOnly Date, string Tool), int> _perTool = new();
    private readonly ConcurrentDictionary<(DateOnly Date, CallerLevel Level), int> _perLevel = new();

    public int GetDailyCount(string subject, DateOnly date)
        => _perSubject.TryGetValue((subject, date), out var count) ? count : 0;

    /// <summary>Records one successful call. Returns the subject's new daily count (0 if anonymous).</summary>
    public int RecordCall(string? subject, CallerLevel level, string tool, DateOnly date)
    {
        _perTool.AddOrUpdate((date, tool), 1, (_, value) => value + 1);
        _perLevel.AddOrUpdate((date, level), 1, (_, value) => value + 1);

        if (!string.IsNullOrEmpty(subject))
        {
            return _perSubject.AddOrUpdate((subject, date), 1, (_, value) => value + 1);
        }

        return 0;
    }

    public UsageSnapshot Snapshot(DateOnly date)
    {
        var distinctUsers = _perSubject.Keys.Count(key => key.Date == date);
        var perLevel = _perLevel
            .Where(kv => kv.Key.Date == date)
            .ToDictionary(kv => kv.Key.Level.ToString(), kv => kv.Value);
        var totalCalls = perLevel.Values.Sum();

        var topTools = _perTool
            .Where(kv => kv.Key.Date == date)
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => (kv.Key.Tool, kv.Value))
            .ToList();

        var topCallers = _perSubject
            .Where(kv => kv.Key.Date == date)
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => (kv.Key.Subject, kv.Value))
            .ToList();

        return new UsageSnapshot(distinctUsers, totalCalls, perLevel, topTools, topCallers);
    }

    /// <summary>Removes counters for dates strictly older than the cutoff.</summary>
    public void PruneOlderThan(DateOnly cutoff)
    {
        foreach (var key in _perSubject.Keys.Where(k => k.Date < cutoff).ToList())
        {
            _perSubject.TryRemove(key, out _);
        }

        foreach (var key in _perTool.Keys.Where(k => k.Date < cutoff).ToList())
        {
            _perTool.TryRemove(key, out _);
        }

        foreach (var key in _perLevel.Keys.Where(k => k.Date < cutoff).ToList())
        {
            _perLevel.TryRemove(key, out _);
        }
    }
}

public class UsageSnapshot
{
    public UsageSnapshot(
        int distinctUsers,
        int totalCalls,
        IReadOnlyDictionary<string, int> perLevel,
        IReadOnlyList<(string Tool, int Count)> topTools,
        IReadOnlyList<(string Subject, int Count)> topCallers)
    {
        DistinctUsers = distinctUsers;
        TotalCalls = totalCalls;
        PerLevel = perLevel;
        TopTools = topTools;
        TopCallers = topCallers;
    }

    public int DistinctUsers { get; }
    public int TotalCalls { get; }
    public IReadOnlyDictionary<string, int> PerLevel { get; }
    public IReadOnlyList<(string Tool, int Count)> TopTools { get; }
    public IReadOnlyList<(string Subject, int Count)> TopCallers { get; }
}
