using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>
/// Buffers log rows in a bounded channel and writes them in batches, so a tool call never waits
/// for — or fails because of — the log database.
/// </summary>
public sealed class ToolCallLogWriter : IHostedService, IToolCallLogQueue, IDisposable
{
    private readonly IToolCallLogStore _store;
    private readonly ILogger<ToolCallLogWriter> _logger;
    private readonly int _batchSize;
    private readonly Channel<McpToolCallLog> _channel;
    private long _dropped;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public ToolCallLogWriter(IToolCallLogStore store, IOptions<ToolCallLogOptions> options, ILogger<ToolCallLogWriter> logger)
    {
        _store = store;
        _logger = logger;
        _batchSize = Math.Max(1, options.Value.BatchSize);
        _channel = Channel.CreateBounded<McpToolCallLog>(
            new BoundedChannelOptions(Math.Max(1, options.Value.QueueCapacity))
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true
            },
            OnDropped);
    }

    public void Enqueue(McpToolCallLog entry) => _channel.Writer.TryWrite(entry);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_executingTask != null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_executingTask != null)
        {
            // Respect the host's cancellation token: wait for the task OR the shutdown timeout, whichever comes first.
            // Entries still queued when the host gives up are lost—acceptable.
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Dispose();
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;

        try
        {
            try
            {
                while (await reader.WaitToReadAsync(stoppingToken))
                {
                    await FlushAsync(ReadBatch(reader));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down — drain below.
            }
        }
        finally
        {
            // Write what is still queued so a deployment does not silently lose the last calls.
            List<McpToolCallLog> batch;
            while ((batch = ReadBatch(reader)).Count > 0)
            {
                await FlushAsync(batch);
            }

            // Close the writer to signal to the reader that no more items will be enqueued.
            _channel.Writer.TryComplete();
        }
    }

    private List<McpToolCallLog> ReadBatch(ChannelReader<McpToolCallLog> reader)
    {
        var batch = new List<McpToolCallLog>(_batchSize);
        while (batch.Count < _batchSize && reader.TryRead(out var entry))
        {
            batch.Add(entry);
        }

        return batch;
    }

    private async Task FlushAsync(List<McpToolCallLog> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            // No cancellation: the SQL command timeout bounds a hanging database.
            await _store.SaveAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool-call log: writing {Count} entries failed; they are dropped.", batch.Count);
        }
    }

    private void OnDropped(McpToolCallLog entry)
    {
        var dropped = Interlocked.Increment(ref _dropped);
        if (dropped % 100 == 1)
        {
            _logger.LogWarning("Tool-call log queue is full; {Dropped} entries dropped so far.", dropped);
        }
    }
}
