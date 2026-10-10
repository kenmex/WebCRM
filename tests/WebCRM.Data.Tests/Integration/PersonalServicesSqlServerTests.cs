using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Records;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// Favourites, recent views and saved views against real SQL Server: the unique indexes (which EF InMemory ignores)
/// have to hold under concurrent writes, and the caps and visibility rules have to hold on the real tables.
/// </summary>
public class PersonalServicesSqlServerTests : IClassFixture<SqlServerFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly FakeTimeProvider _clock = new(Start);

    // Every test has its own users, so tests sharing the database never see each other's rows.
    private readonly string _token = Guid.NewGuid().ToString("N")[..8];

    private readonly UserContext _alice;
    private readonly UserContext _bob;
    private readonly UserContext _admin;

    public PersonalServicesSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _alice = new UserContext("pa-alice-" + _token, RoleNames.Sales, TeamId: null);
        _bob = new UserContext("pa-bob-" + _token, RoleNames.Sales, TeamId: null);
        _admin = new UserContext("pa-admin-" + _token, RoleNames.Admin, TeamId: null);
    }

    private FavouriteService Favourites => new(_factory, _clock);

    private RecentViewService Recents => new(_factory, _clock);

    private SavedViewService Views => new(_factory);

    private async Task EnsureUsersAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        await using var db = _factory.CreateDbContext();
        foreach (var user in new[] { _alice, _bob, _admin })
        {
            db.Users.Add(new User
            {
                Id = user.UserId,
                UserName = user.UserId,
                NormalizedUserName = user.UserId.ToUpperInvariant(),
                DisplayName = user.UserId,
            });
        }

        if (!await db.AccountStatuses.AnyAsync(Ct))
        {
            db.AccountStatuses.Add(new AccountStatus { Name = "PA Active", SortOrder = 10 });
        }

        if (!await db.Users.AnyAsync(u => u.Id == "system-test", Ct))
        {
            db.Users.Add(new User { Id = "system-test", UserName = "system-test", NormalizedUserName = "SYSTEM-TEST", DisplayName = "System test" });
        }

        await db.SaveChangesAsync(Ct);
    }

    private async Task<List<int>> AddAccountsAsync(int count)
    {
        await using var db = _factory.CreateDbContext();
        var statusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct);
        var accounts = Enumerable.Range(0, count)
            .Select(i => new Account { Name = $"PA {_token} {i:000}", OwnerId = _alice.UserId, AccountStatusId = statusId })
            .ToList();
        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync(Ct);
        return [.. accounts.Select(a => a.Id)];
    }

    // ---- favourites

    [Fact]
    public async Task Starring_the_same_record_many_times_at_once_leaves_one_row_and_every_call_reports_starred()
    {
        await EnsureUsersAsync();
        var id = (await AddAccountsAsync(1))[0];

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Favourites.SetAsync(_alice, SearchEntity.Account, id, true, Ct), Ct)));

        results.ShouldAllBe(r => r);
        await using var db = _factory.CreateDbContext();
        (await db.Favourites.CountAsync(f => f.UserId == _alice.UserId, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Starring_and_un_starring_at_once_ends_with_at_most_one_row()
    {
        await EnsureUsersAsync();
        var id = (await AddAccountsAsync(1))[0];

        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => Task.Run(() => Favourites.SetAsync(_alice, SearchEntity.Account, id, i % 2 == 0, Ct), Ct)));

        await using var db = _factory.CreateDbContext();
        (await db.Favourites.CountAsync(f => f.UserId == _alice.UserId, Ct)).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task A_starred_and_a_recently_viewed_lead_show_up_in_the_lists_with_their_company_and_status()
    {
        await EnsureUsersAsync();
        int leadId;
        await using (var db = _factory.CreateDbContext())
        {
            var lead = new Lead
            {
                Name = $"PA {_token} Lead",
                Company = "Acme",
                OwnerId = _alice.UserId,
                LeadStatusId = await db.LeadStatuses.Where(s => s.SystemCode == LeadStatus.New).Select(s => s.Id).SingleAsync(Ct),
            };
            db.Leads.Add(lead);
            await db.SaveChangesAsync(Ct);
            leadId = lead.Id;
        }

        (await Favourites.SetAsync(_alice, SearchEntity.Lead, leadId, true, Ct)).ShouldBeTrue();
        await Recents.RecordAsync(_alice, SearchEntity.Lead, leadId, Ct);

        var starred = await Favourites.ListAsync(_alice, 10, Ct);
        var recent = await Recents.ListAsync(_alice, 10, Ct);

        starred.ShouldHaveSingleItem().ShouldBe(new SearchHit(SearchEntity.Lead, leadId, $"PA {_token} Lead", "Acme · New"));
        recent.ShouldHaveSingleItem().Id.ShouldBe(leadId);
    }

    [Fact]
    public async Task A_starred_and_a_recently_viewed_opportunity_show_up_with_their_account_and_stage()
    {
        await EnsureUsersAsync();
        var accountId = (await AddAccountsAsync(1))[0];
        int opportunityId;
        await using (var db = _factory.CreateDbContext())
        {
            var opportunity = new Opportunity
            {
                Name = $"PA {_token} Deal",
                AccountId = accountId,
                StageId = await db.Stages.Where(s => s.Name == "Proposal").Select(s => s.Id).SingleAsync(Ct),
                CloseDate = new DateOnly(2030, 1, 1),
                OwnerId = _alice.UserId,
            };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync(Ct);
            opportunityId = opportunity.Id;
        }

        (await Favourites.SetAsync(_alice, SearchEntity.Opportunity, opportunityId, true, Ct)).ShouldBeTrue();
        await Recents.RecordAsync(_alice, SearchEntity.Opportunity, opportunityId, Ct);

        var starred = await Favourites.ListAsync(_alice, 10, Ct);
        var recent = await Recents.ListAsync(_alice, 10, Ct);

        starred.ShouldHaveSingleItem()
            .ShouldBe(new SearchHit(SearchEntity.Opportunity, opportunityId, $"PA {_token} Deal", $"PA {_token} 000 · Proposal"));
        recent.ShouldHaveSingleItem().Id.ShouldBe(opportunityId);
    }

    // ---- recent views

    [Fact]
    public async Task Recent_views_are_trimmed_to_the_last_fifty_on_the_real_table()
    {
        await EnsureUsersAsync();
        var ids = await AddAccountsAsync(PersonalRules.MaxRecentViews + 5);

        foreach (var id in ids)
        {
            _clock.UtcNow = _clock.UtcNow.AddMinutes(2);
            await Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct);
        }

        await using var db = _factory.CreateDbContext();
        var kept = await db.RecentViews.Where(v => v.UserId == _alice.UserId).Select(v => v.EntityId).ToListAsync(Ct);
        kept.Count.ShouldBe(PersonalRules.MaxRecentViews);
        kept.ShouldNotContain(ids[0]); // the oldest went
        kept.ShouldContain(ids[^1]);

        var list = await Recents.ListAsync(_alice, 10, Ct);
        list.Count.ShouldBe(10);
        list[0].Id.ShouldBe(ids[^1]);
    }

    [Fact]
    public async Task Opening_one_record_in_many_tabs_at_once_leaves_one_row()
    {
        await EnsureUsersAsync();
        var id = (await AddAccountsAsync(1))[0];

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct), Ct)));

        await using var db = _factory.CreateDbContext();
        (await db.RecentViews.CountAsync(v => v.UserId == _alice.UserId, Ct)).ShouldBe(1);
    }

    // ---- saved views

    [Fact]
    public async Task Saving_the_same_name_many_times_at_once_creates_one_view_and_refuses_the_rest()
    {
        await EnsureUsersAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Views.SaveAsync(_alice, "accounts", "Same name", "q=a", cancellationToken: Ct), Ct)));

        results.Count(r => r.Status == SaveStatus.Saved).ShouldBe(1);
        results.Count(r => r.Status == SaveStatus.Invalid).ShouldBe(7);
        await using var db = _factory.CreateDbContext();
        (await db.SavedViews.CountAsync(v => v.UserId == _alice.UserId, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task A_name_differing_only_by_case_is_a_duplicate()
    {
        await EnsureUsersAsync();

        (await Views.SaveAsync(_alice, "accounts", "Πελάτες", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        (await Views.SaveAsync(_alice, "accounts", "ΠΕΛΆΤΕΣ", "q=b", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);

        // The same name on another list or for another user is fine.
        (await Views.SaveAsync(_alice, "contacts", "Πελάτες", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        (await Views.SaveAsync(_bob, "accounts", "Πελάτες", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task The_thirty_view_cap_holds_per_user_and_list_on_the_real_table()
    {
        await EnsureUsersAsync();

        for (var i = 0; i < PersonalRules.MaxSavedViewsPerList; i++)
        {
            (await Views.SaveAsync(_alice, "accounts", $"View {i:00}", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        }

        var over = await Views.SaveAsync(_alice, "accounts", "One too many", "q=a", cancellationToken: Ct);
        over.Status.ShouldBe(SaveStatus.Invalid);

        // Replacing an existing view is not a new one, so it still works at the cap.
        (await Views.SaveAsync(_alice, "accounts", "view 00", "q=b", replaceExisting: true, cancellationToken: Ct))
            .Status.ShouldBe(SaveStatus.Saved);
        (await Views.SaveAsync(_alice, "contacts", "Other list", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        await using var db = _factory.CreateDbContext();
        (await db.SavedViews.CountAsync(v => v.UserId == _alice.UserId && v.ListKey == "accounts", Ct))
            .ShouldBe(PersonalRules.MaxSavedViewsPerList);
    }

    [Fact]
    public async Task Private_views_are_seen_only_by_their_owner_and_published_ones_by_everyone_read_only()
    {
        await EnsureUsersAsync();
        var privateView = (await Views.SaveAsync(_alice, "accounts", "Private", "q=a", cancellationToken: Ct)).Id;
        var shared = (await Views.SaveAsync(_admin, "accounts", "Published", "scope=team", cancellationToken: Ct)).Id;

        (await Views.SetPublicAsync(_alice, privateView, true, Ct)).ShouldBeFalse(); // only an Admin publishes
        (await Views.SetPublicAsync(_admin, shared, true, Ct)).ShouldBeTrue();

        var bobSees = await Views.ListAsync(_bob, "accounts", Ct);
        bobSees.Mine.ShouldBeEmpty();
        bobSees.Shared.Select(v => v.Name).ShouldContain("Published");
        bobSees.Shared.Select(v => v.Name).ShouldNotContain("Private");
        bobSees.Shared.Single(v => v.Id == shared).OwnerName.ShouldBe(_admin.UserId);

        (await Views.RenameAsync(_bob, shared, "Mine now", Ct)).Status.ShouldBe(SaveStatus.NotFound);
        (await Views.DeleteAsync(_bob, shared, Ct)).ShouldBeFalse();

        // An Admin may delete a published view of someone else, but does not reach into private ones.
        (await Views.DeleteAsync(_admin, privateView, Ct)).ShouldBeFalse();
        (await Views.SetPublicAsync(_admin, privateView, true, Ct)).ShouldBeFalse(); // not the owner
        (await Views.SetPublicAsync(_alice, privateView, false, Ct)).ShouldBeFalse(); // not an Admin
    }

    [Fact]
    public async Task An_admin_deletes_a_published_view_owned_by_someone_else()
    {
        await EnsureUsersAsync();
        var id = (await Views.SaveAsync(_alice, "contacts", "Alice published", "q=a", cancellationToken: Ct)).Id;
        await using (var db = _factory.CreateDbContext())
        {
            (await db.SavedViews.SingleAsync(v => v.Id == id, Ct)).IsPublic = true;
            await db.SaveChangesAsync(Ct);
        }

        (await Views.DeleteAsync(_bob, id, Ct)).ShouldBeFalse();
        (await Views.DeleteAsync(_admin, id, Ct)).ShouldBeTrue();
        (await Views.ListAsync(_alice, "contacts", Ct)).Mine.ShouldBeEmpty();
    }
}
