namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGameAlternativeName
{
    public int GameId { get; set; }
    public string AlternativeName { get; set; } = "";
    public int? Language { get; set; } // matches your enum storage
    public DateTime AddedOn { get; set; }
}
