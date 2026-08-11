namespace Gesellschaftsspieler.MCPServer.Enforcement;

/// <summary>One named set of enforcement numbers (a profile such as "Production" or "Demo").</summary>
public class EnforcementProfile
{
    /// <summary>Shared per-minute rate limit for ALL anonymous callers together.</summary>
    public int AnonymousSharedPerMinute { get; set; } = 30;

    /// <summary>Per-user (per sub) per-minute rate limit.</summary>
    public int UserPerMinute { get; set; } = 10;

    /// <summary>Per-admin (per sub) per-minute rate limit.</summary>
    public int AdminPerMinute { get; set; } = 60;

    /// <summary>Per-user (per sub) daily quota. Admins are unlimited.</summary>
    public int UserDailyQuota { get; set; } = 500;
}

/// <summary>Bound from configuration section "Mcp:Enforcement".</summary>
public class McpEnforcementOptions
{
    public const string SectionName = "Mcp:Enforcement";

    /// <summary>Name of the active profile in <see cref="Profiles"/>.</summary>
    public string Profile { get; set; } = "Production";

    public Dictionary<string, EnforcementProfile> Profiles { get; set; } = new();

    /// <summary>Subjects (sub claim) that are immediately rejected, regardless of token validity.</summary>
    public List<string> BlockedSubjects { get; set; } = new();

    /// <summary>The currently selected profile, with a safe fallback.</summary>
    public EnforcementProfile ActiveProfile()
    {
        if (!string.IsNullOrWhiteSpace(Profile) && Profiles.TryGetValue(Profile, out var profile))
        {
            return profile;
        }

        return Profiles.Values.FirstOrDefault() ?? new EnforcementProfile();
    }
}
