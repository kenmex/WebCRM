using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

public sealed class FavouriteService(IDbContextFactory<CrmDbContext> factory, TimeProvider timeProvider) : IFavouriteService
{
    public async Task<bool> IsFavouriteAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var name = RecordSummaries.EntityName(type);

        return await db.Favourites.AsNoTracking()
            .AnyAsync(f => f.UserId == user.UserId && f.EntityName == name && f.EntityId == id, cancellationToken);
    }

    public async Task<bool> SetAsync(
        UserContext user, SearchEntity type, int id, bool favourite, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var name = RecordSummaries.EntityName(type);

        var existing = await db.Favourites
            .Where(f => f.UserId == user.UserId && f.EntityName == name && f.EntityId == id)
            .ToListAsync(cancellationToken);

        if (!favourite)
        {
            // Un-starring always works, even for a record that has since been deleted.
            db.Favourites.RemoveRange(existing);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (!(await RecordSummaries.ResolveAsync(db, user, [(type, id)], cancellationToken)).ContainsKey((type, id)))
        {
            return false;
        }

        if (existing.Count > 0)
        {
            return true;
        }

        db.Favourites.Add(new Favourite
        {
            UserId = user.UserId,
            EntityName = name,
            EntityId = id,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two quick clicks: the other one won. The unique index keeps it to a single row, and the star is on either way.
            await using var check = await factory.CreateDbContextAsync(cancellationToken);
            return await check.Favourites.AnyAsync(f => f.UserId == user.UserId && f.EntityName == name && f.EntityId == id, cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var rows = await db.Favourites
            .Where(f => f.UserId == user.UserId)
            .OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

        var known = rows.Where(r => RecordSummaries.TryParse(r.EntityName, out _)).ToList();
        var summaries = await RecordSummaries.ResolveAsync(
            db, user, known.Select(r => (Enum.Parse<SearchEntity>(r.EntityName), r.EntityId)), cancellationToken);

        var hits = new List<SearchHit>();
        var gone = new List<Favourite>();
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

        // Deleted records leave their stars behind; clear them out when we notice.
        if (gone.Count > 0)
        {
            db.Favourites.RemoveRange(gone);
            await db.SaveChangesAsync(cancellationToken);
        }

        return [.. hits.Take(Math.Clamp(take, 1, 100))];
    }
}
