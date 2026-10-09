using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>A scriptable ISearchService: tests choose what each query returns, or hold a query open.</summary>
public sealed class FakeSearchService : ISearchService
{
    public List<(string Text, int Take)> Calls { get; } = [];

    public Func<string, int, Task<SearchResults>> OnSearch { get; set; } = (text, _) => Task.FromResult(SearchResults.Empty(text));

    public Task<SearchResults> SearchAsync(string? text, int takePerGroup, UserContext user, CancellationToken cancellationToken = default)
    {
        Calls.Add((text ?? string.Empty, takePerGroup));
        return OnSearch(text ?? string.Empty, takePerGroup);
    }

    public static SearchResults Results(string query, int accounts = 0, int contacts = 0, int accountTotal = -1, int contactTotal = -1) =>
        new(query,
        [
            new SearchGroup(
                SearchEntity.Account,
                [.. Enumerable.Range(1, accounts).Select(i => new SearchHit(SearchEntity.Account, i, $"Account {i}", i == 1 ? "Retail · Athens" : null))],
                accountTotal >= 0 ? accountTotal : accounts),
            new SearchGroup(
                SearchEntity.Contact,
                [.. Enumerable.Range(1, contacts).Select(i => new SearchHit(SearchEntity.Contact, 100 + i, $"Contact {i}", "Acme · Buyer"))],
                contactTotal >= 0 ? contactTotal : contacts),
        ]);
}
