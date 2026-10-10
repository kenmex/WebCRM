using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core;
using WebCRM.Core.Entities;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

/// <summary>
/// Opportunity rules and queries (P14 list, P15 board, P16 header card). Every read goes through
/// <see cref="OpportunityAccess.VisibleTo"/>; soft-deleted rows are hidden by the global query filter.
/// </summary>
public sealed class OpportunityService(
    IDbContextFactory<CrmDbContext> factory, IOwnerService owners, TimeProvider timeProvider) : IOpportunityService
{
    private const int MaxAccountTabRows = 200;

    // ---- Stages ----

    public async Task<IReadOnlyList<StageOption>> GetStagesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await LoadStagesAsync(db, cancellationToken);
    }

    private static async Task<IReadOnlyList<StageOption>> LoadStagesAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        var stages = await db.Stages.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new StageOption(s.Id, s.Name, s.SortOrder, s.DefaultProbability, s.IsWon, s.IsLost))
            .ToListAsync(cancellationToken);

        // Open stages in their own order, then Won, then Lost.
        return [.. stages.OrderBy(s => s.IsLost ? 2 : s.IsWon ? 1 : 0).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)];
    }

    // ---- List (P14) ----

    public async Task<PagedResult<OpportunityListItem>> SearchAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default)
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
        return new PagedResult<OpportunityListItem>(itemsTask.Result, countTask.Result);
    }

    public async Task<OpportunityTotals> GetTotalsAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        // One aggregate over the whole filtered set. Weighted is the sum of the per-row rounded values, so the
        // footer always equals the sum of the Weighted column.
        var totals = await Filter(db, query, user)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Amount = g.Sum(o => o.Amount),
                Weighted = g.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2)),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null ? new OpportunityTotals(0m, 0m) : new OpportunityTotals(totals.Amount, totals.Weighted);
    }

    private static IQueryable<Opportunity> Filter(CrmDbContext db, OpportunityQuery query, UserContext user)
    {
        var opportunities = db.Opportunities.AsNoTracking().VisibleTo(user);
        opportunities = ApplyScope(db, opportunities, query.Scope, user);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Name and Account.Name have the accent-insensitive collation, so LIKE ignores case and accents.
            var search = query.Search.Trim();
            opportunities = opportunities.Where(o => o.Name.Contains(search) || o.Account.Name.Contains(search));
        }

        if (query.StageId is { } stageId)
        {
            opportunities = opportunities.Where(o => o.StageId == stageId);
        }
        else
        {
            opportunities = query.Outcome switch
            {
                OpportunityOutcome.Open => opportunities.Where(o => !o.Stage.IsWon && !o.Stage.IsLost),
                OpportunityOutcome.Won => opportunities.Where(o => o.Stage.IsWon),
                OpportunityOutcome.Lost => opportunities.Where(o => o.Stage.IsLost),
                _ => opportunities,
            };
        }

        return ApplyCommonFilters(opportunities, query.OwnerId, query.CloseFrom, query.CloseTo);
    }

    private static IQueryable<Opportunity> ApplyScope(
        CrmDbContext db, IQueryable<Opportunity> opportunities, ListScope scope, UserContext user)
    {
        var userId = user.UserId;
        return scope switch
        {
            ListScope.Mine => opportunities.Where(o => o.OwnerId == userId),
            ListScope.Team when user.TeamId is { } teamId =>
                opportunities.Where(o => db.Users.Any(u => u.Id == o.OwnerId && u.TeamId == teamId)),
            ListScope.Team => opportunities.Where(o => false),
            _ => opportunities,
        };
    }

    private static IQueryable<Opportunity> ApplyCommonFilters(
        IQueryable<Opportunity> opportunities, string? ownerId, DateOnly? closeFrom, DateOnly? closeTo)
    {
        if (!string.IsNullOrEmpty(ownerId))
        {
            opportunities = opportunities.Where(o => o.OwnerId == ownerId);
        }

        if (closeFrom is { } from)
        {
            opportunities = opportunities.Where(o => o.CloseDate >= from);
        }

        if (closeTo is { } to)
        {
            opportunities = opportunities.Where(o => o.CloseDate <= to);
        }

        return opportunities;
    }

    private static IQueryable<OpportunityListItem> Project(CrmDbContext db, IQueryable<Opportunity> opportunities) =>
        opportunities.Select(o => new OpportunityListItem(
            o.Id,
            o.Name,
            o.AccountId,
            o.Account.Name,
            o.StageId,
            o.Stage.Name,
            o.Stage.IsWon,
            o.Stage.IsLost,
            o.Amount,
            o.Probability,
            o.CloseDate,
            o.OwnerId,
            db.Users.Where(u => u.Id == o.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == o.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
            o.RowVersion));

    private static IQueryable<Opportunity> Sort(CrmDbContext db, IQueryable<Opportunity> opportunities, OpportunityQuery query)
    {
        var desc = query.Descending;
        var ordered = query.Sort switch
        {
            OpportunitySort.Name => OrderBy(opportunities, o => o.Name, desc),
            OpportunitySort.Account => OrderBy(opportunities, o => o.Account.Name, desc),
            OpportunitySort.Stage => OrderBy(opportunities, o => o.Stage.SortOrder, desc),
            OpportunitySort.Amount => OrderBy(opportunities, o => o.Amount, desc),
            OpportunitySort.Probability => OrderBy(opportunities, o => o.Probability, desc),
            OpportunitySort.Weighted => OrderBy(opportunities, o => o.Amount * o.Probability, desc),
            OpportunitySort.Owner => OrderBy(
                opportunities, o => db.Users.Where(u => u.Id == o.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), desc),
            _ => OrderBy(opportunities, o => o.CloseDate, desc),
        };

        // Id as the tie-breaker keeps paging stable.
        return desc ? ordered.ThenByDescending(o => o.Id) : ordered.ThenBy(o => o.Id);
    }

    private static IOrderedQueryable<Opportunity> OrderBy<TKey>(
        IQueryable<Opportunity> opportunities, Expression<Func<Opportunity, TKey>> key, bool descending) =>
        descending ? opportunities.OrderByDescending(key) : opportunities.OrderBy(key);

    // ---- Board (P15) ----

    public async Task<IReadOnlyList<BoardColumn>> GetBoardAsync(
        BoardQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var stagesDb = await factory.CreateDbContextAsync(cancellationToken);
        var stages = await LoadStagesAsync(stagesDb, cancellationToken);

        IQueryable<Opportunity> Base(CrmDbContext db)
        {
            var opportunities = ApplyScope(db, db.Opportunities.AsNoTracking().VisibleTo(user), query.Scope, user);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                opportunities = opportunities.Where(o => o.Name.Contains(search) || o.Account.Name.Contains(search));
            }

            return ApplyCommonFilters(opportunities, query.OwnerId, query.CloseFrom, query.CloseTo);
        }

        // Count and value of every column in one grouped query, then the cards of each column (each on its own
        // context, run together).
        await using var totalsDb = await factory.CreateDbContextAsync(cancellationToken);
        var totalsTask = Base(totalsDb)
            .GroupBy(o => o.StageId)
            .Select(g => new { StageId = g.Key, Count = g.Count(), Total = g.Sum(o => o.Amount) })
            .ToListAsync(cancellationToken);

        var cardTasks = new List<(StageOption Stage, CrmDbContext Db, Task<List<OpportunityListItem>> Cards)>();
        try
        {
            foreach (var stage in stages)
            {
                var db = await factory.CreateDbContextAsync(cancellationToken);
                var stageId = stage.Id;
                var inStage = Base(db).Where(o => o.StageId == stageId);

                // Open: soonest close first. Won and Lost: most recent first.
                var ordered = stage.IsOpen
                    ? inStage.OrderBy(o => o.CloseDate).ThenBy(o => o.Id)
                    : inStage.OrderByDescending(o => o.CloseDate).ThenByDescending(o => o.Id);

                cardTasks.Add((stage, db, Project(db, ordered.Take(BoardQuery.CardsPerColumn)).ToListAsync(cancellationToken)));
            }

            await Task.WhenAll(cardTasks.Select(t => (Task)t.Cards).Append(totalsTask));
        }
        finally
        {
            foreach (var (_, db, _) in cardTasks)
            {
                await db.DisposeAsync();
            }
        }

        var totals = totalsTask.Result.ToDictionary(t => t.StageId);
        return
        [
            .. cardTasks.Select(t => new BoardColumn(
                t.Stage,
                totals.TryGetValue(t.Stage.Id, out var total) ? total.Count : 0,
                total?.Total ?? 0m,
                t.Cards.Result)),
        ];
    }

    // ---- Account tab (P9) ----

    public async Task<IReadOnlyList<OpportunityListItem>> ListForAccountAsync(
        int accountId, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var rows = await Project(db, db.Opportunities.AsNoTracking().VisibleTo(user)
                .Where(o => o.AccountId == accountId)
                .OrderBy(o => o.CloseDate).ThenBy(o => o.Id)
                .Take(MaxAccountTabRows))
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Where(r => r.IsOpen),
            .. rows.Where(r => !r.IsOpen).OrderByDescending(r => r.CloseDate).ThenByDescending(r => r.Id),
        ];
    }

    public async Task<IReadOnlyList<ContactOption>> GetContactOptionsAsync(
        int accountId, int? currentContactId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        // The current contact is kept in the list even if they were deleted since (the query filter hides them),
        // so an old record never shows a blank picker.
        var rows = await db.Contacts.AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new { c.Id, c.FirstName, c.LastName })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(c => new ContactOption(c.Id, FullName(c.FirstName, c.LastName)))];
    }

    private static string FullName(string? first, string last) =>
        string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrEmpty(s)));

    // ---- Detail (P16) ----

    public async Task<OpportunityDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var row = await db.Opportunities.AsNoTracking()
            .VisibleTo(user)
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Opportunity = o,
                AccountName = o.Account.Name,
                StageName = o.Stage.Name,
                o.Stage.IsWon,
                o.Stage.IsLost,
                ContactFirst = o.PrimaryContact != null ? o.PrimaryContact.FirstName : null,
                ContactLast = o.PrimaryContact != null ? o.PrimaryContact.LastName : null,
                LostReasonName = o.LostReason != null ? o.LostReason.Name : null,
                OwnerName = db.Users.Where(u => u.Id == o.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                OwnerIsActive = db.Users.Where(u => u.Id == o.OwnerId).Select(u => u.IsActive).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var o = row.Opportunity;
        var contactName = row.ContactLast is null ? null : FullName(row.ContactFirst, row.ContactLast);
        return new OpportunityDetail(
            o.Id, o.Name, o.AccountId, row.AccountName, o.PrimaryContactId, contactName, o.StageId, row.StageName,
            row.IsWon, row.IsLost, o.Amount, o.Currency, o.Probability, o.ProbabilityOverridden, o.CloseDate,
            o.ClosedAt, o.LostReasonId, row.LostReasonName, o.OwnerId, row.OwnerName, row.OwnerIsActive,
            o.CreatedAt, o.UpdatedAt, o.RowVersion);
    }

    public async Task<OpportunityEditModel> NewAsync(
        UserContext user, int? accountId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var first = (await LoadStagesAsync(db, cancellationToken)).FirstOrDefault(s => s.IsOpen);
        return new OpportunityEditModel
        {
            AccountId = accountId,
            StageId = first?.Id,
            Probability = first?.DefaultProbability ?? 0m,
            CloseDate = OpportunityDefaults.CloseDate(timeProvider.GetUtcNow()),
            OwnerId = user.UserId,
        };
    }

    // ---- Save ----

    public async Task<SaveResult> SaveAsync(
        OpportunityEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SaveOptions();
        var errors = new Dictionary<string, string>();

        model.Name = model.Name?.Trim() ?? string.Empty;

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
            errors.TryAdd(nameof(OpportunityEditModel.Name), "Name is required.");
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var isNew = model.Id == 0;
        Opportunity? entity = null;
        if (!isNew)
        {
            entity = await db.Opportunities.VisibleTo(user).FirstOrDefaultAsync(o => o.Id == model.Id, cancellationToken);
            if (entity is null)
            {
                return new SaveResult(SaveStatus.NotFound);
            }
        }

        var allowed = (await owners.GetAssignableAsync(user, entity?.OwnerId, cancellationToken)).Select(o => o.Id).ToHashSet();
        if (!allowed.Contains(model.OwnerId!))
        {
            errors[nameof(OpportunityEditModel.OwnerId)] = "You cannot assign this owner.";
        }

        var accountId = model.AccountId!.Value;
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            errors[nameof(OpportunityEditModel.AccountId)] = "Choose a valid account.";
        }

        // The primary contact must belong to the chosen account.
        if (model.PrimaryContactId is { } contactId
            && !await db.Contacts.AnyAsync(c => c.Id == contactId && c.AccountId == accountId, cancellationToken))
        {
            errors[nameof(OpportunityEditModel.PrimaryContactId)] = "The contact must belong to the chosen account.";
        }

        var stageId = model.StageId!.Value;
        var stage = await db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId, cancellationToken);
        if (isNew)
        {
            if (stage is null || !stage.IsActive || stage.IsWon || stage.IsLost)
            {
                errors[nameof(OpportunityEditModel.StageId)] = "Choose an open stage.";
            }
        }
        else if (stageId != entity!.StageId)
        {
            errors[nameof(OpportunityEditModel.StageId)] = "Change the stage with the stage stepper.";
        }

        // A Lost opportunity keeps a valid reason.
        var currentStage = isNew ? stage : await db.Stages.AsNoTracking().FirstAsync(s => s.Id == entity!.StageId, cancellationToken);
        if (currentStage is { IsLost: true })
        {
            var currentReasonId = entity?.LostReasonId;
            if (model.LostReasonId is not { } reasonId)
            {
                errors[nameof(OpportunityEditModel.LostReasonId)] = "A lost reason is required.";
            }
            else if (!await db.LostReasons.AnyAsync(r => r.Id == reasonId && (r.IsActive || r.Id == currentReasonId), cancellationToken))
            {
                errors[nameof(OpportunityEditModel.LostReasonId)] = "Choose a valid lost reason.";
            }
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (isNew)
        {
            entity = new Opportunity { StageId = stageId, Currency = "EUR" };
            db.Opportunities.Add(entity);
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

        // Editing Probability marks it as overridden, so later stage moves keep it; typing the stage's own default
        // back clears the mark.
        if (isNew || model.Probability != entity!.Probability)
        {
            entity!.ProbabilityOverridden = model.Probability != currentStage!.DefaultProbability;
        }

        entity!.Name = model.Name;
        entity.AccountId = accountId;
        entity.PrimaryContactId = model.PrimaryContactId;
        entity.Amount = model.Amount;
        entity.Probability = model.Probability;
        entity.CloseDate = model.CloseDate!.Value;
        entity.OwnerId = model.OwnerId!;
        if (currentStage is { IsLost: true })
        {
            entity.LostReasonId = model.LostReasonId;
        }

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

    private static SaveResult Invalid(Dictionary<string, string> errors) => new(SaveStatus.Invalid, FieldErrors: errors);

    // ---- Delete ----

    public async Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var opportunity = await db.Opportunities.VisibleTo(user).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (opportunity is null)
        {
            return false;
        }

        opportunity.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Move stage (P15 drag, P16 stepper) ----

    public async Task<MoveStageResult> MoveStageAsync(
        MoveStageRequest request, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var opportunity = await db.Opportunities.VisibleTo(user)
            .FirstOrDefaultAsync(o => o.Id == request.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            return new MoveStageResult(MoveStageStatus.NotFound);
        }

        // The row changed since the caller loaded it: refuse before looking at anything else.
        if (!opportunity.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
        {
            return await ConflictResultAsync(request.OpportunityId, cancellationToken);
        }

        var from = await db.Stages.AsNoTracking().FirstAsync(s => s.Id == opportunity.StageId, cancellationToken);
        var to = await db.Stages.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StageId && s.IsActive, cancellationToken);
        if (to is null)
        {
            return new MoveStageResult(MoveStageStatus.Invalid, Message: "Choose a valid stage.");
        }

        var reasonExists = request.LostReasonId is { } reasonId
            && await db.LostReasons.AnyAsync(r => r.Id == reasonId && r.IsActive, cancellationToken);

        // Same stage: nothing to do.
        if (from.Id == to.Id)
        {
            return new MoveStageResult(MoveStageStatus.Moved, ToMoved(opportunity));
        }

        var status = OpportunityRules.ApplyMove(opportunity, from, to, request, reasonExists, timeProvider.GetUtcNow());
        if (status != MoveStageStatus.Moved)
        {
            return new MoveStageResult(status, Message: status switch
            {
                MoveStageStatus.NeedsLostReason => "A lost reason is required.",
                MoveStageStatus.NeedsReopenConfirm => "Reopening clears the close date and the lost reason. Confirm to continue.",
                _ => "The close date cannot be in the future, and the lost reason must be valid.",
            });
        }

        db.Entry(opportunity).Property(e => e.RowVersion).OriginalValue = request.RowVersion;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictResultAsync(request.OpportunityId, cancellationToken);
        }

        return new MoveStageResult(MoveStageStatus.Moved, ToMoved(opportunity));
    }

    private static MovedOpportunity ToMoved(Opportunity o) =>
        new(o.StageId, o.Probability, o.ClosedAt, o.CloseDate, o.LostReasonId, o.RowVersion);

    private async Task<MoveStageResult> ConflictResultAsync(int id, CancellationToken cancellationToken)
    {
        var result = await ConflictAsync(id, cancellationToken);
        return result.Status == SaveStatus.NotFound
            ? new MoveStageResult(MoveStageStatus.NotFound)
            : new MoveStageResult(
                MoveStageStatus.Conflict, Message: "Someone changed this opportunity. Reload to see the latest.", Conflict: result.Conflict);
    }

    private async Task<SaveResult> ConflictAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var current = await db.Opportunities.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.UpdatedAt,
                ChangedBy = db.Users.Where(u => u.Id == o.UpdatedBy).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Deleted by someone else in the meantime: nothing left to conflict with.
        return current is null
            ? new SaveResult(SaveStatus.NotFound)
            : new SaveResult(SaveStatus.Conflict, id, Conflict: new ConcurrencyConflict(current.ChangedBy, current.UpdatedAt));
    }
}
