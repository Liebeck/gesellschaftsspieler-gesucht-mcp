using Gesellschaftsspieler.MCPServer.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Gesellschaftsspieler.MCPServer.Tests;

/// <summary>
/// The table is owned by the web app's migration; this mapping must match it column by column.
/// Building the model does not open a connection.
/// </summary>
public class McpLogDbContextTests
{
    private static IEntityType EntityType()
    {
        var options = new DbContextOptionsBuilder<McpLogDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var context = new McpLogDbContext(options);
        return context.Model.FindEntityType(typeof(McpToolCallLog))!;
    }

    [Fact]
    public void Model_MapsToMcpToolCallLogsTable()
    {
        Assert.Equal("McpToolCallLogs", EntityType().GetTableName());
    }

    [Fact]
    public void Model_UsesDatabaseGeneratedLongKey()
    {
        var key = EntityType().FindPrimaryKey()!.Properties.Single();

        Assert.Equal(nameof(McpToolCallLog.McpToolCallLogId), key.Name);
        Assert.Equal(typeof(long), key.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, key.ValueGenerated);
    }

    [Theory]
    [InlineData(nameof(McpToolCallLog.ToolName), 100)]
    [InlineData(nameof(McpToolCallLog.SessionId), 100)]
    [InlineData(nameof(McpToolCallLog.CallerLevel), 20)]
    [InlineData(nameof(McpToolCallLog.ClientName), 200)]
    [InlineData(nameof(McpToolCallLog.ClientVersion), 50)]
    [InlineData(nameof(McpToolCallLog.Outcome), 20)]
    [InlineData(nameof(McpToolCallLog.ErrorMessage), 1000)]
    public void Model_MatchesWebAppColumnLengths(string propertyName, int maxLength)
    {
        Assert.Equal(maxLength, EntityType().FindProperty(propertyName)!.GetMaxLength());
    }
}
