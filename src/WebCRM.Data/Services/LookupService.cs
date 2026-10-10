using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using WebCRM.Core.Lookups;

namespace WebCRM.Data.Services;

public sealed class LookupService(IDbContextFactory<CrmDbContext> factory) : ILookupService
{
    public async Task<IReadOnlyList<LookupOption>> GetOptionsAsync(
        LookupKind kind, int? currentId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return kind switch
        {
            LookupKind.Industry => await QueryAsync<Industry>(db, currentId, cancellationToken),
            LookupKind.AccountStatus => await QueryAsync<AccountStatus>(db, currentId, cancellationToken),
            LookupKind.LeadSource => await QueryAsync<LeadSource>(db, currentId, cancellationToken),
            LookupKind.LeadStatus => await QueryAsync<LeadStatus>(db, currentId, cancellationToken),
            LookupKind.ActivityType => await QueryAsync<ActivityType>(db, currentId, cancellationToken),
            LookupKind.LostReason => await QueryAsync<LostReason>(db, currentId, cancellationToken),
            LookupKind.Salutation => await QueryAsync<Salutation>(db, currentId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static async Task<IReadOnlyList<LookupOption>> QueryAsync<T>(
        CrmDbContext db, int? currentId, CancellationToken cancellationToken)
        where T : Lookup =>
        await db.Set<T>()
            .AsNoTracking()
            .Where(l => l.IsActive || l.Id == currentId)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new LookupOption(l.Id, l.Name, l.IsActive, l.SystemCode))
            .ToListAsync(cancellationToken);
}
