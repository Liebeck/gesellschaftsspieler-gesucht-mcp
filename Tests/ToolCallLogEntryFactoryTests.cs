using System.Text.Json;
using Gesellschaftsspieler.MCPServer.Enforcement;
using Gesellschaftsspieler.MCPServer.Logging;
using ModelContextProtocol.Protocol;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class ToolCallLogEntryFactoryTests
{
    private static readonly DateTime CalledAt = new(2026, 9, 19, 14, 30, 0);

    private static ToolCallLogOptions Options(bool logArguments = true, bool logResults = true)
        => new() { LogArguments = logArguments, LogResults = logResults };

    private static ToolCallObservation Observation(
        CallToolResult? result = null,
        Exception? failure = null,
        bool denied = false,
        McpCaller? caller = null,
        IDictionary<string, JsonElement>? arguments = null)
        => new(
            ToolName: "search",
            Arguments: arguments,
            Result: result,
            Failure: failure,
            Denied: denied,
            Caller: caller ?? new McpCaller(CallerLevel.Anonymous, null, null),
            SessionId: "session-1",
            ClientName: "claude-ai",
            ClientVersion: "1.0",
            CalledAt: CalledAt,
            DurationMs: 42);

    private static CallToolResult TextResult(string text, bool isError = false)
        => new() { Content = [new TextContentBlock { Text = text }], IsError = isError };

    private static Dictionary<string, JsonElement> Args(params (string Key, object Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));

    [Fact]
    public void Create_SuccessfulCall_FillsCallFields()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult("{\"total\":0,\"items\":[]}")), Options());

        Assert.Equal("search", entry.ToolName);
        Assert.Equal(CalledAt, entry.CalledAt);
        Assert.Equal("session-1", entry.SessionId);
        Assert.Equal("claude-ai", entry.ClientName);
        Assert.Equal("1.0", entry.ClientVersion);
        Assert.Equal(42, entry.DurationMs);
        Assert.Equal(ToolCallLogEntryFactory.OutcomeSuccess, entry.Outcome);
        Assert.Null(entry.ErrorMessage);
    }

    [Fact]
    public void Create_AnonymousCaller_HasNoUserId()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(), Options());

        Assert.Null(entry.UserId);
        Assert.Equal("Anonymous", entry.CallerLevel);
    }

    [Fact]
    public void Create_UserCaller_StoresSubjectAsUserId()
    {
        var userId = Guid.NewGuid();
        var caller = new McpCaller(CallerLevel.User, userId.ToString(), "alice");

        var entry = ToolCallLogEntryFactory.Create(Observation(caller: caller), Options());

        Assert.Equal(userId, entry.UserId);
        Assert.Equal("User", entry.CallerLevel);
    }

    [Fact]
    public void Create_SubjectIsNotAGuid_LeavesUserIdEmpty()
    {
        var caller = new McpCaller(CallerLevel.Admin, "not-a-guid", "admin");

        var entry = ToolCallLogEntryFactory.Create(Observation(caller: caller), Options());

        Assert.Null(entry.UserId);
        Assert.Equal("Admin", entry.CallerLevel);
    }

    [Fact]
    public void Create_DeniedCall_IsDeniedWithReason()
    {
        var result = TextResult("Rate limit exceeded (30 calls per minute).", isError: true);

        var entry = ToolCallLogEntryFactory.Create(Observation(result: result, denied: true), Options());

        Assert.Equal(ToolCallLogEntryFactory.OutcomeDenied, entry.Outcome);
        Assert.Equal("Rate limit exceeded (30 calls per minute).", entry.ErrorMessage);
    }

    [Fact]
    public void Create_ErrorResult_IsErrorWithResultText()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult("Game not found", isError: true)), Options());

        Assert.Equal(ToolCallLogEntryFactory.OutcomeError, entry.Outcome);
        Assert.Equal("Game not found", entry.ErrorMessage);
    }

    [Fact]
    public void Create_Exception_IsErrorWithTypeAndMessage()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(failure: new InvalidOperationException("boom")), Options());

        Assert.Equal(ToolCallLogEntryFactory.OutcomeError, entry.Outcome);
        Assert.Equal("InvalidOperationException: boom", entry.ErrorMessage);
        Assert.Null(entry.ResultJson);
    }

    [Fact]
    public void Create_LongErrorMessage_IsCutAt1000Chars()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult(new string('x', 1500), isError: true)), Options());

        Assert.Equal(1000, entry.ErrorMessage!.Length);
    }

    [Fact]
    public void Create_Arguments_AreSerializedWithReadableUmlauts()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(arguments: Args(("query", "Brändi Dog"))), Options());

        Assert.Equal("{\"query\":\"Brändi Dog\"}", entry.ArgumentsJson);
    }

    [Fact]
    public void Create_NoArguments_StoresNull()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(arguments: new Dictionary<string, JsonElement>()), Options());

        Assert.Null(entry.ArgumentsJson);
    }

    [Fact]
    public void Create_ArgumentLoggingDisabled_StoresNull()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(arguments: Args(("query", "Catan"))), Options(logArguments: false));

        Assert.Null(entry.ArgumentsJson);
    }

    [Fact]
    public void Create_Result_IsStoredCompletely()
    {
        const string text = "{\"total\":1,\"items\":[{\"name\":\"Catan\"}]}";

        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult(text)), Options());

        Assert.Equal(text, entry.ResultJson);
    }

    [Fact]
    public void Create_VeryLongResult_IsNotTruncated()
    {
        var text = new string('x', 100_000);

        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult(text)), Options());

        Assert.Equal(text, entry.ResultJson);
    }

    [Fact]
    public void Create_ResultLoggingDisabled_KeepsItemCountButNoResult()
    {
        var entry = ToolCallLogEntryFactory.Create(
            Observation(result: TextResult("{\"total\":2,\"items\":[{},{}]}")), Options(logResults: false));

        Assert.Null(entry.ResultJson);
        Assert.Equal(2, entry.ResultItemCount);
    }

    [Theory]
    [InlineData("{\"total\":2,\"items\":[{},{}]}", 2)]
    [InlineData("{\"Total\":3,\"Items\":[1,2,3]}", 3)]
    [InlineData("[1,2,3,4]", 4)]
    public void Create_ListResult_CountsItems(string text, int expected)
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult(text)), Options());

        Assert.Equal(expected, entry.ResultItemCount);
    }

    [Theory]
    [InlineData("No games found")]
    [InlineData("{\"name\":\"Catan\"}")]
    public void Create_NonListResult_HasNoItemCount(string text)
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(result: TextResult(text)), Options());

        Assert.Null(entry.ResultItemCount);
    }

    [Fact]
    public void Create_NoResult_LeavesResultColumnsEmpty()
    {
        var entry = ToolCallLogEntryFactory.Create(Observation(), Options());

        Assert.Null(entry.ResultJson);
        Assert.Null(entry.ResultItemCount);
        Assert.Equal(ToolCallLogEntryFactory.OutcomeSuccess, entry.Outcome);
    }
}
