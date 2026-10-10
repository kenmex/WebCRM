using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Leads;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>
/// Global search (top bar and P7). Names, email and legal name carry the accent-insensitive collation, so
/// "Αθηνα" finds "Αθήνα" and case does not matter. Matches that start with the text rank first.
/// </summary>
public sealed class SearchService(IDbContextFactory<CrmDbContext> factory) : ISearchService
{
    public async Task<SearchResults> SearchAsync(
        string? text, int takePerGroup, UserContext user, CancellationToken cancellationToken = default)
    {
        var term = SearchRules.Normalize(text);
        if (term is null)
        {
            return SearchResults.Empty(text?.Trim() ?? string.Empty);
        }

        var take = Math.Clamp(takePerGroup, 1, 50);

        // Each type runs on its own context at the same time (a context runs one query at a time).
        var accounts = SearchAccountsAsync(term, take, user, cancellationToken);
        var contacts = SearchContactsAsync(term, take, user, cancellationToken);
        var opportunities = SearchOpportunitiesAsync(term, take, user, cancellationToken);
        var leads = SearchLeadsAsync(term, take, user, cancellationToken);
        await Task.WhenAll(accounts, contacts, opportunities, leads);

        return new SearchResults(term, [accounts.Result, contacts.Result, opportunities.Result, leads.Result]);
    }

    private async Task<SearchGroup> SearchAccountsAsync(
        string term, int take, UserContext user, CancellationToken cancellationToken)
    {
        // The VAT / Tax ID is stored normalised, so the text is normalised the same way before it is compared.
        var vat = VatNumberRules.Normalize(term);
        var vatTerm = vat is { Length: >= VatNumberRules.MinLength } ? vat : null;

        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        IQueryable<Core.Entities.Account> Matches(CrmDbContext db) => db.Accounts.AsNoTracking()
            .VisibleTo(user)
            .Where(a =>
                a.Name.Contains(term)
                || (a.LegalName != null && a.LegalName.Contains(term))
                || (a.Email != null && a.Email.Contains(term))
                || (a.Phone != null && a.Phone.Contains(term))
                || (vatTerm != null && a.VatNumber != null && a.VatNumber.Contains(vatTerm)));

        var countTask = Matches(countDb).CountAsync(cancellationToken);
        var rowsTask = Matches(pageDb)
            .OrderBy(a => a.Name.StartsWith(term) ? 0 : 1).ThenBy(a => a.Name).ThenBy(a => a.Id)
            .Take(take)
            .Select(a => new
            {
                a.Id,
                a.Name,
                Industry = a.Industry != null ? a.Industry.Name : null,
                City = a.Addresses.Where(ad => ad.AddressType == Core.Entities.AddressType.Billing).Select(ad => ad.City).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        await Task.WhenAll(countTask, rowsTask);

        var hits = rowsTask.Result
            .Select(r => new SearchHit(SearchEntity.Account, r.Id, r.Name, Join(r.Industry, r.City)))
            .ToList();
        return new SearchGroup(SearchEntity.Account, hits, countTask.Result);
    }

    private async Task<SearchGroup> SearchContactsAsync(
        string term, int take, UserContext user, CancellationToken cancellationToken)
    {
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        IQueryable<Core.Entities.Contact> Matches(CrmDbContext db) => db.Contacts.AsNoTracking()
            .VisibleTo(user)
            .Where(c =>
                c.FullName.Contains(term)
                || (c.Email != null && c.Email.Contains(term))
                || (c.Phone != null && c.Phone.Contains(term))
                || (c.Mobile != null && c.Mobile.Contains(term)));

        var countTask = Matches(countDb).CountAsync(cancellationToken);
        var rowsTask = Matches(pageDb)
            .OrderBy(c => c.FullName.StartsWith(term) ? 0 : 1).ThenBy(c => c.FullName).ThenBy(c => c.Id)
            .Take(take)
            .Select(c => new { c.Id, c.FullName, AccountName = c.Account.Name, c.JobTitle })
            .ToListAsync(cancellationToken);
        await Task.WhenAll(countTask, rowsTask);

        var hits = rowsTask.Result
            .Select(r => new SearchHit(SearchEntity.Contact, r.Id, r.FullName, Join(r.AccountName, r.JobTitle)))
            .ToList();
        return new SearchGroup(SearchEntity.Contact, hits, countTask.Result);
    }

    private async Task<SearchGroup> SearchLeadsAsync(
        string term, int take, UserContext user, CancellationToken cancellationToken)
    {
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        // Converted and Disqualified leads are found too (they stay queryable for source reporting); the status in
        // the subtitle tells them apart.
        IQueryable<Core.Entities.Lead> Matches(CrmDbContext db) => db.Leads.AsNoTracking()
            .VisibleTo(user)
            .Where(l =>
                l.Name.Contains(term)
                || (l.Company != null && l.Company.Contains(term))
                || (l.Email != null && l.Email.Contains(term))
                || (l.Phone != null && l.Phone.Contains(term)));

        var countTask = Matches(countDb).CountAsync(cancellationToken);
        var rowsTask = Matches(pageDb)
            .OrderBy(l => l.Name.StartsWith(term) ? 0 : 1).ThenBy(l => l.Name).ThenBy(l => l.Id)
            .Take(take)
            .Select(l => new { l.Id, l.Name, l.Company, Status = l.LeadStatus.Name })
            .ToListAsync(cancellationToken);
        await Task.WhenAll(countTask, rowsTask);

        var hits = rowsTask.Result
            .Select(r => new SearchHit(SearchEntity.Lead, r.Id, r.Name, Join(r.Company, r.Status)))
            .ToList();
        return new SearchGroup(SearchEntity.Lead, hits, countTask.Result);
    }

    private async Task<SearchGroup> SearchOpportunitiesAsync(
        string term, int take, UserContext user, CancellationToken cancellationToken)
    {
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        // Won and Lost opportunities are found too; the stage in the subtitle tells them apart.
        IQueryable<Core.Entities.Opportunity> Matches(CrmDbContext db) => db.Opportunities.AsNoTracking()
            .VisibleTo(user)
            .Where(o => o.Name.Contains(term) || o.Account.Name.Contains(term));

        var countTask = Matches(countDb).CountAsync(cancellationToken);
        var rowsTask = Matches(pageDb)
            .OrderBy(o => o.Name.StartsWith(term) ? 0 : 1).ThenBy(o => o.Name).ThenBy(o => o.Id)
            .Take(take)
            .Select(o => new { o.Id, o.Name, AccountName = o.Account.Name, Stage = o.Stage.Name })
            .ToListAsync(cancellationToken);
        await Task.WhenAll(countTask, rowsTask);

        var hits = rowsTask.Result
            .Select(r => new SearchHit(SearchEntity.Opportunity, r.Id, r.Name, Join(r.AccountName, r.Stage)))
            .ToList();
        return new SearchGroup(SearchEntity.Opportunity, hits, countTask.Result);
    }

    private static string? Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }
}
