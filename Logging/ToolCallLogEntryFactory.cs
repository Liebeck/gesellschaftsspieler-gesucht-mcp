using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Gesellschaftsspieler.MCPServer.Enforcement;
using ModelContextProtocol.Protocol;

namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>What the filter saw of one call; everything the log row is built from.</summary>
public sealed record ToolCallObservation(
    string ToolName,
    IDictionary<string, JsonElement>? Arguments,
    CallToolResult? Result,
    Exception? Failure,
    bool Denied,
    McpCaller Caller,
    string? SessionId,
    string? ClientName,
    string? ClientVersion,
    DateTime CalledAt,
    int DurationMs);

/// <summary>Turns an observed call into a log row. Pure, so the rules are unit-tested.</summary>
public static class ToolCallLogEntryFactory
{
    public const string OutcomeSuccess = "Success";
    public const string OutcomeError = "Error";
    public const string OutcomeDenied = "Denied";

    private const int ErrorMessageMaxChars = 1000;

    // Keeps umlauts readable in SQL instead of ä escapes.
    private static readonly JsonSerializerOptions ArgumentJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static McpToolCallLog Create(ToolCallObservation observation, ToolCallLogOptions options)
    {
        var resultText = ExtractResultText(observation.Result);

        // Only the bounded columns are cut to their column size; ResultJson is always complete.
        return new McpToolCallLog
        {
            CalledAt = observation.CalledAt,
            ToolName = Truncate(observation.ToolName, 100) ?? "(unknown)",
            SessionId = Truncate(observation.SessionId, 100),
            UserId = Guid.TryParse(observation.Caller.Subject, out var userId) ? userId : null,
            CallerLevel = observation.Caller.Level.ToString(),
            ClientName = Truncate(observation.ClientName, 200),
            ClientVersion = Truncate(observation.ClientVersion, 50),
            Outcome = ResolveOutcome(observation),
            ErrorMessage = BuildErrorMessage(observation, resultText),
            DurationMs = observation.DurationMs,
            ArgumentsJson = options.LogArguments ? SerializeArguments(observation.Arguments) : null,
            ResultJson = options.LogResults ? resultText : null,
            ResultItemCount = CountItems(resultText),
        };
    }

    private static string ResolveOutcome(ToolCallObservation observation)
    {
        if (observation.Denied)
        {
            return OutcomeDenied;
        }

        if (observation.Failure is not null || observation.Result?.IsError == true)
        {
            return OutcomeError;
        }

        return OutcomeSuccess;
    }

    private static string? BuildErrorMessage(ToolCallObservation observation, string? resultText)
    {
        if (observation.Failure is not null)
        {
            return Truncate($"{observation.Failure.GetType().Name}: {observation.Failure.Message}", ErrorMessageMaxChars);
        }

        if (observation.Denied || observation.Result?.IsError == true)
        {
            return Truncate(resultText, ErrorMessageMaxChars);
        }

        return null;
    }

    private static string? SerializeArguments(IDictionary<string, JsonElement>? arguments)
        => arguments is null || arguments.Count == 0
            ? null
            : JsonSerializer.Serialize(arguments, ArgumentJsonOptions);

    /// <summary>
    /// Prefers the structured result; otherwise the text blocks, which is where the SDK puts the
    /// serialized DTOs our tools return.
    /// </summary>
    private static string? ExtractResultText(CallToolResult? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result.StructuredContent is not null)
        {
            return JsonSerializer.Serialize(result.StructuredContent, ArgumentJsonOptions);
        }

        if (result.Content is null || result.Content.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock textBlock)
            {
                text.Append(textBlock.Text);
            }
        }

        return text.Length > 0 ? text.ToString() : null;
    }

    /// <summary>
    /// Root array length, or the length of an "items" array (GameListResponseDto). Tells a working
    /// search apart from one that answers everything with an empty list.
    /// </summary>
    private static int? CountItems(string? resultText)
    {
        if (string.IsNullOrEmpty(resultText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(resultText);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (property.NameEquals("items") || property.NameEquals("Items"))
                    {
                        return property.Value.ValueKind == JsonValueKind.Array ? property.Value.GetArrayLength() : null;
                    }
                }
            }

            return null;
        }
        catch (JsonException)
        {
            // Plain sentences such as "No games found" are not a list.
            return null;
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
