using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebCRM.Core.Entities;
using WebCRM.Core.Interfaces;

namespace WebCRM.Data.Interceptors;

/// <summary>
/// Fills CreatedAt/CreatedBy on insert and UpdatedAt/UpdatedBy on update, so no service
/// or component sets them by hand. Created* can never be changed after insert.
/// </summary>
/// <remarks>
/// Async only: the current user comes from the Blazor authentication state, which is
/// asynchronous. A synchronous SaveChanges() with audited changes throws.
/// </remarks>
public sealed class AuditFieldsInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context && GetAuditedEntries(context).Count > 0)
        {
            throw new InvalidOperationException(
                "Audited entities must be saved with SaveChangesAsync, because the current user is resolved asynchronously.");
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context)
        {
            return result;
        }

        var entries = GetAuditedEntries(context);
        if (entries.Count == 0)
        {
            // Nothing audited (e.g. Identity saving AspNetUsers): no user needed.
            return result;
        }

        var userId = await currentUser.GetUserIdAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Cannot save audited entities without a signed-in user (CreatedBy/UpdatedBy are required).");

        // datetime2(0) stores whole seconds; truncate so the tracked value matches the stored one.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is ICreationAudited)
                {
                    entry.Property(nameof(ICreationAudited.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(ICreationAudited.CreatedBy)).CurrentValue = userId;
                }
                else
                {
                    // Tables with only Updated* columns (CompanySetting) record their first save there.
                    SetUpdated(entry, userId, now);
                }
            }
            else
            {
                if (entry.Entity is ICreationAudited)
                {
                    KeepOriginal(entry.Property(nameof(ICreationAudited.CreatedAt)));
                    KeepOriginal(entry.Property(nameof(ICreationAudited.CreatedBy)));
                }

                SetUpdated(entry, userId, now);
            }
        }

        return result;
    }

    private static List<EntityEntry> GetAuditedEntries(DbContext context) =>
        context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified
                && e.Entity is ICreationAudited or IModificationAudited)
            .ToList();

    private static void SetUpdated(EntityEntry entry, string userId, DateTime now)
    {
        if (entry.Entity is IModificationAudited)
        {
            entry.Property(nameof(IModificationAudited.UpdatedAt)).CurrentValue = now;
            entry.Property(nameof(IModificationAudited.UpdatedBy)).CurrentValue = userId;
        }
    }

    private static void KeepOriginal(PropertyEntry property)
    {
        property.CurrentValue = property.OriginalValue;
        property.IsModified = false;
    }
}
