using Microsoft.EntityFrameworkCore;

namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>
/// Write-only context for the tool-call log. Separate from <see cref="McpReadDbContext"/> so the
/// read views and the log use different SQL users: the log user may only INSERT into this table.
/// Never migrate or EnsureCreated — the web app owns the schema.
/// </summary>
public sealed class McpLogDbContext : DbContext
{
    public McpLogDbContext(DbContextOptions<McpLogDbContext> options) : base(options) { }

    public DbSet<McpToolCallLog> ToolCallLogs => Set<McpToolCallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<McpToolCallLog>(b =>
        {
            b.ToTable("McpToolCallLogs");
            b.HasKey(e => e.McpToolCallLogId);
            b.Property(e => e.McpToolCallLogId).ValueGeneratedOnAdd();

            b.Property(e => e.ToolName).HasMaxLength(100);
            b.Property(e => e.SessionId).HasMaxLength(100);
            b.Property(e => e.CallerLevel).HasMaxLength(20).IsUnicode(false);
            b.Property(e => e.ClientName).HasMaxLength(200);
            b.Property(e => e.ClientVersion).HasMaxLength(50);
            b.Property(e => e.Outcome).HasMaxLength(20).IsUnicode(false);
            b.Property(e => e.ErrorMessage).HasMaxLength(1000);
        });
    }
}
