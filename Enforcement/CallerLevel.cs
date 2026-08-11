using System.Security.Claims;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

public enum CallerLevel
{
    Anonymous,
    User,
    Admin
}

/// <summary>The enforcement identity derived from a validated token.</summary>
public readonly record struct McpCaller(CallerLevel Level, string? Subject, string? Username);

/// <summary>
/// Determines the enforcement level from the token claims. Pure and unit-tested:
/// no token → Anonymous; sub present → User; sub present and a role containing "Admin" → Admin.
/// </summary>
public static class CallerLevelResolver
{
    public const string AnonymousPartitionKey = "anonymous";

    public static McpCaller Resolve(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new McpCaller(CallerLevel.Anonymous, null, null);
        }

        var subject = user.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return new McpCaller(CallerLevel.Anonymous, null, null);
        }

        var username = user.FindFirst("preferred_username")?.Value;

        var isAdmin = user.FindAll("role")
            .Any(claim => claim.Value.Contains("Admin", StringComparison.OrdinalIgnoreCase));

        return new McpCaller(isAdmin ? CallerLevel.Admin : CallerLevel.User, subject, username);
    }
}
