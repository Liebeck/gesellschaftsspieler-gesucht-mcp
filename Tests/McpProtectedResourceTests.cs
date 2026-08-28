namespace Gesellschaftsspieler.MCPServer.Tests;

using System.Security.Cryptography;
using Gesellschaftsspieler.MCPServer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public class McpProtectedResourceTests
{
    private const string DirectUrl = "https://gsgesucht-mcp-live.azurewebsites.net";
    private const string ApimUrl = "https://gs-gesucht.azure-api.net/mcp";

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // --- resource identifier -------------------------------------------------

    [Fact]
    public void ResolveResource_DefaultsToOidcAudience_WhenResourceUrlUnset()
    {
        var config = Config(new() { ["Oidc:Audience"] = DirectUrl });

        Assert.Equal(DirectUrl, McpProtectedResource.ResolveResource(config, "https://request-host"));
    }

    [Fact]
    public void ResolveResource_UsesRequestOrigin_WhenNothingConfigured()
    {
        var config = Config(new());

        Assert.Equal("https://request-host", McpProtectedResource.ResolveResource(config, "https://request-host"));
    }

    [Fact]
    public void ResolveResource_PrefersResourceUrl_WhenSet()
    {
        var config = Config(new()
        {
            ["Oidc:Audience"] = DirectUrl,
            ["Mcp:ResourceUrl"] = ApimUrl,
        });

        Assert.Equal(ApimUrl, McpProtectedResource.ResolveResource(config, "https://request-host"));
    }

    // --- protected-resource-metadata URL (WWW-Authenticate hint) -------------

    [Fact]
    public void ResolveMetadataUrl_FallsBackToRequestOrigin_WhenResourceUrlUnset()
    {
        var config = Config(new() { ["Oidc:Audience"] = DirectUrl });

        Assert.Equal(
            "https://request-host/.well-known/oauth-protected-resource",
            McpProtectedResource.ResolveMetadataUrl(config, "https://request-host"));
    }

    [Fact]
    public void ResolveMetadataUrl_UsesResourceUrlOrigin_WhenSet()
    {
        var config = Config(new() { ["Mcp:ResourceUrl"] = ApimUrl });

        // The metadata document lives at the APIM origin, NOT under the /mcp path.
        Assert.Equal(
            "https://gs-gesucht.azure-api.net/.well-known/oauth-protected-resource",
            McpProtectedResource.ResolveMetadataUrl(config, "https://request-host"));
    }

    // --- audience list -------------------------------------------------------

    [Fact]
    public void ResolveValidAudiences_DefaultsToSingleOidcAudience()
    {
        var config = Config(new() { ["Oidc:Audience"] = DirectUrl });

        Assert.Equal(new[] { DirectUrl }, McpProtectedResource.ResolveValidAudiences(config));
    }

    [Fact]
    public void ResolveValidAudiences_MergesConfiguredEntries_AndDeduplicates()
    {
        var config = Config(new()
        {
            ["Oidc:Audience"] = DirectUrl,
            ["Mcp:ValidAudiences:0"] = DirectUrl, // duplicate of the default entry
            ["Mcp:ValidAudiences:1"] = ApimUrl,
        });

        Assert.Equal(new[] { DirectUrl, ApimUrl }, McpProtectedResource.ResolveValidAudiences(config));
    }

    // --- end-to-end token validation against the resolved audience list ------

    [Fact]
    public async Task Token_WithConfiguredApimAudience_IsAccepted_And_UnknownAudience_IsRejected()
    {
        var config = Config(new()
        {
            ["Oidc:Audience"] = DirectUrl,
            ["Mcp:ValidAudiences:0"] = ApimUrl,
        });

        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var handler = new JsonWebTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateLifetime = false,
            ValidateAudience = true,
            ValidAudiences = McpProtectedResource.ResolveValidAudiences(config),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
        };

        string Token(string audience) => handler.CreateToken(new SecurityTokenDescriptor
        {
            Audience = audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });

        // The direct URL (default entry) and the APIM URL both validate.
        Assert.True((await handler.ValidateTokenAsync(Token(DirectUrl), validationParameters)).IsValid);
        Assert.True((await handler.ValidateTokenAsync(Token(ApimUrl), validationParameters)).IsValid);

        // An unknown audience is still rejected.
        var rejected = await handler.ValidateTokenAsync(Token("https://attacker.example.com"), validationParameters);
        Assert.False(rejected.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(rejected.Exception);
    }
}
