using System.Text.Json;
using Gesellschaftsspieler.MCPServer.Enforcement;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class WriteConfirmationTests
{
    // --- Tool-level gate (Phase 1): the two-call flow ---

    [Fact]
    public void Evaluate_WithoutConfirm_RequiresConfirmationWithSpokenPrompt()
    {
        var decision = WriteConfirmationGate.Evaluate(confirm: false, action: "add 'Wingspan' to your collection");

        Assert.False(decision.Proceed);
        Assert.NotNull(decision.PromptMessage);
        Assert.Contains("add 'Wingspan' to your collection", decision.PromptMessage!);
        Assert.Contains("confirm=true", decision.PromptMessage!);
    }

    [Fact]
    public void Evaluate_WithConfirm_Proceeds()
    {
        var decision = WriteConfirmationGate.Evaluate(confirm: true, action: "add 'Wingspan' to your collection");

        Assert.True(decision.Proceed);
        Assert.Null(decision.PromptMessage);
    }

    // --- Quota rule: an unconfirmed preview must NOT be counted as a quota-consuming write ---

    private static Dictionary<string, JsonElement> Args(params (string Key, object Value)[] pairs)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (key, value) in pairs)
        {
            dict[key] = JsonSerializer.SerializeToElement(value);
        }
        return dict;
    }

    [Fact]
    public void UnconfirmedWrite_InToolLevel_IsFlaggedAsNonQuota()
    {
        Assert.True(WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            WriteConfirmationMode.ToolLevel, "add_game_to_collection", arguments: null));

        Assert.True(WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            WriteConfirmationMode.ToolLevel, "rate_game", Args(("game", "Wingspan"))));
    }

    [Fact]
    public void ConfirmedWrite_IsNotFlagged()
    {
        Assert.False(WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            WriteConfirmationMode.ToolLevel, "add_game_to_collection", Args(("confirm", true))));
    }

    [Fact]
    public void NonWriteTool_IsNeverFlagged()
    {
        Assert.False(WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            WriteConfirmationMode.ToolLevel, "search", arguments: null));
    }

    [Fact]
    public void ProtocolMode_IsNeverFlagged()
    {
        // In Protocol mode the single call is the real action once confirmed — it counts normally.
        Assert.False(WriteConfirmationGate.IsUnconfirmedToolLevelWrite(
            WriteConfirmationMode.Protocol, "add_game_to_collection", arguments: null));
    }
}
