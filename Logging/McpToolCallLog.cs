namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>
/// One row of dbo.McpToolCallLogs. The table is owned by the web app's EF migration
/// (AddMcpToolCallLogs); this class only mirrors its columns.
/// </summary>
public sealed class McpToolCallLog
{
    public long McpToolCallLogId { get; set; }

    /// <summary>Start of the call in German local time.</summary>
    public DateTime CalledAt { get; set; }

    /// <summary>Tool name, or "initialize" for a session start.</summary>
    public string ToolName { get; set; } = "";

    public string? SessionId { get; set; }

    public Guid? UserId { get; set; }

    public string CallerLevel { get; set; } = "";

    public string? ClientName { get; set; }

    public string? ClientVersion { get; set; }

    public string Outcome { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public int DurationMs { get; set; }

    public string? ArgumentsJson { get; set; }

    /// <summary>Complete result, never truncated.</summary>
    public string? ResultJson { get; set; }

    public int? ResultItemCount { get; set; }
}
