using Microsoft.Extensions.Hosting;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>
/// Existing job mechanism for this server is IHostedService. This one prunes in-memory usage
/// counters older than 60 days once a day (keeps the dictionaries from growing unbounded).
/// </summary>
public class McpUsagePruneService : BackgroundService
{
    private const int RetentionDays = 60;

    private readonly InMemoryMcpUsageStore _usage;

    public McpUsagePruneService(InMemoryMcpUsageStore usage) => _usage = usage;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            _usage.PruneOlderThan(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-RetentionDays));
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
