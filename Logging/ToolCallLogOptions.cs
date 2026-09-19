namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>Settings for the tool-call log (section <c>Mcp:ToolCallLog</c>).</summary>
public sealed class ToolCallLogOptions
{
    public const string SectionName = "Mcp:ToolCallLog";

    /// <summary>Logging also needs the <c>McpLogDb</c> connection string; without it nothing is written.</summary>
    public bool Enabled { get; set; } = true;

    public bool LogArguments { get; set; } = true;

    /// <summary>Results are always stored completely; this only switches them off.</summary>
    public bool LogResults { get; set; } = true;

    /// <summary>Entries waiting to be written; when full, new entries are dropped.</summary>
    public int QueueCapacity { get; set; } = 1_000;

    public int BatchSize { get; set; } = 50;
}
