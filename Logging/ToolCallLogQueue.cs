namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>Accepts log rows without waiting for the database.</summary>
public interface IToolCallLogQueue
{
    void Enqueue(McpToolCallLog entry);
}

/// <summary>Used when tool-call logging is disabled or no McpLogDb connection string is configured.</summary>
public sealed class NullToolCallLogQueue : IToolCallLogQueue
{
    public void Enqueue(McpToolCallLog entry)
    {
    }
}
