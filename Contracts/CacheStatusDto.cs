namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class CacheStatusDto
{
    public int GamesLoaded { get; set; }
    public DateTimeOffset LastRefreshUtc { get; set; }
}
