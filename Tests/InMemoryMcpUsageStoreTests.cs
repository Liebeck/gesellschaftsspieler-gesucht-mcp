using Gesellschaftsspieler.MCPServer.Enforcement;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class InMemoryMcpUsageStoreTests
{
    private static readonly DateOnly Today = new(2026, 9, 4);
    private static readonly DateOnly Yesterday = new(2026, 9, 3);

    [Fact]
    public void RecordCall_IncrementsDailyCount()
    {
        var store = new InMemoryMcpUsageStore();

        store.RecordCall("u1", CallerLevel.User, "search", Today);
        store.RecordCall("u1", CallerLevel.User, "fetch", Today);

        Assert.Equal(2, store.GetDailyCount("u1", Today));
    }

    [Fact]
    public void Counts_AreSeparatePerDay()
    {
        var store = new InMemoryMcpUsageStore();

        store.RecordCall("u1", CallerLevel.User, "search", Yesterday);
        store.RecordCall("u1", CallerLevel.User, "search", Today);

        Assert.Equal(1, store.GetDailyCount("u1", Today));
        Assert.Equal(1, store.GetDailyCount("u1", Yesterday));
    }

    [Fact]
    public void AnonymousCall_DoesNotAffectSubjectCount()
    {
        var store = new InMemoryMcpUsageStore();

        var result = store.RecordCall(subject: null, CallerLevel.Anonymous, "search", Today);

        Assert.Equal(0, result);
        var snapshot = store.Snapshot(Today);
        Assert.Equal(0, snapshot.DistinctUsers);
        Assert.Equal(1, snapshot.TotalCalls);
    }

    [Fact]
    public void Increment_IsAtomicUnderParallelism()
    {
        var store = new InMemoryMcpUsageStore();

        Parallel.For(0, 1000, _ => store.RecordCall("u1", CallerLevel.User, "search", Today));

        Assert.Equal(1000, store.GetDailyCount("u1", Today));
    }

    [Fact]
    public void Snapshot_ReportsUsersTotalsAndTopTools()
    {
        var store = new InMemoryMcpUsageStore();
        store.RecordCall("u1", CallerLevel.User, "search", Today);
        store.RecordCall("u1", CallerLevel.User, "search", Today);
        store.RecordCall("u2", CallerLevel.Admin, "fetch", Today);

        var snapshot = store.Snapshot(Today);

        Assert.Equal(2, snapshot.DistinctUsers);
        Assert.Equal(3, snapshot.TotalCalls);
        Assert.Equal(2, snapshot.PerLevel["User"]);
        Assert.Equal(1, snapshot.PerLevel["Admin"]);
        Assert.Equal("search", snapshot.TopTools[0].Tool);
        Assert.Equal(2, snapshot.TopTools[0].Count);
    }

    [Fact]
    public void Prune_RemovesOlderDates()
    {
        var store = new InMemoryMcpUsageStore();
        store.RecordCall("u1", CallerLevel.User, "search", Yesterday);
        store.RecordCall("u1", CallerLevel.User, "search", Today);

        store.PruneOlderThan(Today);

        Assert.Equal(0, store.GetDailyCount("u1", Yesterday));
        Assert.Equal(1, store.GetDailyCount("u1", Today));
    }
}
