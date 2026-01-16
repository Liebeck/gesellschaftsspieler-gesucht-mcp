using System.Threading.RateLimiting;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Gesellschaftsspieler.MCPServer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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


var app = builder.Build();
app.UseRateLimiter();
app.MapMcp(pattern: "/mcp").RequireRateLimiting("PerIpRateLimit");
app.Run();
