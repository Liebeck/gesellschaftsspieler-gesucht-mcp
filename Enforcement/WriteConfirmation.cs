using System.Text.Json;

namespace Gesellschaftsspieler.MCPServer.Enforcement;

public enum WriteConfirmationMode
{
    /// <summary>Two-call confirm parameter — works with every client and SDK. Default.</summary>
    ToolLevel,

    /// <summary>Protocol-level confirmation (Elicitation on 2025 SDK / MRTR on 2.0).</summary>
    Protocol
}

/// <summary>Bound from configuration section "Mcp" (only the WriteConfirmation key is read).</summary>
public class WriteConfirmationOptions
{
    public WriteConfirmationMode WriteConfirmation { get; set; } = WriteConfirmationMode.ToolLevel;
}

public readonly record struct ConfirmationDecision(bool Proceed, string? PromptMessage)
{
    public static ConfirmationDecision Approved() => new(true, null);

    public static ConfirmationDecision Required(string message) => new(false, message);
}

/// <summary>
/// Pure logic for the tool-level write confirmation (Phase 1). No SDK, no client dependency —
/// this is the guaranteed fallback: call once for a spoken prompt, call again with confirm=true
/// to write.
/// </summary>
public static class WriteConfirmationGate
{
    public const string ConfirmArgument = "confirm";

    /// <summary>Tools that require confirmation before writing.</summary>
    public static readonly IReadOnlySet<string> WriteTools =
        new HashSet<string>(StringComparer.Ordinal) { "add_game_to_collection", "rate_game" };

    public static ConfirmationDecision Evaluate(bool confirm, string action)
        => confirm
            ? ConfirmationDecision.Approved()
            : ConfirmationDecision.Required(
                $"Confirmation required: {action}? Call this tool again with confirm=true to proceed.");

    /// <summary>
    /// True when this is an unconfirmed first call to a write tool in ToolLevel mode — such a call
    /// is a preview, so it must NOT consume the daily quota (it still consumes a rate-limit permit).
    /// </summary>
    public static bool IsUnconfirmedToolLevelWrite(
        WriteConfirmationMode mode, string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (mode != WriteConfirmationMode.ToolLevel || !WriteTools.Contains(tool))
        {
            return false;
        }

        return !IsConfirmTrue(arguments);
    }

    private static bool IsConfirmTrue(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue(ConfirmArgument, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True
            || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }
}
