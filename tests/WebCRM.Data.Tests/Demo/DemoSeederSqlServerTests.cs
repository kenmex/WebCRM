using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Integration;
using WebCRM.DemoData;

namespace WebCRM.Data.Tests.Demo;

/// <summary>
/// The demo tool against real SQL Server (a throwaway database): everything it writes passes the database's own rules,
/// it can run again and again, and it never touches data that is not demo data.
/// </summary>
public class DemoSeederSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Password = "Demo-Password-123";
    private const string RealUser = "real-user";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DemoOptions Small = new()
    {
        Accounts = 60,
        Contacts = 300,
        Leads = 20,
        Opportunities = 90,
        Activities = 500,
        Seed = 5,
        Today = new DateOnly(2026, 10, 9),
    };

    private readonly SqlServerFixture _sql;
    private DemoSeeder Seeder => new(_sql.ConnectionString);

    public DemoSeederSqlServerTests(SqlServerFixture sql) => _sql = sql;

    /// <summary>What the web app's start-up seeder provides: the system user, the roles and the lookup values the tool needs.</summary>
    private async Task EnsureBaseDataAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        await using var db = _sql.CreateContext();
        if (await db.Users.AnyAsync(u => u.Id == SystemUser.Id, Ct))
        {
            return;
        }

        db.Users.AddRange(
            new User { Id = SystemUser.Id, UserName = "system", NormalizedUserName = "SYSTEM", DisplayName = "System", IsActive = false },
            new User { Id = "system-test", UserName = "system-test", NormalizedUserName = "SYSTEM-TEST", DisplayName = "Fixture" },
            new User { Id = RealUser, UserName = "me@example.com", NormalizedUserName = "ME@EXAMPLE.COM", DisplayName = "Me, the developer" });
        db.Roles.AddRange(RoleNames.All.Select(r => new IdentityRole { Id = Guid.NewGuid().ToString(), Name = r, NormalizedName = r.ToUpperInvariant() }));
        db.AccountStatuses.AddRange(new AccountStatus { Name = "Prospect", SortOrder = 10 }, new AccountStatus { Name = "Active", SortOrder = 20 }, new AccountStatus { Name = "Inactive", SortOrder = 30 });
        db.Industries.AddRange(new Industry { Name = "Retail", SortOrder = 10 }, new Industry { Name = "Technology", SortOrder = 20 }, new Industry { Name = "Other", SortOrder = 30 });
        db.Salutations.AddRange(new Salutation { Name = "Mr", SortOrder = 10 }, new Salutation { Name = "Ms", SortOrder = 20 }, new Salutation { Name = "Dr", SortOrder = 30 });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<T> Query<T>(string sql)
    {
        await using var connection = new SqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync(Ct) ?? default(T)!, typeof(T));
    }

    private Task<int> Count(string table) => Query<int>($"SELECT COUNT(*) FROM {table}");

    private Task<int> DemoCount(string table) => Query<int>(table switch
    {
        "Accounts" or "Contacts" => $"SELECT COUNT(*) FROM {table} WHERE ImportBatchId IN (SELECT Id FROM ImportBatches WHERE Entity = 'Demo')",
        "AspNetUsers" => "SELECT COUNT(*) FROM AspNetUsers WHERE Id LIKE 'demo-%'",
        "Teams" => "SELECT COUNT(*) FROM Teams WHERE Name LIKE 'Demo: %'",
        _ => $"SELECT COUNT(*) FROM {table} WHERE CreatedBy = '{SystemUser.Id}'",
    });

    /// <summary>A few records of the developer's own, the kind the tool must never touch.</summary>
    private async Task<(int Account, int Contact, int Lead, int Opportunity, int Activity)> AddRealDataAsync()
    {
        await using var db = _sql.CreateContext();
        var status = await db.AccountStatuses.OrderBy(s => s.SortOrder).FirstAsync(Ct);
        var stage = await db.Stages.FirstAsync(s => !s.IsWon && !s.IsLost, Ct);
        var leadStatus = await db.LeadStatuses.FirstAsync(Ct);
        var type = await db.ActivityTypes.FirstAsync(Ct);
        var token = Guid.NewGuid().ToString("N")[..8];

        var account = new Account { Name = "My own account " + token, OwnerId = RealUser, AccountStatusId = status.Id };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);

        var contact = new Contact { LastName = "Mine " + token, AccountId = account.Id, OwnerId = RealUser };
        var opportunity = new Opportunity { Name = "My deal " + token, AccountId = account.Id, StageId = stage.Id, OwnerId = RealUser, CloseDate = new DateOnly(2026, 12, 1) };
        var lead = new Lead { Name = "My lead " + token, LeadStatusId = leadStatus.Id, OwnerId = RealUser };
        db.AddRange(contact, opportunity, lead);
        await db.SaveChangesAsync(Ct);

        var activity = new Activity { ActivityTypeId = type.Id, Subject = "My call " + token, OwnerId = RealUser, AccountId = account.Id };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(Ct);
        return (account.Id, contact.Id, lead.Id, opportunity.Id, activity.Id);
    }

    private async Task AssertRealDataIntactAsync((int Account, int Contact, int Lead, int Opportunity, int Activity) ids)
    {
        await using var db = _sql.CreateContext();
        (await db.Accounts.AnyAsync(a => a.Id == ids.Account && a.OwnerId == RealUser && a.ImportBatchId == null, Ct)).ShouldBeTrue("real account");
        (await db.Contacts.AnyAsync(c => c.Id == ids.Contact && c.OwnerId == RealUser, Ct)).ShouldBeTrue("real contact");
        (await db.Leads.AnyAsync(l => l.Id == ids.Lead && l.OwnerId == RealUser, Ct)).ShouldBeTrue("real lead");
        (await db.Opportunities.AnyAsync(o => o.Id == ids.Opportunity && o.OwnerId == RealUser, Ct)).ShouldBeTrue("real opportunity");
        (await db.Activities.AnyAsync(a => a.Id == ids.Activity && a.OwnerId == RealUser, Ct)).ShouldBeTrue("real activity");
        (await db.Users.AnyAsync(u => u.Id == RealUser, Ct)).ShouldBeTrue("real user");
    }

    // ---- Writing ----

    [Fact]
    public async Task It_writes_exactly_the_requested_counts_and_leaves_every_constraint_trusted()
    {
        await EnsureBaseDataAsync();
        await Seeder.WipeAsync(_ => { }, Ct);
        var before = (Accounts: await Count("Accounts"), Contacts: await Count("Contacts"), Activities: await Count("Activities"));

        var report = await Seeder.SeedAsync(Small, Password, _ => { }, Ct);

        report.Accounts.ShouldBe(60);
        report.Contacts.ShouldBe(300);
        report.Leads.ShouldBe(20);
        report.Opportunities.ShouldBe(90);
        report.Activities.ShouldBe(500);
        report.Users.ShouldBe(11);
        report.Teams.ShouldBe(2);
        (await Count("Accounts")).ShouldBe(before.Accounts + 60);
        (await Count("Contacts")).ShouldBe(before.Contacts + 300);
        (await Count("Activities")).ShouldBe(before.Activities + 500);
        (await DemoCount("Accounts")).ShouldBe(60);
        (await DemoCount("Contacts")).ShouldBe(300);
        (await DemoCount("Leads")).ShouldBe(20);
        (await DemoCount("Opportunities")).ShouldBe(90);
        (await DemoCount("Activities")).ShouldBe(500);

        // A bulk copy that skipped the checks would leave constraints "not trusted" and slow the optimiser down.
        (await Query<int>("SELECT COUNT(*) FROM sys.check_constraints WHERE is_not_trusted = 1")).ShouldBe(0);
        (await Query<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1")).ShouldBe(0);
        (await Query<int>("SELECT COUNT(*) FROM Addresses a JOIN Accounts x ON x.Id = a.AccountId WHERE x.ImportBatchId IS NOT NULL")).ShouldBeGreaterThan(59);
    }

    [Fact]
    public async Task The_demo_users_exist_with_roles_teams_a_working_password_and_managers()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);

        await using var db = _sql.CreateContext();
        var users = await db.Users.Where(u => u.Id.StartsWith("demo-")).ToListAsync(Ct);
        users.Count.ShouldBe(11);
        var roles = await db.UserRoles.Where(r => r.UserId.StartsWith("demo-")).Join(db.Roles, r => r.RoleId, r => r.Id, (_, role) => role.Name).ToListAsync(Ct);
        roles.Count(r => r == "Admin").ShouldBe(1);
        roles.Count(r => r == "Manager").ShouldBe(2);
        roles.Count(r => r == "Sales").ShouldBe(8);

        var teams = await db.Teams.Where(t => t.Name.StartsWith("Demo: ")).ToListAsync(Ct);
        teams.Count.ShouldBe(2);
        teams.ShouldAllBe(t => t.ManagerId != null && t.ManagerId.StartsWith("demo-manager-"));
        users.Where(u => !u.Id.Contains("admin")).ShouldAllBe(u => u.TeamId != null);

        var hasher = new PasswordHasher<User>();
        foreach (var user in users)
        {
            hasher.VerifyHashedPassword(user, user.PasswordHash!, Password).ShouldNotBe(PasswordVerificationResult.Failed, user.Email);
            hasher.VerifyHashedPassword(user, user.PasswordHash!, "not the password").ShouldBe(PasswordVerificationResult.Failed);
        }
    }

    [Fact]
    public async Task Running_again_replaces_the_demo_data_instead_of_adding_to_it()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        var firstAccounts = await Count("Accounts");

        var report = await Seeder.SeedAsync(Small, Password, _ => { }, Ct);

        report.Removed.Accounts.ShouldBe(60);
        report.Removed.Contacts.ShouldBe(300);
        report.Removed.Activities.ShouldBe(500);
        report.Removed.Users.ShouldBe(11);
        (await Count("Accounts")).ShouldBe(firstAccounts);
        (await DemoCount("Accounts")).ShouldBe(60);
        (await DemoCount("AspNetUsers")).ShouldBe(11);
        (await DemoCount("Teams")).ShouldBe(2);
        (await Query<int>("SELECT COUNT(*) FROM ImportBatches WHERE Entity = 'Demo'")).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_options_make_the_same_names_every_time()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        string[] First() => _sql.CreateContext().Accounts.IgnoreQueryFilters().Where(a => a.ImportBatchId != null).OrderBy(a => a.Name).Select(a => a.Name).ToArray();
        var names = First();

        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);

        First().ShouldBe(names);
    }

    // ---- Never touching real data ----

    [Fact]
    public async Task Your_own_records_users_and_lookups_survive_seeding_reseeding_and_wiping()
    {
        await EnsureBaseDataAsync();
        var real = await AddRealDataAsync();
        await using (var db = _sql.CreateContext())
        {
            db.LeadSources.Add(new LeadSource { Name = "My own source" });
            await db.SaveChangesAsync(Ct);
        }

        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        await AssertRealDataIntactAsync(real);
        await Seeder.SeedAsync(Small with { Seed = 99 }, Password, _ => { }, Ct);
        await AssertRealDataIntactAsync(real);
        await Seeder.WipeAsync(_ => { }, Ct);
        await AssertRealDataIntactAsync(real);

        (await DemoCount("Accounts")).ShouldBe(0);
        (await DemoCount("Contacts")).ShouldBe(0);
        (await DemoCount("Opportunities")).ShouldBe(0);
        (await DemoCount("Leads")).ShouldBe(0);
        (await DemoCount("Activities")).ShouldBe(0);
        (await DemoCount("AspNetUsers")).ShouldBe(0);
        (await DemoCount("Teams")).ShouldBe(0);
        (await Query<int>("SELECT COUNT(*) FROM ImportBatches WHERE Entity = 'Demo'")).ShouldBe(0);
        (await Query<int>("SELECT COUNT(*) FROM LeadSources WHERE Name = 'My own source'")).ShouldBe(1);
    }

    [Fact]
    public async Task A_contact_you_added_to_a_demo_account_stops_the_wipe_and_nothing_is_deleted()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        int demoAccount;
        await using (var db = _sql.CreateContext())
        {
            demoAccount = await db.Accounts.Where(a => a.ImportBatchId != null).Select(a => a.Id).FirstAsync(Ct);
            db.Contacts.Add(new Contact { LastName = "Added by me", AccountId = demoAccount, OwnerId = RealUser });
            await db.SaveChangesAsync(Ct);
        }

        try
        {
            var ex = await Should.ThrowAsync<DemoException>(() => Seeder.WipeAsync(_ => { }, Ct));
            ex.Message.ShouldContain("contacts that belong to a demo account");
            ex.Message.ShouldContain("nothing was changed", Case.Insensitive);

            // The failed run changed nothing: the demo data and the contact are all still there, and so is a reseed attempt.
            (await DemoCount("Accounts")).ShouldBe(60);
            (await DemoCount("Contacts")).ShouldBe(300);
            (await DemoCount("Activities")).ShouldBe(500);
            (await DemoCount("AspNetUsers")).ShouldBe(11);
            (await Query<int>($"SELECT COUNT(*) FROM Contacts WHERE LastName = 'Added by me' AND AccountId = {demoAccount}")).ShouldBe(1);
            await Should.ThrowAsync<DemoException>(() => Seeder.SeedAsync(Small, Password, _ => { }, Ct));
            (await DemoCount("Accounts")).ShouldBe(60);
            (await DemoCount("AspNetUsers")).ShouldBe(11);
        }
        finally
        {
            // Leave the shared database as found, so the other tests can wipe again.
            await Query<int>($"DELETE FROM Contacts WHERE LastName = 'Added by me' AND AccountId = {demoAccount}; SELECT 0");
        }
    }

    [Fact]
    public async Task A_record_of_yours_reassigned_to_a_demo_user_stops_the_wipe()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        var real = await AddRealDataAsync();
        await using (var db = _sql.CreateContext())
        {
            (await db.Accounts.FindAsync([real.Account], Ct))!.OwnerId = "demo-sales-athens-1";
            await db.SaveChangesAsync(Ct);
        }

        var ex = await Should.ThrowAsync<DemoException>(() => Seeder.WipeAsync(_ => { }, Ct));

        ex.Message.ShouldContain("accounts owned by a demo user that are not demo accounts");
        (await DemoCount("Accounts")).ShouldBe(60);
        (await Query<int>($"SELECT COUNT(*) FROM Accounts WHERE Id = {real.Account}")).ShouldBe(1);

        // Give it back, and the wipe works again.
        await using (var db = _sql.CreateContext())
        {
            (await db.Accounts.FindAsync([real.Account], Ct))!.OwnerId = RealUser;
            await db.SaveChangesAsync(Ct);
        }

        await Seeder.WipeAsync(_ => { }, Ct);
        (await DemoCount("Accounts")).ShouldBe(0);
    }

    // ---- Lookups ----

    [Fact]
    public async Task Missing_lead_sources_and_lost_reasons_are_added_once_and_existing_ones_are_left_alone()
    {
        await EnsureBaseDataAsync();
        await using (var db = _sql.CreateContext())
        {
            // Whatever ran before in this shared database, "Price" must exist as the developer's own value with its own order.
            var price = await db.LostReasons.FirstOrDefaultAsync(r => r.Name == "Price", Ct);
            if (price is null)
            {
                db.LostReasons.Add(new LostReason { Name = "Price", SortOrder = 5 });
            }
            else
            {
                price.SortOrder = 5;
            }

            await db.SaveChangesAsync(Ct);
        }

        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        var sources = await Count("LeadSources");
        var reasons = await Count("LostReasons");
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);

        (await Count("LeadSources")).ShouldBe(sources);
        (await Count("LostReasons")).ShouldBe(reasons);
        (await Query<int>("SELECT COUNT(*) FROM LeadSources WHERE Name IN ('Website', 'Referral', 'Trade show', 'Partner', 'Cold call')")).ShouldBe(5);
        (await Query<int>("SELECT COUNT(*) FROM LostReasons WHERE Name = 'Price'")).ShouldBe(1);
        (await Query<int>("SELECT SortOrder FROM LostReasons WHERE Name = 'Price'")).ShouldBe(5); // not renamed or reordered
    }

    // ---- The data is usable by the app ----

    [Fact]
    public async Task The_app_lists_and_search_work_on_the_demo_data_including_Greek_without_accents()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        var factory = _sql.CreateFactory();
        var owners = new OwnerService(factory);
        var admin = new UserContext("demo-admin", RoleNames.Admin, null);

        var accounts = await new AccountService(factory, owners).SearchAsync(new AccountQuery(ListScope.All, Search: null), admin, Ct);
        var contacts = await new ContactService(factory, owners, TimeProvider.System).SearchAsync(new ContactQuery(ListScope.All), admin, Ct);
        accounts.TotalCount.ShouldBeGreaterThanOrEqualTo(60);
        contacts.TotalCount.ShouldBeGreaterThanOrEqualTo(300);

        // A Greek company and a Greek surname, typed without accents and in capitals, are found (D3).
        await using var db = _sql.CreateContext();
        var greek = await db.Accounts.Where(a => a.ImportBatchId != null && a.TaxOffice != null).Select(a => a.Name).FirstAsync(Ct);
        var greekWord = Pools.Fold(greek.Split(' ').OrderByDescending(w => w.Length).First()).ToUpperInvariant();
        var surname = await db.Contacts.Where(c => c.ImportBatchId != null && c.LastName.StartsWith("Παπαδοπ")).Select(c => c.LastName).FirstOrDefaultAsync(Ct)
            ?? await db.Contacts.Where(c => c.ImportBatchId != null).Select(c => c.LastName).FirstAsync(Ct);

        var found = await new SearchService(factory).SearchAsync(greekWord, 20, admin, Ct);
        found.Groups[0].Hits.ShouldContain(h => h.Title == greek, $"search '{greekWord}'");
        var byName = await new SearchService(factory).SearchAsync(Pools.Fold(surname).ToUpperInvariant(), 20, admin, Ct);
        byName.Groups[1].TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Managers_see_their_teams_records_and_sales_people_their_own()
    {
        await EnsureBaseDataAsync();
        await Seeder.SeedAsync(Small, Password, _ => { }, Ct);
        var factory = _sql.CreateFactory();
        var accounts = new AccountService(factory, new OwnerService(factory));
        await using var db = _sql.CreateContext();
        var manager = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "demo-manager-athens", Ct);
        var sales = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "demo-sales-athens-1", Ct);

        var team = await accounts.SearchAsync(new AccountQuery(ListScope.Team), new UserContext(manager.Id, RoleNames.Manager, manager.TeamId), Ct);
        var mine = await accounts.SearchAsync(new AccountQuery(ListScope.Mine), new UserContext(sales.Id, RoleNames.Sales, sales.TeamId), Ct);
        var all = await accounts.SearchAsync(new AccountQuery(ListScope.All), new UserContext(manager.Id, RoleNames.Manager, manager.TeamId), Ct);

        mine.TotalCount.ShouldBeGreaterThan(0);
        team.TotalCount.ShouldBeGreaterThan(mine.TotalCount);
        all.TotalCount.ShouldBeGreaterThan(team.TotalCount);
    }
}

