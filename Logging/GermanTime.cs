namespace Gesellschaftsspieler.MCPServer.Logging;

/// <summary>
/// Log timestamps are German local time so they can be read in SQL without converting.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="DateTime.Now"/>: App Service runs on UTC, so the same column
/// would mean UTC in production and local time on a developer machine.
/// </remarks>
public static class GermanTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now() => FromUtc(DateTime.UtcNow);

    public static DateTime FromUtc(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    private static TimeZoneInfo ResolveZone()
    {
        // .NET resolves the IANA id on Windows (via ICU) and Linux alike.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
