using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using WebCRM.Core.Querying;

namespace WebCRM.Web.Components.Shared;

/// <summary>
/// Everything a list page remembers: scope, quick filter text, typed filters, sort and page.
/// The URL query string is the source of truth (<c>?scope=team&amp;status=2&amp;page=3</c>), so Back,
/// refresh and shared links restore the view. Only values that differ from the defaults are written.
/// </summary>
public sealed class ListState
{
    public const string ScopeKey = "scope";
    public const string SearchKey = "q";
    public const string PageKey = "page";
    public const string SortKey = "sort";
    public const string DescendingKey = "desc";

    public ListScope Scope { get; set; }

    public string? Search { get; set; }

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    public string? Sort { get; set; }

    public bool Descending { get; set; }

    public Dictionary<string, string> Filters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetFilter(string key) => Filters.GetValueOrDefault(key);

    public int? GetInt(string key) =>
        int.TryParse(GetFilter(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    public void SetFilter(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Filters.Remove(key);
        }
        else
        {
            Filters[key] = value;
        }
    }

    /// <summary>True when the quick filter or any typed filter is set (scope does not count).</summary>
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || Filters.Count > 0;

    /// <summary>Applies a change and returns this state, for <c>state.Clone().With(s => ...)</c>.</summary>
    public ListState With(Action<ListState> change)
    {
        change(this);
        return this;
    }

    public ListState Clone()
    {
        var copy = new ListState
        {
            Scope = Scope,
            Search = Search,
            Page = Page,
            Sort = Sort,
            Descending = Descending,
        };
        foreach (var (key, value) in Filters)
        {
            copy.Filters[key] = value;
        }

        return copy;
    }

    /// <summary>Reads the state from a URL. Unknown or invalid values fall back to the defaults.</summary>
    public static ListState FromUri(
        string uri, IReadOnlyList<ListScope> scopes, ListScope defaultScope, IEnumerable<string> filterKeys)
    {
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        var state = new ListState { Scope = defaultScope };

        if (query.TryGetValue(ScopeKey, out var scope)
            && Enum.TryParse<ListScope>(scope.ToString(), ignoreCase: true, out var parsed)
            && scopes.Contains(parsed))
        {
            state.Scope = parsed;
        }

        if (query.TryGetValue(SearchKey, out var search) && !string.IsNullOrWhiteSpace(search))
        {
            state.Search = search.ToString();
        }

        if (query.TryGetValue(PageKey, out var page)
            && int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number >= 1)
        {
            state.Page = number;
        }

        if (query.TryGetValue(SortKey, out var sort) && !string.IsNullOrWhiteSpace(sort))
        {
            state.Sort = sort.ToString();
        }

        state.Descending = query.TryGetValue(DescendingKey, out var desc) && desc == "1";

        foreach (var key in filterKeys)
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                state.Filters[key] = value.ToString();
            }
        }

        return state;
    }

    /// <summary>The query-string parameters for this state; null removes a parameter from the URL.</summary>
    public Dictionary<string, object?> ToQueryParameters(ListScope defaultScope, IEnumerable<string> filterKeys)
    {
        var parameters = new Dictionary<string, object?>
        {
            [ScopeKey] = Scope == defaultScope ? null : Scope.ToString().ToLowerInvariant(),
            [SearchKey] = string.IsNullOrWhiteSpace(Search) ? null : Search,
            [PageKey] = Page > 1 ? Page : null,
            [SortKey] = string.IsNullOrEmpty(Sort) ? null : Sort,
            [DescendingKey] = Descending && !string.IsNullOrEmpty(Sort) ? "1" : null,
        };

        foreach (var key in filterKeys)
        {
            parameters[key] = GetFilter(key);
        }

        return parameters;
    }
}