/// <summary>A database that the web app never started on: the tool says what is missing instead of failing half way.</summary>
public class DemoSeederPrerequisitesTests : IClassFixture<SqlServerFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;

    public DemoSeederPrerequisitesTests(SqlServerFixture sql) => _sql = sql;

    [Fact]
    public async Task Without_the_system_user_and_roles_it_asks_you_to_start_the_web_app_first_and_writes_nothing()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        var ex = await Should.ThrowAsync<DemoException>(() => new DemoSeeder(_sql.ConnectionString).SeedAsync(
            new DemoOptions().Scaled(0.001), "Demo-Password-123", _ => { }, Ct));

        ex.Message.ShouldContain("Start the web app once");
        await using var db = _sql.CreateContext();
        (await db.Accounts.IgnoreQueryFilters().CountAsync(Ct)).ShouldBe(0);
        (await db.ImportBatches.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_weak_password_is_refused_before_the_database_is_touched()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        var ex = await Should.ThrowAsync<DemoException>(() => new DemoSeeder(_sql.ConnectionString).SeedAsync(
            new DemoOptions().Scaled(0.001), "weak", _ => { }, Ct));

        ex.Message.ShouldContain("Demo:Password");
    }

    [Fact]
    public async Task Wiping_an_empty_database_is_a_quiet_no_op()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);
        var messages = new List<string>();

        var report = await new DemoSeeder(_sql.ConnectionString).WipeAsync(messages.Add, Ct);

        report.Total.ShouldBe(0);
        messages.ShouldContain("No demo data to remove.");
    }
}
