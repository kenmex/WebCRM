using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// AccountService against real SQL Server. The in-memory tests cannot catch LINQ that EF cannot translate,
/// collation behaviour, unique indexes or rowversion; these can.
/// </summary>
public class AccountServiceSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Alice = "it-alice";
    private const string Bob = "it-bob";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly AccountService _service;

    // Every test uses its own prefix, so tests sharing the database never see each other's rows.
    private readonly string _prefix = "T" + Guid.NewGuid().ToString("N")[..8] + " ";

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);
    private readonly UserContext _admin = new("it-admin", RoleNames.Admin, TeamId: null);

    public AccountServiceSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _service = new AccountService(_factory, new OwnerService(_factory));
    }

    private async Task EnsureReferenceDataAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        await using var db = _factory.CreateDbContext();
        if (await db.Users.AnyAsync(u => u.Id == Alice, Ct))
        {
            return;
        }

        // Users first: every audited insert (the team too) needs its CreatedBy user to exist.
        var alice = User(Alice, "Alice");
        var bob = User(Bob, "Bob");
        db.Users.AddRange(alice, bob, User("it-admin", "Admin"), User("system-test", "System test"));
        await db.SaveChangesAsync(Ct);

        var team = new Team { Name = "IT team" };
        db.Teams.Add(team);
        await db.SaveChangesAsync(Ct);
        alice.TeamId = bob.TeamId = team.Id;
        db.AccountStatuses.AddRange(
            new AccountStatus { Name = "IT Prospect", SortOrder = 10 },
            new AccountStatus { Name = "IT Active", SortOrder = 20 });
        db.Industries.Add(new Industry { Name = "IT Retail", SortOrder = 10 });
        db.ActivityTypes.Add(new ActivityType { Name = "IT Call" });
        await db.SaveChangesAsync(Ct);
    }

    private static User User(string id, string name, int? teamId = null) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        DisplayName = name,
        TeamId = teamId,
    };

    private async Task<(int StatusId, int IndustryId)> LookupIdsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return (await db.AccountStatuses.Where(s => s.Name == "IT Active").Select(s => s.Id).SingleAsync(Ct),
            await db.Industries.Where(i => i.Name == "IT Retail").Select(i => i.Id).SingleAsync(Ct));
    }

    private async Task<int> AddAccountAsync(string name, string owner = Alice, string? city = null)
    {
        var (statusId, industryId) = await LookupIdsAsync();
        await using var db = _factory.CreateDbContext();
        var account = new Account
        {
            Name = _prefix + name,
            OwnerId = owner,
            AccountStatusId = statusId,
            IndustryId = industryId,
        };
        if (city is not null)
        {
            account.Addresses.Add(new Address { AddressType = AddressType.Billing, City = city });
        }

        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    private async Task<AccountEditModel> FormAsync(string name)
    {
        var (statusId, _) = await LookupIdsAsync();
        return new AccountEditModel { Name = _prefix + name, AccountStatusId = statusId, OwnerId = Alice };
    }

    // ---- The list query ----

    [Fact]
    public async Task Search_translates_and_runs_for_every_sort_in_both_directions()
    {
        await EnsureReferenceDataAsync();
        var a = await AddAccountAsync("Alpha", city: "Athens");
        await AddAccountAsync("Bravo", Bob, city: "Patras");

        await using (var db = _factory.CreateDbContext())
        {
            var open = await db.Stages.FirstAsync(s => !s.IsWon && !s.IsLost, Ct);
            var call = await db.ActivityTypes.SingleAsync(t => t.Name == "IT Call", Ct);
            db.Opportunities.Add(new Opportunity { Name = "Deal", AccountId = a, StageId = open.Id, OwnerId = Alice });
            db.Activities.Add(new Activity
            {
                Subject = "Ring",
                AccountId = a,
                ActivityTypeId = call.Id,
                OwnerId = Alice,
                DoneAt = DateTime.UtcNow.AddDays(-2),
            });
            await db.SaveChangesAsync(Ct);
        }

        foreach (var sort in Enum.GetValues<AccountSort>())
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await _service.SearchAsync(
                    new AccountQuery(ListScope.All, Search: _prefix, Sort: sort, Descending: descending), _alice, Ct);

                result.TotalCount.ShouldBe(2, $"sort {sort} descending={descending}");
            }
        }

        var row = (await _service.SearchAsync(new AccountQuery(ListScope.All, Search: _prefix + "Alpha"), _alice, Ct)).Items.Single();
        row.City.ShouldBe("Athens");
        row.OwnerName.ShouldBe("Alice");
        row.OpenOpportunities.ShouldBe(1);
        row.LastActivityAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Search_runs_every_scope_and_filter()
    {
        await EnsureReferenceDataAsync();
        await AddAccountAsync("Alpha", city: "Athens");
        var (statusId, industryId) = await LookupIdsAsync();
        var teamUser = new UserContext(Alice, RoleNames.Manager, TeamId: await TeamIdAsync());

        foreach (var scope in Enum.GetValues<ListScope>())
        {
            var query = new AccountQuery(
                scope, Search: _prefix, StatusId: statusId, IndustryId: industryId, OwnerId: Alice, City: "Ath");
            var result = await _service.SearchAsync(query, teamUser, Ct);

            result.TotalCount.ShouldBe(1, $"scope {scope}");
        }
    }

    [Fact]
    public async Task Search_pages_on_the_server()
    {
        await EnsureReferenceDataAsync();
        for (var i = 1; i <= 5; i++)
        {
            await AddAccountAsync($"Page {i}");
        }

        var page = await _service.SearchAsync(
            new AccountQuery(ListScope.All, Search: _prefix, Page: 2, PageSize: 2), _alice, Ct);

        page.TotalCount.ShouldBe(5);
        page.Items.Select(i => i.Name).ShouldBe([_prefix + "Page 3", _prefix + "Page 4"]);
    }

    [Fact]
    public async Task Search_is_accent_and_case_insensitive()
    {
        await EnsureReferenceDataAsync();
        await AddAccountAsync("Αθήνα Συμβουλευτική", city: "Αθήνα");

        // D3: "Αθηνα" (no accent, different case) must find "Αθήνα".
        var byName = await _service.SearchAsync(new AccountQuery(ListScope.All, Search: "αθηνα συμβ"), _alice, Ct);
        var byCity = await _service.SearchAsync(new AccountQuery(ListScope.All, City: "ΑΘΗΝΑ", Search: _prefix), _alice, Ct);

        byName.Items.Select(i => i.Name).ShouldContain(_prefix + "Αθήνα Συμβουλευτική");
        byCity.TotalCount.ShouldBe(1);
    }

    // ---- Save, delete and the constraints only SQL Server enforces ----

    [Fact]
    public async Task Save_and_get_round_trip_through_sql()
    {
        await EnsureReferenceDataAsync();
        var model = await FormAsync("Round trip");
        model.VatNumber = "DE" + Random.Shared.NextInt64(100_000_000, 999_999_999);

        var saved = await _service.SaveAsync(model, _alice, cancellationToken: Ct);
        saved.Status.ShouldBe(AccountSaveStatus.Saved);

        var detail = await _service.GetAsync(saved.Id, _alice, Ct);
        detail!.Name.ShouldBe(model.Name);
        detail.RowVersion.ShouldNotBeEmpty();
        (await _service.NewAsync(_alice, Ct)).AccountStatusId.ShouldNotBeNull();
        (await _service.GetDeleteImpactAsync(saved.Id, _alice, Ct)).ShouldBe(new AccountDeleteImpact(0, 0));
    }

    [Fact]
    public async Task Duplicate_names_are_rejected_by_the_service_and_by_the_unique_index()
    {
        await EnsureReferenceDataAsync();
        var model = await FormAsync("Unique");
        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(AccountSaveStatus.Saved);

        // Same name, different case: the service catches it.
        var again = await FormAsync("UNIQUE");
        (await _service.SaveAsync(again, _alice, cancellationToken: Ct)).Status.ShouldBe(AccountSaveStatus.Invalid);

        // And the database refuses it even when the service is bypassed.
        await using var db = _factory.CreateDbContext();
        db.Accounts.Add(new Account
        {
            Name = model.Name,
            OwnerId = Alice,
            AccountStatusId = model.AccountStatusId!.Value,
        });
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_deleted_accounts_name_can_be_reused()
    {
        await EnsureReferenceDataAsync();
        var id = await AddAccountAsync("Reusable");

        (await _service.DeleteAsync(id, _alice, Ct)).Status.ShouldBe(AccountDeleteStatus.Deleted);

        (await _service.SaveAsync(await FormAsync("Reusable"), _alice, cancellationToken: Ct))
            .Status.ShouldBe(AccountSaveStatus.Saved);
    }

    [Fact]
    public async Task A_stale_row_version_gives_a_conflict_naming_the_other_user()
    {
        await EnsureReferenceDataAsync();
        var id = await AddAccountAsync("Contested");
        var stale = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();

        // Bob saves first.
        _sql.CurrentUser.UserId = Bob;
        try
        {
            var bobs = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
            bobs.Phone = "111";
            (await _service.SaveAsync(bobs, _admin, cancellationToken: Ct)).Status.ShouldBe(AccountSaveStatus.Saved);
        }
        finally
        {
            _sql.CurrentUser.UserId = "system-test";
        }

        stale.Phone = "222";
        var result = await _service.SaveAsync(stale, _admin, cancellationToken: Ct);

        result.Status.ShouldBe(AccountSaveStatus.Conflict);
        result.Conflict!.ChangedBy.ShouldBe("Bob");
        result.Conflict.ChangedAtUtc.ShouldNotBeNull();

        // An Admin may overwrite, a Sales user may not.
        (await _service.SaveAsync(stale.Clone(), _alice, new AccountSaveOptions { Overwrite = true }, Ct))
            .Status.ShouldBe(AccountSaveStatus.Conflict);
        (await _service.SaveAsync(stale.Clone(), _admin, new AccountSaveOptions { Overwrite = true }, Ct))
            .Status.ShouldBe(AccountSaveStatus.Saved);
    }

    [Fact]
    public async Task Delete_soft_deletes_the_account_and_its_contacts_and_hides_them()
    {
        await EnsureReferenceDataAsync();
        var id = await AddAccountAsync("Doomed");
        await using (var db = _factory.CreateDbContext())
        {
            db.Contacts.Add(new Contact { LastName = "Smith", AccountId = id, OwnerId = Alice });
            await db.SaveChangesAsync(Ct);
        }

        (await _service.GetDeleteImpactAsync(id, _alice, Ct)).ShouldBe(new AccountDeleteImpact(1, 0));
        (await _service.DeleteAsync(id, _alice, Ct)).Status.ShouldBe(AccountDeleteStatus.Deleted);

        (await _service.GetAsync(id, _alice, Ct)).ShouldBeNull();
        await using var check = _factory.CreateDbContext();
        (await check.Contacts.IgnoreQueryFilters().SingleAsync(c => c.AccountId == id, Ct)).IsActive.ShouldBeFalse();
    }

    private async Task<int> TeamIdAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Teams.Where(t => t.Name == "IT team").Select(t => t.Id).SingleAsync(Ct);
    }
}
