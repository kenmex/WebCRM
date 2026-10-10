using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>
/// Lead rules and queries (P12 list, P13 header card) and the Convert action. Every read goes through
/// <see cref="LeadAccess.VisibleTo"/>; soft-deleted rows are hidden by the global query filter.
/// </summary>
public sealed class LeadService(
    IDbContextFactory<CrmDbContext> factory, IOwnerService owners, TimeProvider timeProvider) : ILeadService
{
    private const int MaxAccountMatches = 5;

    // ---- List (P12) ----

    public async Task<PagedResult<LeadListItem>> SearchAsync(
        LeadQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Count and page run together, each on its own context (a context runs one query at a time).
        await using var countDb = await factory.CreateDbContextAsync(cancellationToken);
        await using var pageDb = await factory.CreateDbContextAsync(cancellationToken);

        var countTask = Filter(countDb, query, user).CountAsync(cancellationToken);
        var itemsTask = Project(pageDb, Sort(pageDb, Filter(pageDb, query, user), query)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return new PagedResult<LeadListItem>(itemsTask.Result, countTask.Result);
    }

    private static IQueryable<Lead> Filter(CrmDbContext db, LeadQuery query, UserContext user)
    {
        var leads = db.Leads.AsNoTracking().VisibleTo(user);

        var userId = user.UserId;
        leads = query.Scope switch
        {
            ListScope.Mine => leads.Where(l => l.OwnerId == userId),
            ListScope.Team when user.TeamId is { } teamId =>
                leads.Where(l => db.Users.Any(u => u.Id == l.OwnerId && u.TeamId == teamId)),
            ListScope.Team => leads.Where(l => false),
            _ => leads,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Name, Company and Email have the accent-insensitive collation, so LIKE ignores case and accents.
            var search = query.Search.Trim();
            leads = leads.Where(l =>
                l.Name.Contains(search)
                || (l.Company != null && l.Company.Contains(search))
                || (l.Email != null && l.Email.Contains(search))
                || (l.Phone != null && l.Phone.Contains(search)));
        }

        if (query.StatusId is { } statusId)
        {
            leads = leads.Where(l => l.LeadStatusId == statusId);
        }
        else if (query.OpenOnly)
        {
            leads = leads.Where(l => l.LeadStatus.SystemCode != LeadStatus.Converted
                && l.LeadStatus.SystemCode != LeadStatus.Disqualified);
        }

        if (query.SourceId is { } sourceId)
        {
            leads = leads.Where(l => l.LeadSourceId == sourceId);
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;
            leads = leads.Where(l => l.OwnerId == ownerId);
        }

        return leads;
    }

    private static IQueryable<LeadListItem> Project(CrmDbContext db, IQueryable<Lead> leads) =>
        leads.Select(l => new LeadListItem(
            l.Id,
            l.Name,
            l.Company,
            l.Email,
            l.LeadSource != null ? l.LeadSource.Name : null,
            l.LeadStatus.Name,
            l.LeadStatus.SystemCode,
            l.OwnerId,
            db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
            l.CreatedAt));

    private static IQueryable<Lead> Sort(CrmDbContext db, IQueryable<Lead> leads, LeadQuery query)
    {
        var desc = query.Descending;
        var ordered = query.Sort switch
        {
            LeadSort.Name => OrderBy(leads, l => l.Name, desc),
            LeadSort.Company => OrderBy(leads, l => l.Company, desc),
            LeadSort.Email => OrderBy(leads, l => l.Email, desc),
            LeadSort.Source => OrderBy(leads, l => l.LeadSource != null ? l.LeadSource.Name : null, desc),
            LeadSort.Status => OrderBy(leads, l => l.LeadStatus.SortOrder, desc),
            LeadSort.Owner => OrderBy(
                leads, l => db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), desc),
            _ => OrderBy(leads, l => l.CreatedAt, desc),
        };

        // Id as the tie-breaker keeps paging stable.
        return desc ? ordered.ThenByDescending(l => l.Id) : ordered.ThenBy(l => l.Id);
    }

    private static IOrderedQueryable<Lead> OrderBy<TKey>(
        IQueryable<Lead> leads, Expression<Func<Lead, TKey>> key, bool descending) =>
        descending ? leads.OrderByDescending(key) : leads.OrderBy(key);

    // ---- Detail (P13) ----

    public async Task<LeadDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var row = await db.Leads.AsNoTracking()
            .VisibleTo(user)
            .Where(l => l.Id == id)
            .Select(l => new
            {
                Lead = l,
                SourceName = l.LeadSource != null ? l.LeadSource.Name : null,
                StatusName = l.LeadStatus.Name,
                StatusCode = l.LeadStatus.SystemCode,
                OwnerName = db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                OwnerIsActive = db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
                AccountName = l.ConvertedAccount != null ? l.ConvertedAccount.Name : null,
                ContactFirst = l.ConvertedContact != null ? l.ConvertedContact.FirstName : null,
                ContactLast = l.ConvertedContact != null ? l.ConvertedContact.LastName : null,
                OpportunityName = l.ConvertedOpportunity != null ? l.ConvertedOpportunity.Name : null,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var l = row.Lead;
        string? contactName = l.ConvertedContactId is null || row.ContactLast is null
            ? null
            : string.Join(' ', new[] { row.ContactFirst, row.ContactLast }.Where(s => !string.IsNullOrEmpty(s)));

        return new LeadDetail(
            l.Id, l.Name, l.Company, l.Email, l.Phone, l.LeadSourceId, row.SourceName, l.LeadStatusId, row.StatusName,
            row.StatusCode, l.OwnerId, row.OwnerName, row.OwnerIsActive, l.CreatedAt, l.UpdatedAt, l.RowVersion,
            l.ConvertedAt, l.ConvertedAccountId, row.AccountName, l.ConvertedContactId, contactName,
            l.ConvertedOpportunityId, row.OpportunityName);
    }

    public async Task<LeadEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        return new LeadEditModel
        {
            LeadStatusId = await StatusIdAsync(db, LeadStatus.New, cancellationToken),
            OwnerId = user.UserId,
        };
    }

    // ---- Save ----

    public async Task<SaveResult> SaveAsync(
        LeadEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SaveOptions();
        var errors = new Dictionary<string, string>();

        // Normalise first, so validation sees what will be stored.
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Company = Blank(model.Company);
        model.Phone = Blank(model.Phone);
        model.Email = ContactRules.NormalizeEmail(model.Email);

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

        if (model.Name.Length == 0)
        {
            errors.TryAdd(nameof(LeadEditModel.Name), "Name is required.");
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var isNew = model.Id == 0;
        Lead? entity = null;
        if (!isNew)
        {
            entity = await db.Leads.VisibleTo(user).FirstOrDefaultAsync(l => l.Id == model.Id, cancellationToken);
            if (entity is null)
            {
                return new SaveResult(SaveStatus.NotFound);
            }

            if (entity.ConvertedAt is not null)
            {
                return Invalid(new Dictionary<string, string> { [string.Empty] = "A converted lead is read-only." });
            }
        }

        // Who may own it: the usual rule (Sales: themselves, Manager: their team, Admin: anyone active, plus the
        // current owner on edit).
        var allowed = (await owners.GetAssignableAsync(user, entity?.OwnerId, cancellationToken)).Select(o => o.Id).ToHashSet();
        if (!allowed.Contains(model.OwnerId!))
        {
            errors[nameof(LeadEditModel.OwnerId)] = "You cannot assign this owner.";
        }

        // A deactivated value may stay on a lead that already has it, but cannot be newly chosen.
        var currentSourceId = entity?.LeadSourceId;
        if (model.LeadSourceId is { } sourceId
            && !await db.LeadSources.AnyAsync(s => s.Id == sourceId && (s.IsActive || s.Id == currentSourceId), cancellationToken))
        {
            errors[nameof(LeadEditModel.LeadSourceId)] = "Choose a valid source.";
        }

        var currentStatusId = entity?.LeadStatusId;
        var statusId = model.LeadStatusId!.Value;
        var status = await db.LeadStatuses.AsNoTracking()
            .Where(s => s.Id == statusId && (s.IsActive || s.Id == currentStatusId))
            .Select(s => new { s.SystemCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (status is null)
        {
            errors[nameof(LeadEditModel.LeadStatusId)] = "Choose a valid status.";
        }
        else if (status.SystemCode == LeadStatus.Converted)
        {
            errors[nameof(LeadEditModel.LeadStatusId)] = "A lead becomes Converted only through the Convert action.";
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (isNew)
        {
            entity = new Lead();
            db.Leads.Add(entity);
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

        entity!.Name = model.Name;
        entity.Company = model.Company;
        entity.Email = model.Email;
        entity.Phone = model.Phone;
        entity.LeadSourceId = model.LeadSourceId;
        entity.LeadStatusId = statusId;
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

    private async Task<SaveResult> ConflictAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var current = await db.Leads.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new
            {
                l.UpdatedAt,
                ChangedBy = db.Users.Where(u => u.Id == l.UpdatedBy).Select(u => u.DisplayName).FirstOrDefault(),
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

        var lead = await db.Leads.VisibleTo(user).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        // A converted lead is kept: it is the source record behind the account, contact and opportunity.
        if (lead is null || lead.ConvertedAt is not null)
        {
            return false;
        }

        lead.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Convert (P13, US3) ----

    public async Task<ConvertPrefill?> GetConvertPrefillAsync(
        int leadId, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var lead = await db.Leads.AsNoTracking()
            .VisibleTo(user)
            .Where(l => l.Id == leadId)
            .Select(l => new { l.Name, l.Company, l.Email, l.Phone, l.ConvertedAt, StatusCode = l.LeadStatus.SystemCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (lead is null || lead.ConvertedAt is not null || lead.StatusCode is LeadStatus.Converted or LeadStatus.Disqualified)
        {
            return null;
        }

        var (first, last) = LeadRules.SplitName(lead.Name);
        return new ConvertPrefill(
            leadId,
            lead.Company?.Trim() ?? string.Empty,
            first,
            last,
            lead.Email,
            lead.Phone,
            LeadRules.DefaultOpportunityName(lead.Company, lead.Name),
            await FindAccountMatchesAsync(db, lead.Company, lead.Email, user, cancellationToken));
    }

    private static async Task<IReadOnlyList<AccountMatch>> FindAccountMatchesAsync(
        CrmDbContext db, string? company, string? email, UserContext user, CancellationToken cancellationToken)
    {
        var matches = new Dictionary<int, AccountMatch>();

        void Add(IEnumerable<(int Id, string Name)> found, AccountMatchReason reason)
        {
            foreach (var (id, name) in found)
            {
                matches[id] = matches.TryGetValue(id, out var existing)
                    ? existing with { Reasons = existing.Reasons | reason }
                    : new AccountMatch(id, name, reason);
            }
        }

        // Name has the accent-insensitive collation, so LIKE ignores case and accents.
        if (company?.Trim() is { Length: >= 2 } name)
        {
            var byName = await db.Accounts.AsNoTracking()
                .VisibleTo(user)
                .Where(a => a.Name.Contains(name) || (a.Name.Length >= 4 && name.Contains(a.Name)))
                .OrderBy(a => a.Name).ThenBy(a => a.Id)
                .Select(a => new { a.Id, a.Name })
                .Take(MaxAccountMatches)
                .ToListAsync(cancellationToken);
            Add(byName.Select(a => (a.Id, a.Name)), AccountMatchReason.CompanyName);
        }

        if (LeadRules.MatchingDomain(email) is { } domain)
        {
            var suffix = "@" + domain;
            var byDomain = await db.Accounts.AsNoTracking()
                .VisibleTo(user)
                .Where(a => (a.Email != null && a.Email.EndsWith(suffix))
                    || a.Contacts.Any(c => c.Email != null && c.Email.EndsWith(suffix)))
                .OrderBy(a => a.Name).ThenBy(a => a.Id)
                .Select(a => new { a.Id, a.Name })
                .Take(MaxAccountMatches)
                .ToListAsync(cancellationToken);
            Add(byDomain.Select(a => (a.Id, a.Name)), AccountMatchReason.EmailDomain);
        }

        return [.. matches.Values
            .OrderByDescending(m => BitCount((int)m.Reasons))
            .ThenBy(m => m.Name)
            .ThenBy(m => m.AccountId)
            .Take(MaxAccountMatches)];

        static int BitCount(int flags) => System.Numerics.BitOperations.PopCount((uint)flags);
    }

    public async Task<ConvertResult> ConvertAsync(
        LeadConvertRequest request, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var lead = await db.Leads
            .VisibleTo(user)
            .Include(l => l.LeadStatus)
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken);
        if (lead is null)
        {
            return new ConvertResult(ConvertStatus.NotFound);
        }

        if (lead.ConvertedAt is not null || lead.LeadStatus.SystemCode == LeadStatus.Converted)
        {
            return new ConvertResult(ConvertStatus.AlreadyConverted);
        }

        if (lead.LeadStatus.SystemCode == LeadStatus.Disqualified)
        {
            return new ConvertResult(ConvertStatus.Disqualified);
        }

        // Everything below only builds entities on this one context; nothing is written until the single
        // SaveChangesAsync at the end, which SQL Server runs as one transaction. Any failure leaves nothing behind.
        var errors = new Dictionary<string, string>();

        // ---- Account ----
        Account? newAccount = null;
        int? accountId = request.ExistingAccountId;
        if (accountId is { } existingAccountId)
        {
            // Linking to a teammate's account is allowed when it is visible (D7).
            if (!await db.Accounts.AsNoTracking().VisibleTo(user).AnyAsync(a => a.Id == existingAccountId, cancellationToken))
            {
                errors[nameof(LeadConvertRequest.ExistingAccountId)] = "Choose an existing account.";
            }
        }
        else
        {
            var name = request.NewAccountName?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                errors[nameof(LeadConvertRequest.NewAccountName)] = "Account name is required.";
            }
            else if (name.Length > 200)
            {
                errors[nameof(LeadConvertRequest.NewAccountName)] = "The account name is too long (maximum 200 characters).";
            }
            else if (await db.Accounts.AnyAsync(a => a.Name == name, cancellationToken))
            {
                errors[nameof(LeadConvertRequest.NewAccountName)] =
                    "An account with this name already exists. Choose it as the existing account instead.";
            }
            else
            {
                var statusId = await db.AccountStatuses.AsNoTracking()
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (statusId is null)
                {
                    throw new InvalidOperationException("There is no active account status to give the new account.");
                }

                newAccount = new Account { Name = name, AccountStatusId = statusId.Value, OwnerId = user.UserId };
                db.Accounts.Add(newAccount);
            }
        }

        // ---- Contact ----
        Contact? newContact = null;
        int? contactId = null;
        if (request.ExistingContactId is { } existingContactId)
        {
            if (newAccount is not null || accountId is null)
            {
                errors[nameof(LeadConvertRequest.ExistingContactId)] = "A new account starts with a new contact.";
            }
            else if (!await db.Contacts.AsNoTracking().VisibleTo(user)
                .AnyAsync(c => c.Id == existingContactId && c.AccountId == accountId, cancellationToken))
            {
                errors[nameof(LeadConvertRequest.ExistingContactId)] = "Choose a contact of this account.";
            }
            else
            {
                contactId = existingContactId;
            }
        }
        else
        {
            var firstName = Blank(request.ContactFirstName);
            var lastName = request.ContactLastName?.Trim() ?? string.Empty;
            var email = ContactRules.NormalizeEmail(request.ContactEmail);
            var jobTitle = Blank(request.ContactJobTitle);
            var phone = Blank(request.ContactPhone);

            if (lastName.Length == 0)
            {
                errors[nameof(LeadConvertRequest.ContactLastName)] = "Last name is required.";
            }

            if (firstName is { Length: > 100 } || lastName.Length > 100)
            {
                errors[nameof(LeadConvertRequest.ContactLastName)] = "The name is too long (maximum 100 characters).";
            }

            if (jobTitle is { Length: > 100 })
            {
                errors[nameof(LeadConvertRequest.ContactJobTitle)] = "The job title is too long (maximum 100 characters).";
            }

            if (phone is { Length: > 30 })
            {
                errors[nameof(LeadConvertRequest.ContactPhone)] = "The phone number is too long (maximum 30 characters).";
            }

            if (email is not null && !ContactRules.IsValidEmail(email))
            {
                errors[nameof(LeadConvertRequest.ContactEmail)] = ContactRules.InvalidEmailMessage;
            }

            // Built even when there are errors: they all return before SaveChanges, which discards the context.
            newContact = new Contact
            {
                FirstName = firstName,
                LastName = lastName,
                JobTitle = jobTitle,
                Email = email,
                Phone = phone,
                OwnerId = user.UserId,
            };
            if (newAccount is not null)
            {
                newContact.Account = newAccount;
            }
            else if (accountId is { } linkedAccountId)
            {
                newContact.AccountId = linkedAccountId;
            }

            db.Contacts.Add(newContact);
        }

        // ---- Opportunity (optional) ----
        Opportunity? newOpportunity = null;
        if (request.CreateOpportunity)
        {
            var opportunityName = request.OpportunityName?.Trim() ?? string.Empty;
            if (opportunityName.Length == 0)
            {
                errors[nameof(LeadConvertRequest.OpportunityName)] = "Opportunity name is required.";
            }
            else if (opportunityName.Length > 200)
            {
                errors[nameof(LeadConvertRequest.OpportunityName)] = "The opportunity name is too long (maximum 200 characters).";
            }

            var stage = await db.Stages.AsNoTracking()
                .Where(s => s.IsActive && !s.IsWon && !s.IsLost)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (stage is null)
            {
                throw new InvalidOperationException("There is no open stage to give the new opportunity.");
            }

            if (errors.Count == 0)
            {
                newOpportunity = new Opportunity
                {
                    Name = opportunityName,
                    StageId = stage.Id,
                    Amount = 0,
                    Currency = "EUR",
                    Probability = stage.DefaultProbability,
                    ProbabilityOverridden = false,
                    CloseDate = OpportunityDefaults.CloseDate(timeProvider.GetUtcNow()),
                    OwnerId = user.UserId,
                };
                if (newAccount is not null)
                {
                    newOpportunity.Account = newAccount;
                }
                else
                {
                    newOpportunity.AccountId = accountId!.Value;
                }

                if (newContact is not null)
                {
                    newOpportunity.PrimaryContact = newContact;
                }
                else
                {
                    newOpportunity.PrimaryContactId = contactId;
                }

                db.Opportunities.Add(newOpportunity);
            }
        }

        if (errors.Count > 0)
        {
            return new ConvertResult(ConvertStatus.Invalid, FieldErrors: errors);
        }

        // ---- The lead's activities and notes move to the contact (deleted ones too: the history goes with it) ----
        var activities = await db.Activities.IgnoreQueryFilters().Where(a => a.LeadId == lead.Id).ToListAsync(cancellationToken);
        var notes = await db.Notes.IgnoreQueryFilters().Where(n => n.LeadId == lead.Id).ToListAsync(cancellationToken);
        foreach (var activity in activities)
        {
            activity.LeadId = null;
            if (newContact is not null)
            {
                activity.Contact = newContact;
            }
            else
            {
                activity.ContactId = contactId;
            }
        }

        foreach (var note in notes)
        {
            note.LeadId = null;
            if (newContact is not null)
            {
                note.Contact = newContact;
            }
            else
            {
                note.ContactId = contactId;
            }
        }

        // ---- The lead itself ----
        var now = timeProvider.GetUtcNow().UtcDateTime;
        lead.LeadStatusId = await StatusIdAsync(db, LeadStatus.Converted, cancellationToken)
            ?? throw new InvalidOperationException("The Converted lead status is missing.");
        lead.ConvertedAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        if (newAccount is not null)
        {
            lead.ConvertedAccount = newAccount;
        }
        else
        {
            lead.ConvertedAccountId = accountId;
        }

        if (newContact is not null)
        {
            lead.ConvertedContact = newContact;
        }
        else
        {
            lead.ConvertedContactId = contactId;
        }

        if (newOpportunity is not null)
        {
            lead.ConvertedOpportunity = newOpportunity;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Nothing was written. Most likely someone else converted this lead at the same moment (the row version
            // no longer matches, or both tried to create the same account name).
            return await AfterFailedConvertAsync(request.LeadId, ex, cancellationToken);
        }

        return new ConvertResult(
            ConvertStatus.Converted,
            lead.ConvertedAccountId,
            lead.ConvertedContactId,
            lead.ConvertedOpportunityId);
    }

    private async Task<ConvertResult> AfterFailedConvertAsync(int leadId, Exception failure, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var current = await db.Leads.AsNoTracking()
            .Where(l => l.Id == leadId)
            .Select(l => new { l.ConvertedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return new ConvertResult(ConvertStatus.NotFound);
        }

        if (current.ConvertedAt is not null)
        {
            return new ConvertResult(ConvertStatus.AlreadyConverted);
        }

        if (failure is DbUpdateConcurrencyException || IsDeadlock(failure))
        {
            return new ConvertResult(ConvertStatus.Conflict);
        }

        if (IsUniqueViolation(failure))
        {
            return new ConvertResult(ConvertStatus.Invalid, FieldErrors: new Dictionary<string, string>
            {
                [nameof(LeadConvertRequest.NewAccountName)] =
                    "An account with this name already exists. Choose it as the existing account instead.",
            });
        }

        throw failure is DbUpdateException ? new InvalidOperationException("Could not convert the lead.", failure) : failure;
    }

    private static bool IsUniqueViolation(Exception ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsDeadlock(Exception ex) => ex.InnerException is SqlException { Number: 1205 };

    // ---- Helpers ----

    private static async Task<int?> StatusIdAsync(CrmDbContext db, string systemCode, CancellationToken cancellationToken) =>
        await db.LeadStatuses.AsNoTracking()
            .Where(s => s.SystemCode == systemCode)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static SaveResult Invalid(IReadOnlyDictionary<string, string> errors) => new(SaveStatus.Invalid, FieldErrors: errors);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
