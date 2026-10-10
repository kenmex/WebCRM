using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// OpportunityService against real SQL Server: the stage rules of MoveStageAsync (each rule, and the row-version
/// conflict) and the list totals, which are sums in SQL over the whole filtered set.
/// </summary>
public class OpportunityServiceSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Alice = "op-alice";
    private const string Bob = "op-bob";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly OpportunityService _service;

    // Every test uses its own prefix, so tests sharing the database never see each other's rows.
    private readonly string _prefix = "O" + Guid.NewGuid().ToString("N")[..8] + " ";

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);

    private int _prospecting;
    private int _qualification;
    private int _won;
    private int _lost;
    private int _reasonId;
    private int _accountId;

    public OpportunityServiceSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _service = new OpportunityService(_factory, new OwnerService(_factory), TimeProvider.System);
    }

    // Tests of this class run in parallel on one database: the shared rows are inserted by one test at a time.
    private static readonly SemaphoreSlim SharedSetup = new(1, 1);

    private bool _ready;

    private async Task EnsureReferenceDataAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);
        if (_ready)
        {
            return;
        }

        await using var db = _factory.CreateDbContext();
        await SharedSetup.WaitAsync(Ct);
        try
        {
            if (!await db.Users.AnyAsync(u => u.Id == Alice, Ct))
            {
                db.Users.AddRange(User(Alice, "Alice"), User(Bob, "Bob"), User("system-test", "System test"));
                await db.SaveChangesAsync(Ct);
            }

            if (!await db.AccountStatuses.AnyAsync(Ct))
            {
                db.AccountStatuses.Add(new AccountStatus { Name = "OP Active", SortOrder = 10 });
            }

            if (!await db.LostReasons.AnyAsync(r => r.Name == "OP Price", Ct))
            {
                db.LostReasons.Add(new LostReason { Name = "OP Price", SortOrder = 10 });
            }

            await db.SaveChangesAsync(Ct);
        }
        finally
        {
            SharedSetup.Release();
        }

        var stages = await db.Stages.AsNoTracking().ToListAsync(Ct);
        _prospecting = stages.Single(s => s.Name == "Prospecting").Id;
        _qualification = stages.Single(s => s.Name == "Qualification").Id;
        _won = stages.Single(s => s.IsWon).Id;
        _lost = stages.Single(s => s.IsLost).Id;
        _reasonId = await db.LostReasons.Where(r => r.Name == "OP Price").Select(r => r.Id).SingleAsync(Ct);

        var statusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct);
        var account = new Account { Name = _prefix + "Acct", AccountStatusId = statusId, OwnerId = Alice };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        _accountId = account.Id;
        _ready = true;
    }

    private static User User(string id, string name) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        DisplayName = name,
    };

    private async Task<Opportunity> AddAsync(
        string suffix, int? stageId = null, decimal amount = 1000m, decimal probability = 10m,
        bool overridden = false, DateOnly? closeDate = null, string owner = Alice, bool active = true)
    {
        await EnsureReferenceDataAsync();
        await using var db = _factory.CreateDbContext();
        var opportunity = new Opportunity
        {
            Name = _prefix + suffix,
            AccountId = _accountId,
            StageId = stageId ?? _prospecting,
            Amount = amount,
            Probability = probability,
            ProbabilityOverridden = overridden,
            CloseDate = closeDate ?? new DateOnly(2030, 1, 1),
            OwnerId = owner,
            IsActive = active,
        };
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync(Ct);
        return opportunity;
    }

    private async Task<Opportunity> ReloadAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Opportunities.AsNoTracking().SingleAsync(o => o.Id == id, Ct);
    }

    private static MoveStageRequest Move(Opportunity o, int stageId, int? reason = null, DateOnly? closedOn = null, bool confirm = false) =>
        new(o.Id, stageId, o.RowVersion, reason, closedOn, confirm);

    // ---- MoveStageAsync ----

    [Fact]
    public async Task Move_to_an_open_stage_resets_probability_to_the_stage_default()
    {
        var o = await AddAsync("reset", probability: 10m);

        var result = await _service.MoveStageAsync(Move(o, _qualification), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        result.Moved!.Probability.ShouldBe(20m);
        var saved = await ReloadAsync(o.Id);
        saved.StageId.ShouldBe(_qualification);
        saved.Probability.ShouldBe(20m);
        saved.ClosedAt.ShouldBeNull();
        saved.UpdatedBy.ShouldNotBeNull();
    }

    [Fact]
    public async Task Move_keeps_an_overridden_probability()
    {
        var o = await AddAsync("override", probability: 33m, overridden: true);

        var result = await _service.MoveStageAsync(Move(o, _qualification), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        (await ReloadAsync(o.Id)).Probability.ShouldBe(33m);
    }

    [Fact]
    public async Task Move_to_Won_stamps_ClosedAt_and_sets_the_close_date_to_today()
    {
        var o = await AddAsync("won");
        var before = DateTime.UtcNow.AddSeconds(-5);

        var result = await _service.MoveStageAsync(Move(o, _won), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        var saved = await ReloadAsync(o.Id);
        saved.ClosedAt.ShouldNotBeNull().ShouldBeGreaterThan(before);
        saved.Probability.ShouldBe(100m);
        saved.LostReasonId.ShouldBeNull();
        var today = DateOnly.FromDateTime(WebCRM.Core.TimeDisplay.ToLocal(DateTime.UtcNow));
        saved.CloseDate.ShouldBe(today);
    }

    [Fact]
    public async Task Move_to_Won_with_a_chosen_date_sets_both_ClosedAt_and_CloseDate()
    {
        var o = await AddAsync("won-date");
        var yesterday = DateOnly.FromDateTime(WebCRM.Core.TimeDisplay.ToLocal(DateTime.UtcNow)).AddDays(-1);

        var result = await _service.MoveStageAsync(Move(o, _won, closedOn: yesterday), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        var saved = await ReloadAsync(o.Id);
        saved.CloseDate.ShouldBe(yesterday);
        DateOnly.FromDateTime(WebCRM.Core.TimeDisplay.ToLocal(saved.ClosedAt!.Value)).ShouldBe(yesterday);
    }

    [Fact]
    public async Task Move_to_Won_with_a_future_date_is_invalid_and_changes_nothing()
    {
        var o = await AddAsync("won-future");
        var tomorrow = DateOnly.FromDateTime(WebCRM.Core.TimeDisplay.ToLocal(DateTime.UtcNow)).AddDays(2);

        var result = await _service.MoveStageAsync(Move(o, _won, closedOn: tomorrow), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Invalid);
        (await ReloadAsync(o.Id)).StageId.ShouldBe(_prospecting);
    }

    [Fact]
    public async Task Move_to_Lost_without_a_reason_is_refused_and_changes_nothing()
    {
        var o = await AddAsync("lost-no-reason");

        var result = await _service.MoveStageAsync(Move(o, _lost), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.NeedsLostReason);
        var saved = await ReloadAsync(o.Id);
        saved.StageId.ShouldBe(_prospecting);
        saved.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Move_to_Lost_with_a_reason_stamps_ClosedAt_and_stores_the_reason()
    {
        var o = await AddAsync("lost");
        await EnsureReferenceDataAsync();

        var result = await _service.MoveStageAsync(Move(o, _lost, reason: _reasonId), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        var saved = await ReloadAsync(o.Id);
        saved.ClosedAt.ShouldNotBeNull();
        saved.LostReasonId.ShouldBe(_reasonId);
        saved.Probability.ShouldBe(0m);
    }

    [Fact]
    public async Task Move_to_Lost_with_an_unknown_reason_is_invalid()
    {
        var o = await AddAsync("lost-bad-reason");

        var result = await _service.MoveStageAsync(Move(o, _lost, reason: int.MaxValue), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Invalid);
    }

    [Fact]
    public async Task Reopening_needs_confirmation_and_then_clears_ClosedAt_and_the_reason()
    {
        var o = await AddAsync("reopen");
        await EnsureReferenceDataAsync();
        var lost = await _service.MoveStageAsync(Move(o, _lost, reason: _reasonId), _alice, Ct);
        var version = lost.Moved!.RowVersion;

        var refused = await _service.MoveStageAsync(new MoveStageRequest(o.Id, _qualification, version), _alice, Ct);
        refused.Status.ShouldBe(MoveStageStatus.NeedsReopenConfirm);
        (await ReloadAsync(o.Id)).StageId.ShouldBe(_lost);

        var confirmed = await _service.MoveStageAsync(
            new MoveStageRequest(o.Id, _qualification, version, ConfirmReopen: true), _alice, Ct);
        confirmed.Status.ShouldBe(MoveStageStatus.Moved);
        var saved = await ReloadAsync(o.Id);
        saved.StageId.ShouldBe(_qualification);
        saved.ClosedAt.ShouldBeNull();
        saved.LostReasonId.ShouldBeNull();
        saved.Probability.ShouldBe(20m);
    }

    [Fact]
    public async Task Moving_from_Won_to_Lost_needs_no_confirmation()
    {
        var o = await AddAsync("won-to-lost");
        await EnsureReferenceDataAsync();
        var won = await _service.MoveStageAsync(Move(o, _won), _alice, Ct);

        var result = await _service.MoveStageAsync(
            new MoveStageRequest(o.Id, _lost, won.Moved!.RowVersion, _reasonId), _alice, Ct);

        result.Status.ShouldBe(MoveStageStatus.Moved);
        (await ReloadAsync(o.Id)).LostReasonId.ShouldBe(_reasonId);
    }

    [Fact]
    public async Task Move_with_a_stale_row_version_is_a_conflict_and_changes_nothing()
    {
        var o = await AddAsync("conflict");

        // Someone else moves it first.
        var first = await _service.MoveStageAsync(Move(o, _qualification), _alice, Ct);
        first.Status.ShouldBe(MoveStageStatus.Moved);

        // Our copy still has the old version.
        var stale = await _service.MoveStageAsync(Move(o, _won), _alice, Ct);

        stale.Status.ShouldBe(MoveStageStatus.Conflict);
        stale.Conflict.ShouldNotBeNull();
        (await ReloadAsync(o.Id)).StageId.ShouldBe(_qualification);
    }

    [Fact]
    public async Task Two_concurrent_moves_from_the_same_version_have_exactly_one_winner()
    {
        var o = await AddAsync("race");

        var results = await Task.WhenAll(
            _service.MoveStageAsync(Move(o, _qualification), _alice, Ct),
            _service.MoveStageAsync(Move(o, _won), _alice, Ct));

        results.Count(r => r.Status == MoveStageStatus.Moved).ShouldBe(1);
        results.Count(r => r.Status == MoveStageStatus.Conflict).ShouldBe(1);
    }

    [Fact]
    public async Task Move_of_a_missing_or_deleted_opportunity_is_NotFound()
    {
        var o = await AddAsync("deleted", active: false);

        (await _service.MoveStageAsync(Move(o, _qualification), _alice, Ct)).Status.ShouldBe(MoveStageStatus.NotFound);
    }

    // ---- Totals and list ----

    private OpportunityQuery Query(OpportunityOutcome outcome = OpportunityOutcome.Open, int? stageId = null) =>
        new(Scope: ListScope.All, Search: _prefix, StageId: stageId, Outcome: outcome);

    [Fact]
    public async Task Totals_cover_the_whole_filtered_set_not_just_the_page()
    {
        await AddAsync("a", amount: 1000m, probability: 10m);
        await AddAsync("b", amount: 2000m, probability: 50m);
        await AddAsync("c", amount: 333.33m, probability: 33m);

        var query = Query() with { PageSize = 1 };
        var page = await _service.SearchAsync(query, _alice, Ct);
        var totals = await _service.GetTotalsAsync(query, _alice, Ct);

        page.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBe(3);
        totals.Amount.ShouldBe(3333.33m);
        // 100.00 + 1000.00 + round(110.0, 2) = 110.00
        totals.Weighted.ShouldBe(1210.00m);
    }

    [Fact]
    public async Task Totals_equal_the_sum_of_the_rounded_Weighted_column()
    {
        await AddAsync("r1", amount: 100.01m, probability: 33.33m);
        await AddAsync("r2", amount: 200.05m, probability: 12.5m);

        var query = Query();
        var items = (await _service.SearchAsync(query, _alice, Ct)).Items;
        var totals = await _service.GetTotalsAsync(query, _alice, Ct);

        totals.Weighted.ShouldBe(items.Sum(i => i.Weighted));
    }

    [Fact]
    public async Task Totals_respect_the_outcome_stage_owner_and_date_filters_and_skip_deleted_rows()
    {
        await AddAsync("open1", amount: 100m, probability: 50m, closeDate: new DateOnly(2030, 3, 1));
        await AddAsync("open2", stageId: _qualification, amount: 200m, probability: 50m, closeDate: new DateOnly(2030, 6, 1), owner: Bob);
        await AddAsync("won1", stageId: _won, amount: 400m, probability: 100m);
        await AddAsync("gone", amount: 9999m, active: false);

        (await _service.GetTotalsAsync(Query(), _alice, Ct)).Amount.ShouldBe(300m);
        (await _service.GetTotalsAsync(Query(OpportunityOutcome.Won), _alice, Ct)).Amount.ShouldBe(400m);
        (await _service.GetTotalsAsync(Query(OpportunityOutcome.Any), _alice, Ct)).Amount.ShouldBe(700m);
        (await _service.GetTotalsAsync(Query(stageId: _qualification), _alice, Ct)).Amount.ShouldBe(200m);
        (await _service.GetTotalsAsync(Query() with { OwnerId = Bob }, _alice, Ct)).Amount.ShouldBe(200m);
        (await _service.GetTotalsAsync(
            Query() with { CloseFrom = new DateOnly(2030, 2, 1), CloseTo = new DateOnly(2030, 4, 1) }, _alice, Ct))
            .Amount.ShouldBe(100m);
        (await _service.GetTotalsAsync(Query() with { Scope = ListScope.Mine }, _alice, Ct)).Amount.ShouldBe(100m);
    }

    [Fact]
    public async Task Totals_of_an_empty_set_are_zero()
    {
        await EnsureReferenceDataAsync();

        var totals = await _service.GetTotalsAsync(Query(), _alice, Ct);

        totals.ShouldBe(new OpportunityTotals(0m, 0m));
    }

    // ---- Board ----

    [Fact]
    public async Task Board_has_a_column_per_stage_with_count_value_and_capped_cards()
    {
        await AddAsync("b1", amount: 100m, closeDate: new DateOnly(2030, 2, 1));
        await AddAsync("b2", amount: 250m, closeDate: new DateOnly(2030, 1, 1));
        await AddAsync("b3", stageId: _won, amount: 500m);

        var board = await _service.GetBoardAsync(new BoardQuery(ListScope.All, Search: _prefix), _alice, Ct);

        board.Select(c => c.Stage.Id).Last().ShouldBe(_lost);
        board.Select(c => c.Stage.IsWon).SkipLast(1).Last().ShouldBeTrue();
        var prospecting = board.Single(c => c.Stage.Id == _prospecting);
        prospecting.Count.ShouldBe(2);
        prospecting.Total.ShouldBe(350m);
        prospecting.Cards.Select(c => c.Name).ShouldBe([_prefix + "b2", _prefix + "b1"]);
        board.Single(c => c.Stage.Id == _won).Total.ShouldBe(500m);
        board.Single(c => c.Stage.Id == _lost).Count.ShouldBe(0);
    }

    // ---- Save ----

    [Fact]
    public async Task Save_rejects_a_primary_contact_from_another_account()
    {
        await EnsureReferenceDataAsync();
        int contactId;
        await using (var db = _factory.CreateDbContext())
        {
            var statusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct);
            var other = new Account { Name = _prefix + "Other", AccountStatusId = statusId, OwnerId = Alice };
            other.Contacts.Add(new Contact { LastName = _prefix + "Stranger", OwnerId = Alice });
            db.Accounts.Add(other);
            await db.SaveChangesAsync(Ct);
            contactId = other.Contacts.Single().Id;
        }

        var model = await _service.NewAsync(_alice, _accountId, Ct);
        model.Name = _prefix + "with contact";
        model.PrimaryContactId = contactId;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(OpportunityEditModel.PrimaryContactId));
    }

    [Fact]
    public async Task Save_creates_with_the_first_open_stage_and_marks_an_edited_probability_as_overridden()
    {
        await EnsureReferenceDataAsync();
        var model = await _service.NewAsync(_alice, _accountId, Ct);
        model.Name = "  " + _prefix + "new  ";
        model.Probability = 42m;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await ReloadAsync(result.Id);
        saved.Name.ShouldBe(_prefix + "new");
        saved.StageId.ShouldBe(_prospecting);
        saved.ProbabilityOverridden.ShouldBeTrue();
        saved.Probability.ShouldBe(42m);
        saved.OwnerId.ShouldBe(Alice);
    }

    [Fact]
    public async Task Save_with_a_stale_row_version_is_a_conflict()
    {
        var o = await AddAsync("save-conflict");
        var detail = (await _service.GetAsync(o.Id, _alice, Ct))!;
        var stale = detail.ToEditModel();

        var fresh = detail.ToEditModel();
        fresh.Name = _prefix + "changed by someone";
        (await _service.SaveAsync(fresh, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        stale.Name = _prefix + "my change";
        var result = await _service.SaveAsync(stale, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Conflict);
    }

    [Fact]
    public async Task Save_cannot_change_the_stage_of_an_existing_opportunity()
    {
        var o = await AddAsync("stage-locked");
        var model = (await _service.GetAsync(o.Id, _alice, Ct))!.ToEditModel();
        model.StageId = _qualification;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(OpportunityEditModel.StageId));
    }
}
