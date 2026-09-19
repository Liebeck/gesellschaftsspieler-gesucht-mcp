using Microsoft.EntityFrameworkCore;

namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>Persists a batch of log rows.</summary>
public interface IToolCallLogStore
{
    Task SaveAsync(IReadOnlyList<McpToolCallLog> entries, CancellationToken ct);
}

public sealed class SqlToolCallLogStore : IToolCallLogStore
{
    private readonly IDbContextFactory<McpLogDbContext> _contextFactory;

    public SqlToolCallLogStore(IDbContextFactory<McpLogDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task SaveAsync(IReadOnlyList<McpToolCallLog> entries, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.ToolCallLogs.AddRange(entries);
        await context.SaveChangesAsync(ct);
    }
}
