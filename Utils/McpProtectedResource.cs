namespace Gesellschaftsspieler.MCPServer;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Resolves the OAuth 2.0 protected-resource identity (RFC 9728) and the set of accepted token
/// audiences from configuration. When the optional <c>Mcp:ResourceUrl</c> / <c>Mcp:ValidAudiences</c>
/// settings are absent every value falls back to today's behaviour, so an unconfigured server is
/// byte-for-byte unchanged. Setting <c>Mcp:ResourceUrl</c> lets a reverse proxy (e.g. Azure API
/// Management) sit in front of the server: the metadata then advertises the external identity while
/// the server itself stays reachable directly.
/// </summary>
public static class McpProtectedResource
{
    public const string ResourceUrlKey = "Mcp:ResourceUrl";
    public const string ValidAudiencesKey = "Mcp:ValidAudiences";
    public const string OidcAudienceKey = "Oidc:Audience";

    /// <summary>RFC 9728 well-known path for the protected-resource metadata document.</summary>
    public const string MetadataPath = "/.well-known/oauth-protected-resource";

    /// <summary>
    /// The <c>resource</c> value advertised in the protected-resource metadata document.
    /// Precedence: <c>Mcp:ResourceUrl</c> → <c>Oidc:Audience</c> → the current request origin.
    /// </summary>
    public static string ResolveResource(IConfiguration configuration, string requestOrigin)
    {
        var resourceUrl = configuration[ResourceUrlKey];
        if (!string.IsNullOrWhiteSpace(resourceUrl))
        {
            return resourceUrl;
        }

        var oidcAudience = configuration[OidcAudienceKey];
        return !string.IsNullOrWhiteSpace(oidcAudience) ? oidcAudience : requestOrigin;
    }

    /// <summary>
    /// The absolute URL of the protected-resource metadata document, used in the WWW-Authenticate
    /// <c>resource_metadata</c> hint of a 401 response. When <c>Mcp:ResourceUrl</c> is set the metadata
    /// is advertised under that URL's origin (e.g. the APIM gateway domain); otherwise it falls back to
    /// the current request origin (today's behaviour).
    /// </summary>
    public static string ResolveMetadataUrl(IConfiguration configuration, string requestOrigin)
    {
        var resourceUrl = configuration[ResourceUrlKey];
        if (!string.IsNullOrWhiteSpace(resourceUrl) &&
            Uri.TryCreate(resourceUrl, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority) + MetadataPath;
        }

        return requestOrigin + MetadataPath;
    }

    /// <summary>
    /// The audiences an access token may carry to be accepted. <c>Oidc:Audience</c> is always the
    /// default entry (today's single value); any <c>Mcp:ValidAudiences</c> entries are added so that
    /// tokens minted for several front doors — e.g. the direct App Service URL and an APIM gateway URL —
    /// validate in parallel. Blank entries are ignored and the result is de-duplicated.
    /// </summary>
    public static IReadOnlyList<string> ResolveValidAudiences(IConfiguration configuration)
    {
        var audiences = new List<string>();

        var oidcAudience = configuration[OidcAudienceKey];
        if (!string.IsNullOrWhiteSpace(oidcAudience))
        {
            audiences.Add(oidcAudience);
        }

        var configured = configuration.GetSection(ValidAudiencesKey).Get<string[]>();
        if (configured is not null)
        {
            foreach (var audience in configured)
            {
                if (!string.IsNullOrWhiteSpace(audience))
                {
                    audiences.Add(audience);
                }
            }
        }

        return audiences.Distinct(StringComparer.Ordinal).ToList();
    }
}
