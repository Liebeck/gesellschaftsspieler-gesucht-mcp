namespace Gesellschaftsspieler.MCPServer;

using Microsoft.EntityFrameworkCore;

public sealed class McpReadDbContext : DbContext
{
    public McpReadDbContext(DbContextOptions<McpReadDbContext> options) : base(options) { }

    public DbSet<VwMcpGamesCore> GamesCore => Set<VwMcpGamesCore>();
    public DbSet<VwMcpGameAlternativeName> AlternativeNames => Set<VwMcpGameAlternativeName>();
    public DbSet<VwMcpGameAuthor> GameAuthors => Set<VwMcpGameAuthor>();
    public DbSet<VwMcpGamePublisher> GamePublishers => Set<VwMcpGamePublisher>();
    public DbSet<VwMcpGameCategory> GameCategories => Set<VwMcpGameCategory>();
    public DbSet<VwMcpGameRatingAggregate> RatingAggregates => Set<VwMcpGameRatingAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VwMcpGamesCore>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_games_core");
        });

        modelBuilder.Entity<VwMcpGameAlternativeName>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_game_alternative_names");
        });

        modelBuilder.Entity<VwMcpGameAuthor>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_game_authors");
        });

        modelBuilder.Entity<VwMcpGamePublisher>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_game_publishers");
        });

        modelBuilder.Entity<VwMcpGameCategory>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_game_categories");
        });

        modelBuilder.Entity<VwMcpGameRatingAggregate>(b =>
        {
            b.HasNoKey();
            b.ToView("vw_mcp_game_rating_aggregates");
        });
    }
}
