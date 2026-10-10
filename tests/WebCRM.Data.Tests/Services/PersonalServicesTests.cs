using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Records;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Services;

public class PersonalServicesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Factory _factory = new(Guid.NewGuid().ToString());
    private readonly FakeTimeProvider _clock = new(Start);
    private readonly UserContext _alice = new("alice", RoleNames.Sales, TeamId: null);
    private readonly UserContext _bob = new("bob", RoleNames.Sales, TeamId: null);
    private readonly UserContext _admin = new("admin", RoleNames.Admin, TeamId: null);

    private FavouriteService Favourites => new(_factory, _clock);

    private RecentViewService Recents => new(_factory, _clock);

    private SavedViewService Views => new(_factory);

    private async Task<int> AddAccountAsync(string name, string ownerId = "alice")
    {
        await using var db = _factory.CreateDbContext();
        if (!await db.Users.AnyAsync(u => u.Id == ownerId, Ct))
        {
            db.Users.Add(new User { Id = ownerId, UserName = ownerId, DisplayName = ownerId });
        }

        if (!await db.AccountStatuses.AnyAsync(Ct))
        {
            db.AccountStatuses.Add(new AccountStatus { Id = 1, Name = "Active" });
        }

        var account = new Account { Name = name, OwnerId = ownerId, AccountStatusId = 1, CreatedBy = ownerId };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    // ---- favourites

    [Fact]
    public async Task Starring_a_record_is_idempotent_and_can_be_undone()
    {
        var id = await AddAccountAsync("Acme");

        (await Favourites.SetAsync(_alice, SearchEntity.Account, id, true, Ct)).ShouldBeTrue();
        (await Favourites.SetAsync(_alice, SearchEntity.Account, id, true, Ct)).ShouldBeTrue();
        (await Favourites.IsFavouriteAsync(_alice, SearchEntity.Account, id, Ct)).ShouldBeTrue();
        (await Favourites.IsFavouriteAsync(_bob, SearchEntity.Account, id, Ct)).ShouldBeFalse();
        (await Favourites.ListAsync(_alice, 10, Ct)).Count.ShouldBe(1);

        (await Favourites.SetAsync(_alice, SearchEntity.Account, id, false, Ct)).ShouldBeTrue();
        (await Favourites.IsFavouriteAsync(_alice, SearchEntity.Account, id, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_record_cannot_be_starred()
    {
        (await Favourites.SetAsync(_alice, SearchEntity.Account, 9999, true, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Favourites_list_newest_first_limited_and_drop_deleted_records()
    {
        var first = await AddAccountAsync("First");
        var second = await AddAccountAsync("Second");
        var third = await AddAccountAsync("Third");

        await Favourites.SetAsync(_alice, SearchEntity.Account, first, true, Ct);
        _clock.UtcNow = Start.AddMinutes(1);
        await Favourites.SetAsync(_alice, SearchEntity.Account, second, true, Ct);
        _clock.UtcNow = Start.AddMinutes(2);
        await Favourites.SetAsync(_alice, SearchEntity.Account, third, true, Ct);

        (await Favourites.ListAsync(_alice, 10, Ct)).Select(h => h.Title).ShouldBe(["Third", "Second", "First"]);
        (await Favourites.ListAsync(_alice, 2, Ct)).Select(h => h.Title).ShouldBe(["Third", "Second"]);

        await using (var db = _factory.CreateDbContext())
        {
            var gone = await db.Accounts.FirstAsync(a => a.Id == second, Ct);
            gone.IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        (await Favourites.ListAsync(_alice, 10, Ct)).Select(h => h.Title).ShouldBe(["Third", "First"]);

        await using var check = _factory.CreateDbContext();
        (await check.Favourites.CountAsync(Ct)).ShouldBe(2); // the star of the removed record was cleared
    }

    // ---- recent views

    [Fact]
    public async Task A_recent_view_is_upserted_and_the_same_record_within_a_minute_writes_nothing()
    {
        var id = await AddAccountAsync("Acme");

        await Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct);
        _clock.UtcNow = Start.AddSeconds(30);
        await Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct);

        await using (var db = _factory.CreateDbContext())
        {
            var row = await db.RecentViews.SingleAsync(Ct);
            row.ViewedAt.ShouldBe(Start.UtcDateTime);
        }

        _clock.UtcNow = Start.AddMinutes(5);
        await Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct);

        await using var after = _factory.CreateDbContext();
        (await after.RecentViews.SingleAsync(Ct)).ViewedAt.ShouldBe(Start.AddMinutes(5).UtcDateTime);
    }

    [Fact]
    public async Task Recent_views_list_newest_first_and_keep_only_the_last_fifty()
    {
        var ids = new List<int>();
        for (var i = 0; i < PersonalRules.MaxRecentViews + 5; i++)
        {
            ids.Add(await AddAccountAsync($"Acct {i:00}"));
        }

        foreach (var id in ids)
        {
            _clock.UtcNow = _clock.UtcNow.AddMinutes(2);
            await Recents.RecordAsync(_alice, SearchEntity.Account, id, Ct);
        }

        await using (var db = _factory.CreateDbContext())
        {
            (await db.RecentViews.CountAsync(Ct)).ShouldBe(PersonalRules.MaxRecentViews);
        }

        var list = await Recents.ListAsync(_alice, 10, Ct);
        list.Count.ShouldBe(10);
        list[0].Title.ShouldBe($"Acct {PersonalRules.MaxRecentViews + 4:00}");
    }

    [Fact]
    public async Task A_record_that_does_not_exist_is_not_remembered()
    {
        // Visibility is still the stub (everything visible) until the team rules arrive, so only a missing record is testable.
        await Recents.RecordAsync(_alice, SearchEntity.Account, 12345, Ct);

        await using var db = _factory.CreateDbContext();
        (await db.RecentViews.CountAsync(Ct)).ShouldBe(0);
    }

    // ---- saved views

    [Fact]
    public async Task A_view_is_saved_normalised_and_listed_for_its_owner_only()
    {
        var result = await Views.SaveAsync(_alice, "accounts", "  My accounts ", "?scope=all&page=2&q=x", cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var mine = await Views.ListAsync(_alice, "accounts", Ct);
        mine.Mine.Single().QueryString.ShouldBe("q=x&scope=all");
        mine.Mine.Single().Name.ShouldBe("My accounts");
        mine.Mine.Single().IsMine.ShouldBeTrue();

        (await Views.ListAsync(_bob, "accounts", Ct)).Mine.ShouldBeEmpty();
        (await Views.ListAsync(_alice, "contacts", Ct)).Mine.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_bad_list_name_or_query_string_is_refused()
    {
        (await Views.SaveAsync(_alice, "users", "V", "a=1", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);
        (await Views.SaveAsync(_alice, "accounts", " ", "a=1", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);
        (await Views.SaveAsync(_alice, "accounts", "V", "http://x/y", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);
    }

    [Fact]
    public async Task A_duplicate_name_is_refused_unless_replacing_and_replacing_updates_the_filters()
    {
        await Views.SaveAsync(_alice, "accounts", "Mine", "q=a", cancellationToken: Ct);

        var dup = await Views.SaveAsync(_alice, "accounts", "MINE", "q=b", cancellationToken: Ct);
        dup.Status.ShouldBe(SaveStatus.Invalid);
        dup.FieldErrors!.ShouldContainKey("Name");

        (await Views.SaveAsync(_alice, "accounts", "MINE", "q=b", replaceExisting: true, cancellationToken: Ct))
            .Status.ShouldBe(SaveStatus.Saved);
        var list = (await Views.ListAsync(_alice, "accounts", Ct)).Mine;
        list.Count.ShouldBe(1);
        list[0].QueryString.ShouldBe("q=b");
    }

    [Fact]
    public async Task A_user_can_keep_thirty_views_per_list_and_no_more()
    {
        for (var i = 0; i < PersonalRules.MaxSavedViewsPerList; i++)
        {
            (await Views.SaveAsync(_alice, "accounts", $"View {i}", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        }

        (await Views.SaveAsync(_alice, "accounts", "One too many", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);
        (await Views.SaveAsync(_alice, "contacts", "Other list", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        (await Views.SaveAsync(_bob, "accounts", "Bob view", "q=a", cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Only_the_owner_renames_and_only_a_clash_free_name_is_accepted()
    {
        var a = (await Views.SaveAsync(_alice, "accounts", "A", "q=a", cancellationToken: Ct)).Id;
        await Views.SaveAsync(_alice, "accounts", "B", "q=b", cancellationToken: Ct);

        (await Views.RenameAsync(_bob, a, "Hijack", Ct)).Status.ShouldBe(SaveStatus.NotFound);
        (await Views.RenameAsync(_alice, a, "b", Ct)).Status.ShouldBe(SaveStatus.Invalid);
        (await Views.RenameAsync(_alice, a, "A2", Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Publishing_is_for_admins_and_published_views_are_shared_read_only()
    {
        var mine = (await Views.SaveAsync(_alice, "accounts", "Alice view", "q=a", cancellationToken: Ct)).Id;
        (await Views.SetPublicAsync(_alice, mine, true, Ct)).ShouldBeFalse();

        var team = (await Views.SaveAsync(_admin, "accounts", "Team view", "scope=team", cancellationToken: Ct)).Id;
        (await Views.SetPublicAsync(_admin, team, true, Ct)).ShouldBeTrue();

        var seen = await Views.ListAsync(_alice, "accounts", Ct);
        seen.Shared.Single().Name.ShouldBe("Team view");
        seen.Shared.Single().IsMine.ShouldBeFalse();

        (await Views.RenameAsync(_alice, team, "Renamed", Ct)).Status.ShouldBe(SaveStatus.NotFound);
        (await Views.DeleteAsync(_alice, team, Ct)).ShouldBeFalse();
        (await Views.SetPublicAsync(_alice, team, false, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_is_for_the_owner_or_an_admin_on_a_published_view()
    {
        var privateView = (await Views.SaveAsync(_alice, "accounts", "Private", "q=a", cancellationToken: Ct)).Id;
        var published = (await Views.SaveAsync(_alice, "accounts", "Published", "q=b", cancellationToken: Ct)).Id;
        await using (var db = _factory.CreateDbContext())
        {
            (await db.SavedViews.FirstAsync(v => v.Id == published, Ct)).IsPublic = true;
            await db.SaveChangesAsync(Ct);
        }

        (await Views.DeleteAsync(_bob, privateView, Ct)).ShouldBeFalse();
        (await Views.DeleteAsync(_admin, privateView, Ct)).ShouldBeFalse(); // an Admin does not reach into private views
        (await Views.DeleteAsync(_admin, published, Ct)).ShouldBeTrue();
        (await Views.DeleteAsync(_alice, privateView, Ct)).ShouldBeTrue();
        (await Views.DeleteAsync(_alice, privateView, Ct)).ShouldBeFalse();
    }

    private sealed class Factory(string dbName) : IDbContextFactory<CrmDbContext>
    {
        private readonly FakeCurrentUser _currentUser = new() { UserId = "alice" };

        public CrmDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseInMemoryDatabase(dbName)
                .AddInterceptors(new AuditFieldsInterceptor(_currentUser, TimeProvider.System))
                .Options;
            return new CrmDbContext(options);
        }
    }
}
