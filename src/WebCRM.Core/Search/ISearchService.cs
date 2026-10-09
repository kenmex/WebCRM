using WebCRM.Core.Users;

namespace WebCRM.Core.Search;

public enum SearchEntity
{
    Account,
    Contact,
}

/// <summary>One match: enough to show a row and to open the record.</summary>
/// <param name="Title">The name.</param>
/// <param name="Subtitle">A second line, e.g. the account and job title of a contact.</param>
public sealed record SearchHit(SearchEntity Type, int Id, string Title, string? Subtitle);

/// <param name="Hits">At most the number asked for, best matches first.</param>
/// <param name="TotalCount">All matches, for "See all N in list".</param>
public sealed record SearchGroup(SearchEntity Type, IReadOnlyList<SearchHit> Hits, int TotalCount);

public sealed record SearchResults(string Query, IReadOnlyList<SearchGroup> Groups)
{
    public int TotalCount => Groups.Sum(g => g.TotalCount);

    public static SearchResults Empty(string query) => new(query, []);
}

public static class SearchRules
{
    /// <summary>Shorter text is not searched (it would match half the database).</summary>
    public const int MinLength = 2;

    public static string? Normalize(string? text)
    {
        var trimmed = text?.Trim();
        return trimmed is { Length: >= MinLength } ? trimmed : null;
    }
}

public interface ISearchService
{
    /// <summary>
    /// Global search over accounts (name, legal name, email, VAT / Tax ID, phone) and contacts (name, email, phone,
    /// mobile). Accent- and case-insensitive, only records the user may see. Fewer than <see cref="SearchRules.MinLength"/>
    /// characters gives no groups.
    /// </summary>
    Task<SearchResults> SearchAsync(
        string? text, int takePerGroup, UserContext user, CancellationToken cancellationToken = default);
}
