namespace Gesellschaftsspieler.MCPServer;

using System.Diagnostics;

public static class McpTelemetry
{
    public static readonly ActivitySource ActivitySource = new("Gesellschaftsspieler.MCPServer");

    public static Activity? StartToolActivity(string toolName)
    {
        var a = ActivitySource.StartActivity($"mcp.tool:{toolName}", ActivityKind.Internal);
        a?.SetTag("mcp.tool", toolName);
        return a;
    }
}
