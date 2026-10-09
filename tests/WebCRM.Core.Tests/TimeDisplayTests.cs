using Shouldly;

namespace WebCRM.Core.Tests;

public class TimeDisplayTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToLocal_converts_utc_to_Athens_with_daylight_saving()
    {
        // Athens is UTC+3 in October 2026 (EEST) and UTC+2 in January (EET).
        TimeDisplay.ToLocal(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)).Hour.ShouldBe(15);
        TimeDisplay.ToLocal(new DateTime(2026, 1, 9, 12, 0, 0, DateTimeKind.Utc)).Hour.ShouldBe(14);
    }

    [Theory]
    [InlineData("2026-10-09T01:00:00", "today")]
    [InlineData("2026-10-08T12:00:00", "yesterday")]
    [InlineData("2026-10-10T12:00:00", "tomorrow")]
    [InlineData("2026-10-04T12:00:00", "5 days ago")]
    [InlineData("2026-09-18T12:00:00", "3 weeks ago")]
    [InlineData("2026-06-09T12:00:00", "4 months ago")]
    [InlineData("2026-10-16T12:00:00", "in 7 days")]
    [InlineData("2024-10-09T12:00:00", "2 years ago")]
    public void Relative_counts_calendar_days_in_the_local_time_zone(string utc, string expected)
    {
        var value = DateTime.SpecifyKind(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

        TimeDisplay.Relative(value, Now).ShouldBe(expected);
    }

    [Fact]
    public void Relative_uses_the_local_date_not_the_utc_date()
    {
        // 22:30 UTC on 8 Oct is already 9 Oct in Athens, so it is "today" at noon UTC on 9 Oct.
        TimeDisplay.Relative(new DateTime(2026, 10, 8, 22, 30, 0, DateTimeKind.Utc), Now).ShouldBe("today");
    }
}
