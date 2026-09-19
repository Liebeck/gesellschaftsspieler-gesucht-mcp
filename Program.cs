using System.Threading.RateLimiting;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Gesellschaftsspieler.MCPServer;
using Gesellschaftsspieler.MCPServer.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


var builder = WebApplication.CreateBuilder(args);
var keyVaultUri = builder.Configuration["KeyVaultUri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    var credential = new DefaultAzureCredential();
    var secretClient = new SecretClient(new Uri(keyVaultUri), credential);

    builder.Configuration.AddAzureKeyVault(
        secretClient,
        new KeyVaultSecretManager());
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<McpReadDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("McpReadDb"));
});

// --- Tool-call log: every tools/call and initialize becomes a row in dbo.McpToolCallLogs.
// Written through a separate insert-only SQL user; without the connection string nothing is logged.
builder.Services.Configure<ToolCallLogOptions>(builder.Configuration.GetSection(ToolCallLogOptions.SectionName));
var toolCallLogConnection = builder.Configuration.GetConnectionString("McpLogDb");
var toolCallLogEnabled = builder.Configuration.GetValue($"{ToolCallLogOptions.SectionName}:Enabled", true);
if (toolCallLogEnabled && !string.IsNullOrWhiteSpace(toolCallLogConnection))
{
    builder.Services.AddDbContextFactory<McpLogDbContext>(options => options.UseSqlServer(
        toolCallLogConnection,
        // Force single-row inserts: with EF Core 10, a multi-row flush becomes a MERGE ... OUTPUT,
        // but the insert-only mcp_logger SQL user only has INSERT + SELECT on McpToolCallLogId.
        // MaxBatchSize(1) keeps every insert as INSERT ... OUTPUT INSERTED.McpToolCallLogId, which
        // those grants cover.
        sql => sql.MaxBatchSize(1)));
    builder.Services.AddSingleton<IToolCallLogStore, SqlToolCallLogStore>();
    builder.Services.AddSingleton<ToolCallLogWriter>();
    builder.Services.AddSingleton<IToolCallLogQueue>(sp => sp.GetRequiredService<ToolCallLogWriter>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ToolCallLogWriter>());
}
else
{
    builder.Services.AddSingleton<IToolCallLogQueue, NullToolCallLogQueue>();
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("PerIpRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.AddSingleton<McpInMemoryStore>();
builder.Services.AddSingleton<McpCacheWarmupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<McpCacheWarmupService>());
// Application Insights / Azure Monitor OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "Gesellschaftsspieler.MCPServer",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
    })
    .UseAzureMonitor(); // reads APPLICATIONINSIGHTS_CONNECTION_STRING
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(); // scans for [McpServerToolType]

// --- Enforcement (rate limits, quota, blocklist, roles) applied centrally to tools/call.
builder.Services.Configure<Gesellschaftsspieler.MCPServer.Enforcement.McpEnforcementOptions>(
    builder.Configuration.GetSection(Gesellschaftsspieler.MCPServer.Enforcement.McpEnforcementOptions.SectionName));
// Write-confirmation mode (ToolLevel default = two-call confirm parameter; Protocol = elicitation/MRTR).
builder.Services.Configure<Gesellschaftsspieler.MCPServer.Enforcement.WriteConfirmationOptions>(
    builder.Configuration.GetSection("Mcp"));
builder.Services.AddSingleton<Gesellschaftsspieler.MCPServer.Enforcement.InMemoryMcpUsageStore>();
builder.Services.AddSingleton<Gesellschaftsspieler.MCPServer.Enforcement.McpRateLimiter>();
builder.Services.AddSingleton<Gesellschaftsspieler.MCPServer.Enforcement.McpEnforcementService>();
builder.Services.AddHostedService<Gesellschaftsspieler.MCPServer.Enforcement.McpUsagePruneService>();
builder.Services.PostConfigure<ModelContextProtocol.Server.McpServerOptions>(options =>
{
    // SDK 2.0: request filters moved under Filters.Request (were directly on Filters in 0.5-preview).
    // The SDK wraps filters in list order (first = outermost). Logging goes first so it also
    // records the calls enforcement rejects.
    options.Filters.Request.CallToolFilters.Add(ToolCallLogFilters.CallTool);
    options.Filters.Request.CallToolFilters.Add(Gesellschaftsspieler.MCPServer.Enforcement.EnforcementFilters.CallTool);
    options.Filters.Request.ListToolsFilters.Add(Gesellschaftsspieler.MCPServer.Enforcement.EnforcementFilters.ListTools);
    options.Filters.Message.IncomingFilters.Add(ToolCallLogFilters.Initialize);
});

// --- OAuth: validate access tokens issued by Gesellschaftsspieler-gesucht (the authorization server).
// Tokens are plain JWTs (the AS disables access-token encryption), so standard JWT bearer works.
var oidcAuthority = builder.Configuration["Oidc:Authority"];
// Accepted token audiences: Oidc:Audience is always the default entry; Mcp:ValidAudiences adds more so
// that tokens minted for the direct App Service URL and for an APIM gateway URL validate in parallel.
var validAudiences = McpProtectedResource.ResolveValidAudiences(builder.Configuration);
// When true, the whole /mcp endpoint requires a valid token (401 -> OAuth discovery for MCP clients
// like ChatGPT/Claude). When false (default) public tools stay anonymous and only "whoami" needs auth.
var requireAuthentication = builder.Configuration.GetValue<bool>("Mcp:RequireAuthentication");

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidcAuthority;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // Keep original claim names (sub, preferred_username) instead of remapping them.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(oidcAuthority),
            ValidIssuer = oidcAuthority,
            ValidateAudience = validAudiences.Count > 0,
            ValidAudiences = validAudiences,
            NameClaimType = "preferred_username",
            RoleClaimType = "role"
        };
        // Point unauthenticated MCP clients at the RFC 9728 protected-resource metadata.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                var metadataUrl = McpProtectedResource.ResolveMetadataUrl(
                    builder.Configuration, $"{context.Request.Scheme}://{context.Request.Host}");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{metadataUrl}\"";
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Typed client for the web app's MCP-facing API (/api/mcp/*). Forwards the user's token.
var apiBaseUrl = builder.Configuration["GsGesucht:ApiBaseUrl"];
builder.Services.AddHttpClient<Gesellschaftsspieler.MCPServer.Services.GsGesuchtApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiBaseUrl))
    {
        client.BaseAddress = new Uri(apiBaseUrl);
    }
});


var app = builder.Build();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// RFC 9728 Protected Resource Metadata: lets MCP clients discover the authorization server.
app.MapGet("/.well-known/oauth-protected-resource", (HttpContext context) =>
{
    var resource = McpProtectedResource.ResolveResource(
        app.Configuration, $"{context.Request.Scheme}://{context.Request.Host}");
    return Results.Json(new
    {
        resource,
        authorization_servers = string.IsNullOrWhiteSpace(oidcAuthority)
            ? Array.Empty<string>()
            : new[] { oidcAuthority!.TrimEnd('/') },
        scopes_supported = new[] { "mcp" },
        bearer_methods_supported = new[] { "header" }
    });
});

var mcpEndpoint = app.MapMcp(pattern: "/mcp").RequireRateLimiting("PerIpRateLimit");
if (requireAuthentication)
{
    // Full protection: every /mcp call needs a valid token.
    mcpEndpoint.RequireAuthorization();
}
// Otherwise /mcp stays anonymous; UseAuthentication still populates the user when a token is
// present, so the "whoami" tool can identify the caller opportunistically.

app.Run();
