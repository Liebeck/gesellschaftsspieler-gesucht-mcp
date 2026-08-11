using System.ComponentModel;
using Gesellschaftsspieler.MCPServer.Contracts;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Gesellschaftsspieler.MCPServer.Tools;

[McpServerToolType]
public static class WhoAmITools
{
    [McpServerTool(Name = "whoami")]
    [Description("Returns the currently authenticated Gesellschaftsspieler-gesucht user (from the OAuth access token). Requires the caller to be authenticated.")]
    public static WhoAmIDto WhoAmI(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return new WhoAmIDto
            {
                IsAuthenticated = false,
                Message = "Du bist nicht angemeldet. Bitte melde dich über OAuth mit deinem " +
                          "Gesellschaftsspieler-gesucht-Konto an, um dieses Tool zu nutzen."
            };
        }

        // sub / preferred_username / role are kept unmapped (MapInboundClaims = false in Program.cs).
        var subject = user.FindFirst("sub")?.Value;
        var username = user.FindFirst("preferred_username")?.Value ?? user.Identity?.Name;
        var roles = user.FindAll("role").Select(claim => claim.Value).ToArray();

        return new WhoAmIDto
        {
            IsAuthenticated = true,
            Subject = subject,
            Username = username,
            Roles = roles,
            Message = $"Du bist als {username} angemeldet."
        };
    }
}
