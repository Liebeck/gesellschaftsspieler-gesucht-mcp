using Gesellschaftsspieler.MCPServer.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class ToolCallLogWriterTests
{
    private sealed class FakeStore : IToolCallLogStore
    {
        private readonly object _gate = new();
        private readonly List<McpToolCallLog> _saved = new();

        public int FailuresToThrow { get; set; }

        public int Calls { get; private set; }

        public IReadOnlyList<McpToolCallLog> Saved
        {
            get { lock (_gate) { return _saved.ToList(); } }
        }

        public Task SaveAsync(IReadOnlyList<McpToolCallLog> entries, CancellationToken ct)
        {
            lock (_gate)
            {
                Calls++;
                if (FailuresToThrow > 0)
                {
                    FailuresToThrow--;
                    throw new InvalidOperationException("database down");
                }

                _saved.AddRange(entries);
            }

            return Task.CompletedTask;
        }
    }

    private static ToolCallLogWriter CreateWriter(FakeStore store, int batchSize = 50)
        => new(store,
            Options.Create(new ToolCallLogOptions { BatchSize = batchSize, QueueCapacity = 100 }),
            NullLogger<ToolCallLogWriter>.Instance);

    private static ToolCallLogWriter CreateWriter(IToolCallLogStore store, int batchSize = 50)
        => new(store,
            Options.Create(new ToolCallLogOptions { BatchSize = batchSize, QueueCapacity = 100 }),
            NullLogger<ToolCallLogWriter>.Instance);

    private static McpToolCallLog Entry(string toolName)
        => new() { ToolName = toolName, CallerLevel = "Anonymous", Outcome = "Success" };

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Enqueue_WhileRunning_WritesEntries()
    {
        var store = new FakeStore();
        var writer = CreateWriter(store);
        await writer.StartAsync(CancellationToken.None);

        writer.Enqueue(Entry("search"));
        writer.Enqueue(Entry("get_game"));
        await WaitUntil(() => store.Saved.Count == 2);

        await writer.StopAsync(CancellationToken.None);
        Assert.Equal(new[] { "search", "get_game" }, store.Saved.Select(e => e.ToolName));
    }

    [Fact]
    public async Task StoreFailure_DropsThatBatch_AndKeepsWriting()
    {
        var store = new FakeStore { FailuresToThrow = 1 };
        var writer = CreateWriter(store);
        await writer.StartAsync(CancellationToken.None);

        writer.Enqueue(Entry("lost"));
        await WaitUntil(() => store.Calls >= 1);
        writer.Enqueue(Entry("kept"));
        await WaitUntil(() => store.Saved.Count == 1);

        await writer.StopAsync(CancellationToken.None);
        Assert.Equal("kept", Assert.Single(store.Saved).ToolName);
    }

    [Fact]
    public async Task Stop_DrainsQueuedEntries()
    {
        var store = new FakeStore();
        var writer = CreateWriter(store, batchSize: 2);
        for (var i = 0; i < 5; i++)
        {
            writer.Enqueue(Entry($"tool-{i}"));
        }

        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        Assert.Equal(5, store.Saved.Count);
    }

    [Fact]
    public async Task Batches_RespectBatchSize()
    {
        var store = new FakeStore();
        var writer = CreateWriter(store, batchSize: 2);
        for (var i = 0; i < 5; i++)
        {
            writer.Enqueue(Entry($"tool-{i}"));
        }

        await writer.StartAsync(CancellationToken.None);
        await WaitUntil(() => store.Saved.Count == 5);
        await writer.StopAsync(CancellationToken.None);

        Assert.True(store.Calls >= 3);
    }

    [Fact]
    public async Task Stop_Twice_DoesNotThrow()
    {
        var store = new FakeStore();
        var writer = CreateWriter(store);
        await writer.StartAsync(CancellationToken.None);
        writer.Enqueue(Entry("test"));

        // First stop
        await writer.StopAsync(CancellationToken.None);

        // Second stop should not throw
        await writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Enqueue_AfterStop_DoesNotThrow()
    {
        var store = new FakeStore();
        var writer = CreateWriter(store);
        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        // Enqueue after stop should not throw (channel is still open for writes)
        writer.Enqueue(Entry("late-entry"));
    }

    [Fact]
    public async Task Stop_WithCancelledToken_ReturnsWhileStoreHangs()
    {
        var blockingTcs = new TaskCompletionSource();
        var blockingStore = new BlockingStore { ReleaseAt = blockingTcs };
        var writer = CreateWriter(blockingStore);

        writer.Enqueue(Entry("blocking"));
        await writer.StartAsync(CancellationToken.None);

        // Wait for the store to be called
        await WaitUntil(() => blockingStore.StartedSaving);

        // Create a cancelled token
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // StopAsync with cancelled token should return promptly, not wait for the blocking store
        var stopTask = writer.StopAsync(cts.Token);
        var completedFirst = await Task.WhenAny(stopTask, Task.Delay(2000));

        // stopTask should have completed first (within ~2s timeout)
        Assert.Equal(stopTask, completedFirst);

        // Release the blocking store
        blockingTcs.SetResult();

        // Clean up
        writer.Dispose();
    }

    private sealed class BlockingStore : IToolCallLogStore
    {
        private readonly object _gate = new();

        public TaskCompletionSource? ReleaseAt { get; set; }

        public bool StartedSaving { get; private set; }

        public async Task SaveAsync(IReadOnlyList<McpToolCallLog> entries, CancellationToken ct)
        {
            lock (_gate)
            {
                StartedSaving = true;
            }

            if (ReleaseAt != null)
            {
                await ReleaseAt.Task;
            }
        }
    }
}
