using Gesellschaftsspieler.MCPServer.Enforcement;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class EnforcementOptionsTests
{
    [Fact]
    public void ActiveProfile_ReturnsNamedProfile()
    {
        var options = new McpEnforcementOptions
        {
            Profile = "Demo",
            Profiles =
            {
                ["Production"] = new EnforcementProfile { UserPerMinute = 10 },
                ["Demo"] = new EnforcementProfile { UserPerMinute = 3 }
            }
        };

        Assert.Equal(3, options.ActiveProfile().UserPerMinute);
    }

    [Fact]
    public void ActiveProfile_UnknownName_FallsBackToFirst()
    {
        var options = new McpEnforcementOptions
        {
            Profile = "Missing",
            Profiles = { ["Production"] = new EnforcementProfile { UserPerMinute = 10 } }
        };

        Assert.Equal(10, options.ActiveProfile().UserPerMinute);
    }

    [Fact]
    public void ActiveProfile_NoProfiles_ReturnsDefaults()
    {
        var options = new McpEnforcementOptions { Profile = "x" };

        var profile = options.ActiveProfile();

        Assert.NotNull(profile);
        Assert.True(profile.UserPerMinute > 0);
    }
}
