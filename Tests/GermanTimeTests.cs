using Gesellschaftsspieler.MCPServer.Logging;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class GermanTimeTests
{
    [Fact]
    public void FromUtc_InWinter_AddsOneHour()
    {
        var local = GermanTime.FromUtc(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 1, 15, 13, 0, 0), local);
    }

    [Fact]
    public void FromUtc_InSummer_AddsTwoHours()
    {
        var local = GermanTime.FromUtc(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 15, 14, 0, 0), local);
    }

    [Fact]
    public void FromUtc_TreatsUnspecifiedKindAsUtc()
    {
        var local = GermanTime.FromUtc(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTime(2026, 1, 15, 13, 0, 0), local);
    }
}
