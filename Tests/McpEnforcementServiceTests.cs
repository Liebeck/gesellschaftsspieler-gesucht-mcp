using System.Security.Claims;
using Gesellschaftsspieler.MCPServer.Enforcement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class McpEnforcementServiceTests
{
    // Demo profile: anon 10/min, user 3/min, admin 20/min, daily quota 20.
    private static (McpEnforcementService service, InMemoryMcpUsageStore usage, McpEnforcementOptions options) Build()
    {
        var options = new McpEnforcementOptions
        {
            Profile = "Demo",
            Profiles =
            {
                ["Demo"] = new EnforcementProfile
                {
                    AnonymousSharedPerMinute = 10,
                    UserPerMinute = 3,
                    AdminPerMinute = 20,
                    UserDailyQuota = 20
                }
            }
        };

        var usage = new InMemoryMcpUsageStore();
        var rateLimiter = new McpRateLimiter(Options.Create(options));
        var service = new McpEnforcementService(
            rateLimiter,
            usage,
            new TestOptionsMonitor<McpEnforcementOptions>(options),
            new TestOptionsMonitor<WriteConfirmationOptions>(new WriteConfirmationOptions()),
            NullLogger<McpEnforcementService>.Instance);
        return (service, usage, options);
    }

    private static ClaimsPrincipal User(string sub, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", sub) };
        foreach (var role in roles) claims.Add(new Claim("role", role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static string? Text(CallToolResult? result)
        => result?.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;

    [Fact]
    public async Task User_FourthCallInWindow_IsRateLimited()
    {
        var (service, _, _) = Build();
        var caller = service.ResolveCaller(User("u1"));

        Assert.Null(await service.CheckAsync(caller, "search", default));
        Assert.Null(await service.CheckAsync(caller, "search", default));
        Assert.Null(await service.CheckAsync(caller, "search", default));

        var fourth = await service.CheckAsync(caller, "search", default);
        Assert.NotNull(fourth);
        Assert.True(fourth!.IsError);
        Assert.Contains("Rate limit exceeded", Text(fourth));
    }

    [Fact]
    public async Task Admin_TwentyCallsInWindow_AllAllowed()
    {
        var (service, _, _) = Build();
        var caller = service.ResolveCaller(User("a1", "Administrator"));

        for (var i = 0; i < 20; i++)
        {
            Assert.Null(await service.CheckAsync(caller, "search", default));
        }
    }

    [Fact]
    public async Task TwoUsers_HaveSeparateRateLimits()
    {
        var (service, _, _) = Build();
        var u1 = service.ResolveCaller(User("u1"));
        var u2 = service.ResolveCaller(User("u2"));

        for (var i = 0; i < 3; i++) await service.CheckAsync(u1, "search", default);
        Assert.NotNull(await service.CheckAsync(u1, "search", default)); // u1 limited

        Assert.Null(await service.CheckAsync(u2, "search", default)); // u2 unaffected
    }

    [Fact]
    public async Task Anonymous_SharesOnePartition()
    {
        var (service, _, _) = Build();
        var anon = service.ResolveCaller(new ClaimsPrincipal(new ClaimsIdentity()));

        for (var i = 0; i < 10; i++)
        {
            Assert.Null(await service.CheckAsync(anon, "search", default));
        }

        var eleventh = await service.CheckAsync(anon, "search", default);
        Assert.NotNull(eleventh);
        Assert.Contains("Rate limit exceeded", Text(eleventh));
    }

    [Fact]
    public async Task User_AtDailyQuota_IsBlockedWithQuotaMessage()
    {
        var (service, usage, options) = Build();
        var caller = service.ResolveCaller(User("u1"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Simulate having already used the full daily quota (without consuming rate permits).
        for (var i = 0; i < options.ActiveProfile().UserDailyQuota; i++)
        {
            usage.RecordCall("u1", CallerLevel.User, "search", today);
        }

        var result = await service.CheckAsync(caller, "search", default);
        Assert.NotNull(result);
        Assert.Contains("Daily quota exceeded", Text(result));
    }

    [Fact]
    public async Task BlockedSubject_IsRejectedImmediately()
    {
        var (service, _, options) = Build();
        options.BlockedSubjects.Add("u1");
        var caller = service.ResolveCaller(User("u1"));

        var result = await service.CheckAsync(caller, "search", default);
        Assert.NotNull(result);
        Assert.Contains("suspended", Text(result));
    }

    [Fact]
    public async Task AdminTool_DeniedForUser_AllowedForAdmin()
    {
        var (service, _, _) = Build();

        var userResult = await service.CheckAsync(service.ResolveCaller(User("u1")), McpEnforcementService.PlatformStatsToolName, default);
        Assert.NotNull(userResult);
        Assert.Contains("Admin role", Text(userResult));

        var adminResult = await service.CheckAsync(service.ResolveCaller(User("a1", "Administrator")), McpEnforcementService.PlatformStatsToolName, default);
        Assert.Null(adminResult);
    }

    [Fact]
    public void UnconfirmedWritePreview_DoesNotConsumeQuota()
    {
        var (service, usage, _) = Build();
        var caller = service.ResolveCaller(User("u1"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Unconfirmed write preview (ToolLevel default): not counted toward the daily quota.
        Assert.False(service.CountsTowardQuota("add_game_to_collection", null));
        service.RecordSuccess(caller, "add_game_to_collection", countTowardQuota: false);
        Assert.Equal(0, usage.GetDailyCount("u1", today));

        // A normal call counts.
        Assert.True(service.CountsTowardQuota("search", null));
        service.RecordSuccess(caller, "search", countTowardQuota: true);
        Assert.Equal(1, usage.GetDailyCount("u1", today));
    }
}
