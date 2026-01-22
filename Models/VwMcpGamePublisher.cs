namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGamePublisher
{
    public int GameId { get; set; }
    public int GamePublisherId { get; set; }
    public string GamePublisherHashId { get; set; } = "";
    public string PublisherName { get; set; } = "";
}
