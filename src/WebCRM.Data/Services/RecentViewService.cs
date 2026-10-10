using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

public sealed class RecentViewService(IDbContextFactory<CrmDbContext> factory, TimeProvider timeProvider) : IRecentViewService
{
    public async Task RecordAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var name = RecordSummaries.EntityName(type);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var existing = await db.RecentViews
            .FirstOrDefaultAsync(v => v.UserId == user.UserId && v.EntityName == name && v.EntityId == id, cancellationToken);

        if (existing is not null)
        {
            // Opening the same record again straight away is not worth a write.
            if (now - existing.ViewedAt >= PersonalRules.RecentViewRefresh)
            {
                existing.ViewedAt = now;
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Only records the user can see are remembered.
        if (!(await RecordSummaries.ResolveAsync(db, user, [(type, id)], cancellationToken)).ContainsKey((type, id)))
        {
            return;
        }

        db.RecentViews.Add(new RecentView { UserId = user.UserId, EntityName = name, EntityId = id, ViewedAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same record was opened in two tabs at once and the other write got there first: nothing more to do.
            return;
        }

        await TrimAsync(user.UserId, cancellationToken);
    }

    /// <summary>Keeps the newest <see cref="PersonalRules.MaxRecentViews"/> rows of the user.</summary>
    private async Task TrimAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var surplus = await db.RecentViews
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.ViewedAt).ThenByDescending(v => v.Id)
            .Skip(PersonalRules.MaxRecentViews)
            .ToListAsync(cancellationToken);

        if (surplus.Count > 0)
        {
            db.RecentViews.RemoveRange(surplus);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var rows = await db.RecentViews
            .Where(v => v.UserId == user.UserId)
            .OrderByDescending(v => v.ViewedAt).ThenByDescending(v => v.Id)
            .ToListAsync(cancellationToken);

        var known = rows.Where(r => RecordSummaries.TryParse(r.EntityName, out _)).ToList();
        var summaries = await RecordSummaries.ResolveAsync(
            db, user, known.Select(r => (Enum.Parse<SearchEntity>(r.EntityName), r.EntityId)), cancellationToken);

        var hits = new List<SearchHit>();
        var gone = new List<RecentView>();
        foreach (var row in known)
        {
            if (summaries.TryGetValue((Enum.Parse<SearchEntity>(row.EntityName), row.EntityId), out var hit))
            {
                hits.Add(hit);
            }
            else
            {
                gone.Add(row);
            }
        }

        if (gone.Count > 0)
        {
            db.RecentViews.RemoveRange(gone);
            await db.SaveChangesAsync(cancellationToken);
        }

        return [.. hits.Take(Math.Clamp(take, 1, 100))];
    }
}
