using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>
/// Partitioned fixed-window (1 minute) rate limiter. Partition key = sub for User/Admin, or the
/// shared constant "anonymous" for all anonymous callers together. The permit limit depends on
/// the caller level, read from the active profile at construction. A profile switch takes effect
/// on app restart (Azure App Service restarts on an app-setting change — no redeploy needed).
/// </summary>
public sealed class McpRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<McpCaller> _limiter;

    public McpRateLimiter(IOptions<McpEnforcementOptions> options)
    {
        var profile = options.Value.ActiveProfile();

        _limiter = PartitionedRateLimiter.Create<McpCaller, string>(caller => caller.Level switch
        {
            CallerLevel.Anonymous => RateLimitPartition.GetFixedWindowLimiter(
                CallerLevelResolver.AnonymousPartitionKey, _ => Window(profile.AnonymousSharedPerMinute)),

            CallerLevel.Admin => RateLimitPartition.GetFixedWindowLimiter(
                caller.Subject ?? "admin", _ => Window(profile.AdminPerMinute)),

            _ => RateLimitPartition.GetFixedWindowLimiter(
                caller.Subject ?? "user", _ => Window(profile.UserPerMinute))
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(McpCaller caller, CancellationToken ct)
        => _limiter.AcquireAsync(caller, 1, ct);

    private static FixedWindowRateLimiterOptions Window(int permitLimit) => new()
    {
        PermitLimit = Math.Max(1, permitLimit),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    };

    public void Dispose() => _limiter.Dispose();
}
