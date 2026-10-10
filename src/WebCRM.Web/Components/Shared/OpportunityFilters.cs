using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using WebCRM.Core.Opportunities;

namespace WebCRM.Web.Components.Shared;

/// <summary>
/// The filters of P14 (list) and P15 (board) as URL keys, and how they become service queries. The board shares the
/// keys it can use (scope, q, owner, from, to), so the toggle between the two keeps the filters.
/// </summary>
public static class OpportunityFilters
{
    public const string OutcomeKey = "outcome";
    public const string StageKey = "stage";
    public const string OwnerKey = "owner";
    public const string FromKey = "from";
    public const string ToKey = "to";

    public const string OpenValue = "open";
    public const string WonValue = "won";
    public const string LostValue = "lost";
    public const string AnyValue = "any";

    public static readonly string[] ListKeys = [OutcomeKey, StageKey, OwnerKey, FromKey, ToKey];

    /// <summary>The board has no outcome or stage filter: its columns are the stages.</summary>
    public static readonly string[] BoardKeys = [OwnerKey, FromKey, ToKey];

    public static OpportunityOutcome ParseOutcome(string? value) => value switch
    {
        WonValue => OpportunityOutcome.Won,
        LostValue => OpportunityOutcome.Lost,
        AnyValue => OpportunityOutcome.Any,
        _ => OpportunityOutcome.Open,
    };

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    public static string? FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static OpportunityQuery ToListQuery(ListState state)
    {
        var sort = state.Sort?.ToLowerInvariant() switch
        {
            "name" => OpportunitySort.Name,
            "account" => OpportunitySort.Account,
            "stage" => OpportunitySort.Stage,
            "amount" => OpportunitySort.Amount,
            "probability" => OpportunitySort.Probability,
            "weighted" => OpportunitySort.Weighted,
            "owner" => OpportunitySort.Owner,
            _ => OpportunitySort.CloseDate,
        };

        return new OpportunityQuery(
            Scope: state.Scope,
            Search: state.Search,
            StageId: state.GetInt(StageKey),
            Outcome: ParseOutcome(state.GetFilter(OutcomeKey)),
            OwnerId: state.GetFilter(OwnerKey),
            CloseFrom: ParseDate(state.GetFilter(FromKey)),
            CloseTo: ParseDate(state.GetFilter(ToKey)),
            Sort: sort,
            Descending: state.Descending,
            Page: state.Page);
    }

    public static BoardQuery ToBoardQuery(ListState state) => new(
        state.Scope,
        state.Search,
        state.GetFilter(OwnerKey),
        ParseDate(state.GetFilter(FromKey)),
        ParseDate(state.GetFilter(ToKey)));

    /// <summary>The same view on the other page: keeps scope, search, owner and the close date range.</summary>
    public static string SwitchTo(string path, string currentUri)
    {
        var query = QueryHelpers.ParseQuery(new Uri(currentUri).Query);
        var kept = new Dictionary<string, string?>();
        foreach (var key in new[] { ListState.ScopeKey, ListState.SearchKey, OwnerKey, FromKey, ToKey })
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                kept[key] = value.ToString();
            }
        }

        return QueryHelpers.AddQueryString(path, kept);
    }
}

/// <summary>EUR amounts as the lists show them. Currency is EUR only in the MVP (D9).</summary>
public static class Money
{
    public static string Format(decimal amount) => "€" + amount.ToString("N2", CultureInfo.InvariantCulture);
}
