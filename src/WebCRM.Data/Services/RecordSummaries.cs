using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>Turns stored (type, id) pairs into rows to show, keeping only records that exist and that the user may see.</summary>
internal static class RecordSummaries
{
    public static string EntityName(SearchEntity type) => type.ToString();

    public static bool TryParse(string entityName, out SearchEntity type) => Enum.TryParse(entityName, ignoreCase: false, out type)
        && Enum.IsDefined(type);

    public static async Task<Dictionary<(SearchEntity Type, int Id), SearchHit>> ResolveAsync(
        CrmDbContext db, UserContext user, IEnumerable<(SearchEntity Type, int Id)> references, CancellationToken cancellationToken)
    {
        var refs = references.Distinct().ToList();
        var result = new Dictionary<(SearchEntity, int), SearchHit>();

        var accountIds = refs.Where(r => r.Type == SearchEntity.Account).Select(r => r.Id).ToList();
        if (accountIds.Count > 0)
        {
            var accounts = await db.Accounts.AsNoTracking()
                .VisibleTo(user)
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    Industry = a.Industry != null ? a.Industry.Name : null,
                    City = a.Addresses.Where(ad => ad.AddressType == AddressType.Billing).Select(ad => ad.City).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            foreach (var a in accounts)
            {
                result[(SearchEntity.Account, a.Id)] = new SearchHit(SearchEntity.Account, a.Id, a.Name, Join(a.Industry, a.City));
            }
        }

        var contactIds = refs.Where(r => r.Type == SearchEntity.Contact).Select(r => r.Id).ToList();
        if (contactIds.Count > 0)
        {
            var contacts = await db.Contacts.AsNoTracking()
                .VisibleTo(user)
                .Where(c => contactIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName, AccountName = c.Account.Name, c.JobTitle })
                .ToListAsync(cancellationToken);
            foreach (var c in contacts)
            {
                result[(SearchEntity.Contact, c.Id)] = new SearchHit(SearchEntity.Contact, c.Id, c.FullName, Join(c.AccountName, c.JobTitle));
            }
        }

        return result;
    }

    private static string? Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }
}
