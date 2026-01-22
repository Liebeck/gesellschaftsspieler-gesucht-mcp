namespace Gesellschaftsspieler.MCPServer;

public class VwMcpGameRatingAggregate
{
    public int GameId { get; set; }
    public int RatingsRowCount { get; set; }
    public int LikesCount { get; set; }
    public int FavoritesCount { get; set; }
    public int RatingsCount { get; set; }
    public double? AvgRating { get; set; }
    public DateTime? LastRatingUpdate { get; set; }
}
