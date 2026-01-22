namespace Gesellschaftsspieler.MCPServer.Contracts;

public sealed class GameListResponseDto
{
    public int Total { get; set; }
    public List<GameDetailsDto> Items { get; set; } = [];
}
