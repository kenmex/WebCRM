namespace WebCRM.Core.Opportunities;

/// <summary>
/// Defaults of a new opportunity (docs/data-dictionary.md, Opportunity). Lead conversion uses them now; the
/// Opportunities pages will too. The first open stage is read from the Stage table, not decided here.
/// </summary>
public static class OpportunityDefaults
{
    public const int CloseDateDays = 30;

    /// <summary>Today in Europe/Athens plus 30 days.</summary>
    public static DateOnly CloseDate(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeDisplay.ToLocal(now.UtcDateTime)).AddDays(CloseDateDays);
}
