using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

public sealed class SavedViewService(IDbContextFactory<CrmDbContext> factory) : ISavedViewService
{
    public async Task<SavedViewList> ListAsync(UserContext user, string listKey, CancellationToken cancellationToken = default)
    {
        if (!PersonalRules.IsListKey(listKey))
        {
            return SavedViewList.Empty;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var rows = await db.SavedViews.AsNoTracking()
            .Where(v => v.ListKey == listKey && (v.UserId == user.UserId || v.IsPublic))
            .Select(v => new
            {
                v.Id,
                v.Name,
                v.QueryString,
                v.IsPublic,
                v.UserId,
                OwnerName = db.Users.Where(u => u.Id == v.UserId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var mine = rows.Where(v => v.UserId == user.UserId).OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(v => new SavedViewItem(v.Id, v.Name, v.QueryString, v.IsPublic, v.UserId, v.OwnerName, true)).ToList();
        var shared = rows.Where(v => v.UserId != user.UserId).OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(v => new SavedViewItem(v.Id, v.Name, v.QueryString, v.IsPublic, v.UserId, v.OwnerName, false)).ToList();
        return new SavedViewList(mine, shared);
    }

    public async Task<SaveResult> SaveAsync(
        UserContext user, string listKey, string name, string queryString, bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();
        if (!PersonalRules.IsListKey(listKey))
        {
            errors[nameof(listKey)] = "This list cannot have saved views.";
        }

        var cleanName = PersonalRules.NormalizeViewName(name);
        if (cleanName is null)
        {
            errors["Name"] = $"Give the view a name of up to {PersonalRules.MaxViewNameLength} characters.";
        }

        var cleanQuery = PersonalRules.NormalizeQueryString(queryString);
        if (cleanQuery is null)
        {
            errors["QueryString"] = "These filters cannot be saved.";
        }

        if (errors.Count > 0)
        {
            return new SaveResult(SaveStatus.Invalid, FieldErrors: errors);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        // At most 30 per list, so a user's own views are few enough to compare names here (and the same on every collation).
        var mine = await db.SavedViews.Where(v => v.UserId == user.UserId && v.ListKey == listKey).ToListAsync(cancellationToken);
        var sameName = mine.FirstOrDefault(v => string.Equals(v.Name, cleanName, StringComparison.CurrentCultureIgnoreCase));

        if (sameName is not null && !replaceExisting)
        {
            return Invalid("Name", "You already have a view with that name.");
        }

        if (sameName is null && mine.Count >= PersonalRules.MaxSavedViewsPerList)
        {
            return Invalid("Name", $"You can keep up to {PersonalRules.MaxSavedViewsPerList} views for this list. Delete one first.");
        }

        var view = sameName ?? new SavedView { UserId = user.UserId, ListKey = listKey, Name = cleanName! };
        view.QueryString = cleanQuery!;
        if (sameName is null)
        {
            db.SavedViews.Add(view);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Saved twice at once under the same name: the unique index refused the second one.
            return Invalid("Name", "You already have a view with that name.");
        }

        return new SaveResult(SaveStatus.Saved, view.Id);
    }

    public async Task<SaveResult> RenameAsync(UserContext user, int id, string name, CancellationToken cancellationToken = default)
    {
        var cleanName = PersonalRules.NormalizeViewName(name);
        if (cleanName is null)
        {
            return Invalid("Name", $"Give the view a name of up to {PersonalRules.MaxViewNameLength} characters.");
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == user.UserId, cancellationToken);
        if (view is null)
        {
            return new SaveResult(SaveStatus.NotFound);
        }

        var others = await db.SavedViews.Where(v => v.UserId == user.UserId && v.ListKey == view.ListKey && v.Id != id)
            .Select(v => v.Name).ToListAsync(cancellationToken);
        if (others.Any(n => string.Equals(n, cleanName, StringComparison.CurrentCultureIgnoreCase)))
        {
            return Invalid("Name", "You already have a view with that name.");
        }

        view.Name = cleanName;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SaveResult(SaveStatus.NotFound); // deleted while renaming
        }
        catch (DbUpdateException)
        {
            return Invalid("Name", "You already have a view with that name.");
        }

        return new SaveResult(SaveStatus.Saved, id);
    }

    public async Task<bool> DeleteAsync(UserContext user, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        // Your own, or (as an Admin) one that was published to everyone.
        if (view is null || (view.UserId != user.UserId && !(user.IsAdmin && view.IsPublic)))
        {
            return false;
        }

        db.SavedViews.Remove(view);
        await PersonalSave.SaveTolerantAsync(db, cancellationToken);
        return true;
    }

    public async Task<bool> SetPublicAsync(UserContext user, int id, bool isPublic, CancellationToken cancellationToken = default)
    {
        if (!user.IsAdmin)
        {
            return false;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == user.UserId, cancellationToken);
        if (view is null)
        {
            return false;
        }

        view.IsPublic = isPublic;
        await PersonalSave.SaveTolerantAsync(db, cancellationToken);
        return true;
    }

    private static SaveResult Invalid(string field, string message) =>
        new(SaveStatus.Invalid, FieldErrors: new Dictionary<string, string> { [field] = message });
}
