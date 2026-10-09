using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>
/// Contact rules and queries (P10 list, P11 header, the Contacts tab on P9). Every read goes through
/// <see cref="ContactAccess.VisibleTo"/>; soft-deleted rows are hidden by the global query filter.
/// </summary>
public sealed class ContactService(
    IDbContextFactory<CrmDbContext> factory, IOwnerService owners, TimeProvider timeProvider) : IContactService
{
    private const int MaxWarnings = 5;

    // ---- List (P10) ----

    public async Task<PagedResult<ContactListItem>> SearchAsync(
        ContactQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Count and page run together, each on its own context (a context runs one query at a time).
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        var countTask = Filter(countDb, query, user).CountAsync(cancellationToken);

        // Sort and page the contacts first, then project (EF cannot translate OrderBy on members of the
        // constructor-projected DTO, and this way only one page of rows computes the subqueries).
        var itemsTask = Project(pageDb, Sort(pageDb, Filter(pageDb, query, user), query)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return new PagedResult<ContactListItem>(itemsTask.Result, countTask.Result);
    }

    private static IQueryable<Contact> Filter(CrmDbContext db, ContactQuery query, UserContext user)
    {
        var contacts = db.Contacts.AsNoTracking().VisibleTo(user);

        var userId = user.UserId;
        contacts = query.Scope switch
        {
            ListScope.Mine => contacts.Where(c => c.OwnerId == userId),
            ListScope.Team when user.TeamId is { } teamId =>
                contacts.Where(c => db.Users.Any(u => u.Id == c.OwnerId && u.TeamId == teamId)),
            ListScope.Team => contacts.Where(c => false),
            _ => contacts,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // FullName, Email and Account.Name have the accent-insensitive collation, so LIKE ignores case and accents.
            var search = query.Search.Trim();
            contacts = contacts.Where(c =>
                c.FullName.Contains(search)
                || (c.Email != null && c.Email.Contains(search))
                || (c.JobTitle != null && c.JobTitle.Contains(search))
                || (c.Phone != null && c.Phone.Contains(search))
                || (c.Mobile != null && c.Mobile.Contains(search))
                || c.Account.Name.Contains(search));
        }

        if (query.AccountId is { } accountId)
        {
            contacts = contacts.Where(c => c.AccountId == accountId);
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;
            contacts = contacts.Where(c => c.OwnerId == ownerId);
        }

        if (query.HasEmail is { } hasEmail)
        {
            contacts = hasEmail
                ? contacts.Where(c => c.Email != null && c.Email != string.Empty)
                : contacts.Where(c => c.Email == null || c.Email == string.Empty);
        }

        if (query.DoNotContact is { } doNotContact)
        {
            contacts = contacts.Where(c => c.DoNotContact == doNotContact);
        }

        return contacts;
    }

    private static IQueryable<ContactListItem> Project(CrmDbContext db, IQueryable<Contact> contacts) =>
        contacts.Select(c => new ContactListItem(
            c.Id,
            c.FullName,
            c.AccountId,
            c.Account.Name,
            c.JobTitle,
            c.Email,
            c.Phone,
            c.Mobile,
            c.OwnerId,
            db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
            db.Activities.Where(x => x.ContactId == c.Id && x.DoneAt != null).Max(x => x.DoneAt),
            c.DoNotContact));

    private static IQueryable<Contact> Sort(CrmDbContext db, IQueryable<Contact> contacts, ContactQuery query)
    {
        var desc = query.Descending;
        var ordered = query.Sort switch
        {
            ContactSort.Account => OrderBy(contacts, c => c.Account.Name, desc),
            ContactSort.JobTitle => OrderBy(contacts, c => c.JobTitle, desc),
            ContactSort.Email => OrderBy(contacts, c => c.Email, desc),
            ContactSort.Owner => OrderBy(
                contacts, c => db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), desc),
            ContactSort.LastActivity => OrderBy(
                contacts,
                c => db.Activities.Where(x => x.ContactId == c.Id && x.DoneAt != null).Max(x => x.DoneAt),
                desc),
            _ => OrderBy(contacts, c => c.FullName, desc),
        };

        // Id as the tie-breaker keeps paging stable.
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<Contact> OrderBy<TKey>(
        IQueryable<Contact> contacts, Expression<Func<Contact, TKey>> key, bool descending) =>
        descending ? contacts.OrderByDescending(key) : contacts.OrderBy(key);

    // ---- Detail (P11) ----

    public async Task<ContactDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Contacts.AsNoTracking()
            .VisibleTo(user)
            .Where(c => c.Id == id)
            .Select(c => new ContactDetail(
                c.Id,
                c.FirstName,
                c.LastName,
                c.FullName,
                c.AccountId,
                c.Account.Name,
                c.JobTitle,
                c.Email,
                c.Phone,
                c.Mobile,
                c.OwnerId,
                db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
                c.CreatedAt,
                c.UpdatedAt,
                c.RowVersion,
                c.Department,
                c.SalutationId,
                c.Salutation != null ? c.Salutation.Name : null,
                c.DoNotContact,
                c.DoNotContactSince))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ContactEditModel> NewAsync(
        UserContext user, int? accountId = null, CancellationToken cancellationToken = default)
    {
        var accountOwner = accountId is { } id ? await GetDefaultOwnerAsync(id, user, cancellationToken) : null;

        // An account the user cannot see is not prefilled.
        return new ContactEditModel
        {
            AccountId = accountOwner is null ? null : accountId,
            OwnerId = accountOwner ?? user.UserId,
        };
    }

    public async Task<string?> GetDefaultOwnerAsync(
        int accountId, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Accounts.AsNoTracking()
            .VisibleTo(user)
            .Where(a => a.Id == accountId)
            .Select(a => a.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ---- Save ----

    public async Task<SaveResult> SaveAsync(
        ContactEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SaveOptions();
        var errors = new Dictionary<string, string>();

        // Normalise first, so validation and the duplicate check see what will be stored.
        model.FirstName = Blank(model.FirstName);
        model.LastName = model.LastName?.Trim() ?? string.Empty;
        model.JobTitle = Blank(model.JobTitle);
        model.Department = Blank(model.Department);
        model.Email = ContactRules.NormalizeEmail(model.Email);
        model.Phone = Blank(model.Phone);
        model.Mobile = Blank(model.Mobile);

        // The server validates again; the form's checks are only for the user's convenience.
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                errors.TryAdd(member, result.ErrorMessage ?? "Invalid value.");
            }
        }

        if (model.LastName.Length == 0)
        {
            errors.TryAdd(nameof(ContactEditModel.LastName), "Last name is required.");
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var isNew = model.Id == 0;
        Contact? entity = null;
        if (!isNew)
        {
            entity = await db.Contacts.VisibleTo(user).FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken);
            if (entity is null)
            {
                return new SaveResult(SaveStatus.NotFound);
            }
        }

        var accountId = model.AccountId!.Value;
        var accountOwnerId = await db.Accounts.AsNoTracking()
            .VisibleTo(user)
            .Where(a => a.Id == accountId)
            .Select(a => a.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountOwnerId is null)
        {
            errors[nameof(ContactEditModel.AccountId)] = "Choose an existing account.";
        }

        // Who may own it: the usual rule (Sales: themselves, Manager: their team, Admin: anyone active, plus the
        // current owner on edit). On create the account's owner is always allowed, so a contact can start with
        // the owner the spec asks for even when the creator could not assign to that person.
        var allowed = (await owners.GetAssignableAsync(user, entity?.OwnerId, cancellationToken)).Select(o => o.Id).ToHashSet();
        if (isNew && accountOwnerId is not null)
        {
            allowed.Add(accountOwnerId);
        }

        if (!allowed.Contains(model.OwnerId!))
        {
            errors[nameof(ContactEditModel.OwnerId)] = "You cannot assign this owner.";
        }

        // A deactivated salutation may stay on a contact that already has it, but cannot be newly chosen.
        var currentSalutationId = entity?.SalutationId;
        if (model.SalutationId is { } salutationId
            && !await db.Salutations.AnyAsync(
                s => s.Id == salutationId && (s.IsActive || s.Id == currentSalutationId), cancellationToken))
        {
            errors[nameof(ContactEditModel.SalutationId)] = "Choose a valid salutation.";
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        // Warn, don't block: the same email on another active contact.
        var email = model.Email;
        var emailChanged = isNew || !string.Equals(entity!.Email, email, StringComparison.OrdinalIgnoreCase);
        if (email is not null && emailChanged && !options.AcceptWarnings)
        {
            var duplicates = await FindDuplicateEmailsAsync(db, model.Id, email, cancellationToken);
            if (duplicates.Count > 0)
            {
                return new SaveResult(
                    SaveStatus.Warning, Warnings: duplicates, WarningMessage: "A contact with this email already exists");
            }
        }

        if (isNew)
        {
            entity = new Contact();
            db.Contacts.Add(entity);
        }
        else
        {
            // Overwrite means "ignore the version I loaded": only Admin may, by design.
            var overwrite = options.Overwrite && user.IsAdmin;
            if (!overwrite)
            {
                db.Entry(entity!).Property(e => e.RowVersion).OriginalValue = model.RowVersion;
            }
        }

        // Decided from the state before this save: a contact that was already on keeps its date.
        var doNotContactSince = NextDoNotContactSince(entity!, model.DoNotContact);

        entity!.FirstName = model.FirstName;
        entity.LastName = model.LastName;
        entity.AccountId = accountId;
        entity.JobTitle = model.JobTitle;
        entity.Department = model.Department;
        entity.SalutationId = model.SalutationId;
        entity.DoNotContact = model.DoNotContact;
        entity.DoNotContactSince = doNotContactSince;
        entity.Email = email;
        entity.Phone = model.Phone;
        entity.Mobile = model.Mobile;
        entity.OwnerId = model.OwnerId!;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(model.Id, cancellationToken);
        }

        return new SaveResult(SaveStatus.Saved, entity.Id);
    }

    private static async Task<IReadOnlyList<string>> FindDuplicateEmailsAsync(
        CrmDbContext db, int excludeId, string email, CancellationToken cancellationToken)
    {
        var rows = await db.Contacts.AsNoTracking()
            .Where(c => c.Id != excludeId && c.Email == email)
            .OrderBy(c => c.Account.Name).ThenBy(c => c.Id)
            .Select(c => new { c.FirstName, c.LastName, AccountName = c.Account.Name })
            .Take(MaxWarnings)
            .ToListAsync(cancellationToken);

        // Names are put together here rather than read from FullName, which only SQL Server computes.
        return [.. rows.Select(r => $"{string.Join(' ', new[] { r.FirstName, r.LastName }.Where(s => !string.IsNullOrEmpty(s)))} ({r.AccountName})")];
    }

    private async Task<SaveResult> ConflictAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var current = await db.Contacts.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.UpdatedAt,
                ChangedBy = db.Users.Where(u => u.Id == c.UpdatedBy).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted by someone else in the meantime: nothing left to conflict with.
        return current is null
            ? new SaveResult(SaveStatus.NotFound)
            : new SaveResult(SaveStatus.Conflict, id, Conflict: new ConcurrencyConflict(current.ChangedBy, current.UpdatedAt));
    }

    // ---- Delete ----

    public async Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var contact = await db.Contacts.VisibleTo(user).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null)
        {
            return false;
        }

        contact.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// When Do not contact is switched on the date is now; while it stays on the date is kept; switched off clears it.
    /// </summary>
    private DateTime? NextDoNotContactSince(Contact entity, bool switchedOn) =>
        !switchedOn ? null
        : entity.DoNotContact && entity.DoNotContactSince is not null ? entity.DoNotContactSince
        : timeProvider.GetUtcNow().UtcDateTime;

    private static SaveResult Invalid(IReadOnlyDictionary<string, string> errors) => new(SaveStatus.Invalid, FieldErrors: errors);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
