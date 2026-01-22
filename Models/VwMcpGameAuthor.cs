namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGameAuthor
{
    public int GameId { get; set; }
    public int GameAuthorId { get; set; }
    public string GameAuthorHashId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string AuthorDisplayName { get; set; } = "";
}
