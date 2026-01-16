namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class GameListResponseDto
{
    public int Total { get; set; }
    public List<GameSummaryDto> Items { get; set; } = new();
}
