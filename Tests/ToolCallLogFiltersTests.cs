using Gesellschaftsspieler.MCPServer.Logging;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class ToolCallLogFiltersTests
{
    [Fact]
    public void ResolveClientName_PrefersMcpClientName_WhenPresent()
    {
        var name = ToolCallLogFilters.ResolveClientName("claude-desktop", "SomeAgent/1.0");

        Assert.Equal("claude-desktop", name);
    }

    [Fact]
    public void ResolveClientName_FallsBackToUserAgent_WhenMcpClientNameIsBlank()
    {
        var name = ToolCallLogFilters.ResolveClientName(null, "SomeAgent/1.0");

        Assert.Equal("SomeAgent/1.0", name);

        name = ToolCallLogFilters.ResolveClientName("   ", "SomeAgent/1.0");

        Assert.Equal("SomeAgent/1.0", name);
    }

    [Fact]
    public void ResolveClientName_ReturnsNull_WhenBothAreBlank()
    {
        var name = ToolCallLogFilters.ResolveClientName(null, null);

        Assert.Null(name);

        name = ToolCallLogFilters.ResolveClientName("", "   ");

        Assert.Null(name);
    }
}
