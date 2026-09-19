# Gesellschaftsspieler-gesucht MCP Server 🎲🤖

An [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that enables AI assistants to search and recommend board games from the [Gesellschaftsspieler-gesucht](https://www.gs-gesucht.de) community platform.

## About

[Gesellschaftsspieler-gesucht](https://www.gs-gesucht.de) is a German board game community platform that helps players find others to play board games offline. This MCP server exposes the game catalog to AI assistants, allowing them to help users discover and recommend board games based on natural language queries.

> **Note:** This repository showcases the MCP server implementation. The database is not publicly accessible, so the code cannot be run as-is by external users. This is intended as a reference implementation and learning resource.

## Features

### MCP Tools

The server provides three main tools for AI assistants:

- **🔍 search**: Full-text search across game names, authors, and publishers
- **🏆 get_top_games**: Returns the top community-rated games
- **🎲 get_random_games**: Returns random games for discovery

### Technical Features

- **Rate Limiting**: Per-IP rate limiting (30 requests/minute) to protect the service
- **Security**: Secrets managed via Azure Key Vault
- **Observability**: OpenTelemetry integration with Azure Monitor for tracing and metrics
- **Performance**: In-memory caching with background warmup service
- **Data Access**: Read-only database access via Entity Framework Core, plus an insert-only tool-call log (every tool call and session start → one row in `McpToolCallLogs`, written asynchronously in batches; the server runs the SDK's stateless HTTP transport, so the logged session id is always empty, and the client name falls back to the HTTP User-Agent when the MCP client name isn't available)

## Architecture

```
┌─────────────────┐
│  AI Assistant   │
│ (Claude, etc.)  │
└────────┬────────┘
         │ MCP Protocol
         ▼
┌─────────────────┐
│   MCP Server    │
│  (ASP.NET Core) │
├─────────────────┤
│ • HTTP Transport│
│ • Rate Limiter  │
│ • Cache Layer   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   SQL Server    │
│ (Read + Log DB) │
└─────────────────┘
```

## Tech Stack

- **Framework**: ASP.NET Core (.NET 10, C# 14)
- **Database**: SQL Server with Entity Framework Core
- **Cloud**: Azure (Key Vault, Application Insights)
- **Observability**: OpenTelemetry + Azure Monitor
- **Security**: Azure Identity, DefaultAzureCredential
- **Rate Limiting**: ASP.NET Core Rate Limiting middleware

## Configuration

The application uses the following configuration:

- **KeyVaultUri**: Azure Key Vault endpoint for secrets management
- **ConnectionStrings:McpReadDb**: SQL Server connection string (can be stored in Key Vault)
- **ConnectionStrings:McpLogDb**: Connection string of an insert-only SQL user for the tool-call log (optional; without it nothing is logged)
- **Mcp:ToolCallLog**: `Enabled`, `LogArguments`, `LogResults`, `QueueCapacity`, `BatchSize`
- **APPLICATIONINSIGHTS_CONNECTION_STRING**: Azure Application Insights connection string

## API Endpoint

The MCP server is exposed at:
```
POST /mcp
```

Rate limit: 30 requests per minute per IP address

## Future Enhancements

- 🔐 OAuth integration for user-specific features
- 📚 Access to personal game collections
- 🎯 Personalized recommendations based on user preferences

## Development

### Project Structure

- `Program.cs`: Application startup and service configuration
- `McpReadDbContext`: Database context for read-only operations
- `McpInMemoryStore`: In-memory cache for game data
- `McpCacheWarmupService`: Background service for cache initialization

## About MCP

The Model Context Protocol (MCP) is an open standard that enables AI assistants to securely connect to external data sources and tools. This server implements the MCP specification to make board game data accessible to AI assistants.

Learn more: [https://modelcontextprotocol.io/](https://modelcontextprotocol.io/)

## Links

- 🎲 Platform: [www.gs-gesucht.de](https://www.gs-gesucht.de)
- 📖 MCP Specification: [modelcontextprotocol.io](https://modelcontextprotocol.io/)