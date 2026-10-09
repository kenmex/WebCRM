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

    /// <summary>"today", "yesterday", "3 days ago", "in 2 weeks": by calendar day in the user's time zone.</summary>
    public static string Relative(DateTime utc, DateTime nowUtc, string? timeZoneId = null)
    {
        var days = (ToLocal(utc, timeZoneId).Date - ToLocal(nowUtc, timeZoneId).Date).Days;
        return days switch
        {
            0 => "today",
            -1 => "yesterday",
            1 => "tomorrow",
            < 0 => Ago(-days),
            _ => "in " + Span(days),
        };

        static string Ago(int past) => Span(past) + " ago";
    }

    private static string Span(int days) => days switch
    {
        < 14 => $"{days} days",
        < 60 => $"{days / 7} weeks",
        < 730 => $"{days / 30} months",
        _ => $"{days / 365} years",
    };
}
