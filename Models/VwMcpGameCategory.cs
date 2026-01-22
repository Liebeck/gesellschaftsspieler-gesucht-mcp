namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGameCategory
{
    public int GameId { get; set; }
    public int GameCategoryId { get; set; }
    public string GameCategoryHashId { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int Ranking { get; set; }
}
