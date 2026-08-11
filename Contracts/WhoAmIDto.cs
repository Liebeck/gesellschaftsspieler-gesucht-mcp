namespace Gesellschaftsspieler.MCPServer.Contracts;

/// <summary>
/// Result of the <c>whoami</c> tool. Built entirely from the access-token claims — the MCP
/// server never touches the database for this.
/// </summary>
public class WhoAmIDto
{
    public bool IsAuthenticated { get; set; }

    /// <summary>The user id (sub claim), when authenticated.</summary>
    public string? Subject { get; set; }

    /// <summary>The display name (preferred_username claim), when authenticated.</summary>
    public string? Username { get; set; }

    /// <summary>The user's roles (role claims), when authenticated.</summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public string Message { get; set; } = string.Empty;
}
