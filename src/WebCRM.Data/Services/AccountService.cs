using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>
/// Account rules and queries (P8 list, P9 header). Every read goes through
/// <see cref="AccountAccess.VisibleTo"/>; soft-deleted rows are hidden by the global query filter.
/// </summary>
public sealed class AccountService(IDbContextFactory<CrmDbContext> factory, IOwnerService owners) : IAccountService
{
    private const int MaxSimilarNames = 5;
    private const int MaxWebsiteLength = 300;

    // ---- List (P8) ----

    public async Task<PagedResult<AccountListItem>> SearchAsync(
        AccountQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Count and page run together, each on its own context (a context runs one query at a time).
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        var countTask = Filter(countDb, query, user).CountAsync(cancellationToken);
        // Sort and page the accounts first, then project: EF cannot translate OrderBy on members of the
        // constructor-projected DTO, and this way only one page of rows computes the counts.
        var itemsTask = Project(pageDb, Sort(pageDb, Filter(pageDb, query, user), query)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return new PagedResult<AccountListItem>(itemsTask.Result, countTask.Result);
    }

    private static IQueryable<Account> Filter(CrmDbContext db, AccountQuery query, UserContext user)
    {
        var accounts = db.Accounts.AsNoTracking().VisibleTo(user);

        var userId = user.UserId;
        accounts = query.Scope switch
        {
            ListScope.Mine => accounts.Where(a => a.OwnerId == userId),
            ListScope.Team when user.TeamId is { } teamId =>
                accounts.Where(a => db.Users.Any(u => u.Id == a.OwnerId && u.TeamId == teamId)),
            ListScope.Team => accounts.Where(a => false),
            _ => accounts,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Name and City have the accent-insensitive collation, so LIKE ignores case and accents.
            var search = query.Search.Trim();
            accounts = accounts.Where(a =>
                a.Name.Contains(search)
                || (a.VatNumber != null && a.VatNumber.Contains(search))
                || a.Addresses.Any(ad => ad.AddressType == AddressType.Billing && ad.City != null && ad.City.Contains(search)));
        }

        if (query.StatusId is { } statusId)
        {
            accounts = accounts.Where(a => a.AccountStatusId == statusId);
        }

        if (query.IndustryId is { } industryId)
        {
            accounts = accounts.Where(a => a.IndustryId == industryId);
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;
            accounts = accounts.Where(a => a.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            accounts = accounts.Where(a =>
                a.Addresses.Any(ad => ad.AddressType == AddressType.Billing && ad.City != null && ad.City.Contains(city)));
        }

        return accounts;
    }

    private static IQueryable<AccountListItem> Project(CrmDbContext db, IQueryable<Account> accounts) =>
        accounts.Select(a => new AccountListItem(
            a.Id,
            a.Name,
            a.Industry != null ? a.Industry.Name : null,
            a.Addresses.Where(ad => ad.AddressType == AddressType.Billing).Select(ad => ad.City).FirstOrDefault(),
            a.AccountStatusId,
            a.AccountStatus.Name,
            a.OwnerId,
            db.Users.Where(u => u.Id == a.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == a.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
            a.Opportunities.Count(o => !o.Stage.IsWon && !o.Stage.IsLost),
            db.Activities.Where(x => x.AccountId == a.Id && x.DoneAt != null).Max(x => x.DoneAt)));

    private static IQueryable<Account> Sort(CrmDbContext db, IQueryable<Account> accounts, AccountQuery query)
    {
        var desc = query.Descending;
        var ordered = query.Sort switch
        {
            AccountSort.Industry => OrderBy(accounts, a => a.Industry != null ? a.Industry.Name : null, desc),
            AccountSort.City => OrderBy(
                accounts,
                a => a.Addresses.Where(ad => ad.AddressType == AddressType.Billing).Select(ad => ad.City).FirstOrDefault(),
                desc),
            AccountSort.Status => OrderBy(accounts, a => a.AccountStatus.Name, desc),
            AccountSort.Owner => OrderBy(
                accounts, a => db.Users.Where(u => u.Id == a.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), desc),
            AccountSort.OpenOpportunities => OrderBy(
                accounts, a => a.Opportunities.Count(o => !o.Stage.IsWon && !o.Stage.IsLost), desc),
            AccountSort.LastActivity => OrderBy(
                accounts,
                a => db.Activities.Where(x => x.AccountId == a.Id && x.DoneAt != null).Max(x => x.DoneAt),
                desc),
            _ => OrderBy(accounts, a => a.Name, desc),
        };

        // Id as the tie-breaker keeps paging stable.
        return ordered.ThenBy(a => a.Id);
    }

    private static IOrderedQueryable<Account> OrderBy<TKey>(
        IQueryable<Account> accounts, Expression<Func<Account, TKey>> key, bool descending) =>
        descending ? accounts.OrderByDescending(key) : accounts.OrderBy(key);

    // ---- Detail (P9) ----

    public async Task<AccountDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Accounts.AsNoTracking()
            .VisibleTo(user)
            .Where(a => a.Id == id)
            .Select(a => new AccountDetail(
                a.Id,
                a.Name,
                a.VatNumber,
                a.IndustryId,
                a.Industry != null ? a.Industry.Name : null,
                a.AccountStatusId,
                a.AccountStatus.Name,
                a.Phone,
                a.Website,
                a.OwnerId,
                db.Users.Where(u => u.Id == a.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                db.Users.Where(u => u.Id == a.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
                a.CreatedAt,
                a.UpdatedAt,
                a.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return new AccountEditModel
        {
            OwnerId = user.UserId,
            AccountStatusId = await FirstStatusIdAsync(db, cancellationToken),
        };
    }

    private static async Task<int?> FirstStatusIdAsync(CrmDbContext db, CancellationToken cancellationToken) =>
        await db.AccountStatuses.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // ---- Save ----

    public async Task<AccountSaveResult> SaveAsync(
        AccountEditModel model, UserContext user, AccountSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AccountSaveOptions();
        var errors = new Dictionary<string, string>();

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        // Normalise first, so validation and the uniqueness checks see what will be stored: "el 094.259-216"
        // and "EL094259216" are the same Tax ID, and "mexdb.com" becomes "https://mexdb.com". A bare 9-digit
        // number is a Greek ΑΦΜ only when the company's default country is Greece (a US EIN is 9 digits too).
        var defaultCountry = await db.CompanySettings.AsNoTracking()
            .Select(c => c.DefaultCountryCode).FirstOrDefaultAsync(cancellationToken);
        model.VatNumber = VatNumberRules.Normalize(model.VatNumber, defaultCountry);
        model.Website = WebsiteRules.Normalize(model.Website);

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

        var name = model.Name?.Trim() ?? string.Empty;
        if (model.Website is { Length: > MaxWebsiteLength })
        {
            errors.TryAdd(nameof(AccountEditModel.Website), $"The web address is too long (maximum {MaxWebsiteLength} characters).");
        }

        if (name.Length == 0)
        {
            errors.TryAdd(nameof(AccountEditModel.Name), "Name is required.");
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var vat = model.VatNumber;

        var isNew = model.Id == 0;
        Account? entity = null;
        if (!isNew)
        {
            entity = await db.Accounts.VisibleTo(user).FirstOrDefaultAsync(a => a.Id == model.Id, cancellationToken);
            if (entity is null)
            {
                return new AccountSaveResult(AccountSaveStatus.NotFound);
            }
        }

        await CheckReferencesAsync(db, model, entity, user, errors, cancellationToken);

        if (await db.Accounts.AnyAsync(a => a.Id != model.Id && a.Name == name, cancellationToken))
        {
            errors[nameof(AccountEditModel.Name)] = "An account with this name already exists.";
        }

        if (vat is not null && await db.Accounts.AnyAsync(a => a.Id != model.Id && a.VatNumber == vat, cancellationToken))
        {
            errors[nameof(AccountEditModel.VatNumber)] = "An account with this VAT / Tax ID already exists.";
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        // Warn, don't block: similar names (not the exact name, which is blocked above).
        var nameChanged = isNew || !string.Equals(entity!.Name, name, StringComparison.OrdinalIgnoreCase);
        if (nameChanged && !options.AcceptSimilarNames)
        {
            var similar = await FindSimilarNamesAsync(db, model.Id, name, cancellationToken);
            if (similar.Count > 0)
            {
                return new AccountSaveResult(AccountSaveStatus.SimilarNames, SimilarNames: similar);
            }
        }

        if (isNew)
        {
            entity = new Account();
            db.Accounts.Add(entity);
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

        entity!.Name = name;
        entity.VatNumber = vat;
        entity.IndustryId = model.IndustryId;
        entity.AccountStatusId = model.AccountStatusId!.Value;
        entity.Phone = Blank(model.Phone);
        entity.Website = Blank(model.Website);
        entity.OwnerId = model.OwnerId!;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(model.Id, cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation(ex) is { } field)
        {
            // Lost a race with another save that took the same name or VAT number.
            return Invalid(new Dictionary<string, string>
            {
                [field] = field == nameof(AccountEditModel.VatNumber)
                    ? "An account with this VAT / Tax ID already exists."
                    : "An account with this name already exists.",
            });
        }

        return new AccountSaveResult(AccountSaveStatus.Saved, entity.Id);
    }

    private async Task CheckReferencesAsync(
        CrmDbContext db, AccountEditModel model, Account? existing, UserContext user,
        Dictionary<string, string> errors, CancellationToken cancellationToken)
    {
        // A deactivated value may stay on a record that already has it, but cannot be newly chosen.
        var currentStatusId = existing?.AccountStatusId;
        var currentIndustryId = existing?.IndustryId;
        var statusId = model.AccountStatusId!.Value;
        if (!await db.AccountStatuses.AnyAsync(
                s => s.Id == statusId && (s.IsActive || s.Id == currentStatusId), cancellationToken))
        {
            errors[nameof(AccountEditModel.AccountStatusId)] = "Choose a valid status.";
        }

        if (model.IndustryId is { } industryId
            && !await db.Industries.AnyAsync(
                i => i.Id == industryId && (i.IsActive || i.Id == currentIndustryId), cancellationToken))
        {
            errors[nameof(AccountEditModel.IndustryId)] = "Choose a valid industry.";
        }

        var assignable = await owners.GetAssignableAsync(user, existing?.OwnerId, cancellationToken);
        if (assignable.All(o => o.Id != model.OwnerId))
        {
            errors[nameof(AccountEditModel.OwnerId)] = "You cannot assign this owner.";
        }
    }

    private static async Task<IReadOnlyList<string>> FindSimilarNamesAsync(
        CrmDbContext db, int excludeId, string name, CancellationToken cancellationToken)
    {
        var longEnough = name.Length >= 3;
        return await db.Accounts.AsNoTracking()
            .Where(a => a.Id != excludeId && a.Name != name)
            .Where(a => (longEnough && a.Name.Contains(name)) || (a.Name.Length >= 4 && name.Contains(a.Name)))
            .OrderBy(a => a.Name)
            .Select(a => a.Name)
            .Take(MaxSimilarNames)
            .ToListAsync(cancellationToken);
    }

    private async Task<AccountSaveResult> ConflictAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var current = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.UpdatedAt,
                ChangedBy = db.Users.Where(u => u.Id == a.UpdatedBy).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted by someone else in the meantime: nothing left to conflict with.
        return current is null
            ? new AccountSaveResult(AccountSaveStatus.NotFound)
            : new AccountSaveResult(
                AccountSaveStatus.Conflict, id, Conflict: new ConcurrencyConflict(current.ChangedBy, current.UpdatedAt));
    }

    // ---- Delete ----

    public async Task<AccountDeleteImpact?> GetDeleteImpactAsync(
        int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return await db.Accounts.AsNoTracking()
            .VisibleTo(user)
            .Where(a => a.Id == id)
            .Select(a => new AccountDeleteImpact(
                a.Contacts.Count,
                a.Opportunities.Count(o => !o.Stage.IsWon && !o.Stage.IsLost)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountDeleteResult> DeleteAsync(
        int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var account = await db.Accounts.VisibleTo(user).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return new AccountDeleteResult(AccountDeleteStatus.NotFound);
        }

        var openOpportunities = await db.Opportunities
            .CountAsync(o => o.AccountId == id && !o.Stage.IsWon && !o.Stage.IsLost, cancellationToken);
        if (openOpportunities > 0)
        {
            return new AccountDeleteResult(
                AccountDeleteStatus.Blocked,
                $"This account has {openOpportunities} open {(openOpportunities == 1 ? "opportunity" : "opportunities")}. Close or reassign {(openOpportunities == 1 ? "it" : "them")} first.");
        }

        account.IsActive = false;
        foreach (var contact in await db.Contacts.Where(c => c.AccountId == id).ToListAsync(cancellationToken))
        {
            contact.IsActive = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new AccountDeleteResult(AccountDeleteStatus.Deleted);
    }

    // ---- Helpers ----

    private static AccountSaveResult Invalid(IReadOnlyDictionary<string, string> errors) =>
        new(AccountSaveStatus.Invalid, FieldErrors: errors);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The form field behind a unique-index violation, or null when it is something else.</summary>
    private static string? UniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException { Number: 2601 or 2627 } sql)
        {
            return null;
        }

        return sql.Message.Contains("VatNumber", StringComparison.OrdinalIgnoreCase)
            ? nameof(AccountEditModel.VatNumber)
            : nameof(AccountEditModel.Name);
    }
}
