namespace WebCRM.Core;

/// <summary>Times are stored in UTC and shown in the user's time zone (default Europe/Athens).</summary>
public static class TimeDisplay
{
    public const string DefaultTimeZoneId = "Europe/Athens";

    public static DateTime ToLocal(DateTime utc, string? timeZoneId = null)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? DefaultTimeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }

    public static string Format(DateTime utc, string? timeZoneId = null) =>
        ToLocal(utc, timeZoneId).ToString("d MMM yyyy HH:mm");
}
