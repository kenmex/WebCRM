using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// ContactService against real SQL Server: LINQ translation of the list query, the computed FullName column,
/// collations, rowversion and the global soft-delete filter, none of which the in-memory tests can show.
/// </summary>
public class ContactServiceSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Alice = "ct-alice";
    private const string Bob = "ct-bob";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly ContactService _service;

    // Every test uses its own prefix, so tests sharing the database never see each other's rows.
    private readonly string _prefix = "C" + Guid.NewGuid().ToString("N")[..8] + " ";

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);
    private readonly UserContext _admin = new("ct-admin", RoleNames.Admin, TeamId: null);

    public ContactServiceSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _service = new ContactService(_factory, new OwnerService(_factory), TimeProvider.System);
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
        db.Users.AddRange(alice, bob, User("ct-admin", "Admin"), User("system-test", "System test"));
        await db.SaveChangesAsync(Ct);

        var team = new Team { Name = "CT team" };
        db.Teams.Add(team);
        await db.SaveChangesAsync(Ct);
        alice.TeamId = bob.TeamId = team.Id;

        db.AccountStatuses.Add(new AccountStatus { Name = "CT Active", SortOrder = 10 });
        db.ActivityTypes.Add(new ActivityType { Name = "CT Call" });
        await db.SaveChangesAsync(Ct);
    }

    private static User User(string id, string name) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        DisplayName = name,
    };

    private async Task<int> AddAccountAsync(string name, string owner = Alice)
    {
        await using var db = _factory.CreateDbContext();
        var account = new Account
        {
            Name = _prefix + name,
            OwnerId = owner,
            AccountStatusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct),
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    private async Task<int> AddContactAsync(
        string? first, string last, int accountId, string owner = Alice, string? email = null, string? jobTitle = null)
    {
        await using var db = _factory.CreateDbContext();
        var contact = new Contact
        {
            FirstName = first,
            LastName = last,
            AccountId = accountId,
            OwnerId = owner,
            Email = email,
            JobTitle = jobTitle,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(Ct);
        return contact.Id;
    }

    // The tests in this class share one database, so an email must be unique per test: the duplicate-email warning
    // would otherwise depend on which test ran first.
    private string Email(string local) => $"{local}.{_prefix.Trim().ToLowerInvariant()}@example.com";

    private ContactQuery All() => new(ListScope.All, Search: _prefix);

    // ---- The list query ----

    [Fact]
    public async Task Search_translates_and_runs_for_every_sort_in_both_directions()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        var zeta = await AddAccountAsync("Zeta", Bob);
        var email = Email("anna");
        var anna = await AddContactAsync("Anna", _prefix + "Smith", acme, email: email, jobTitle: "Buyer");
        await AddContactAsync("Bob", _prefix + "Jones", zeta, Bob);

        await using (var db = _factory.CreateDbContext())
        {
            db.Activities.Add(new Activity
            {
                Subject = "Ring",
                ContactId = anna,
                ActivityTypeId = await db.ActivityTypes.Select(t => t.Id).FirstAsync(Ct),
                OwnerId = Alice,
                DoneAt = DateTime.UtcNow.AddDays(-3),
            });
            await db.SaveChangesAsync(Ct);
        }

        foreach (var sort in Enum.GetValues<ContactSort>())
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await _service.SearchAsync(All() with { Sort = sort, Descending = descending }, _alice, Ct);

                result.TotalCount.ShouldBe(2, $"sort {sort} descending={descending}");
            }
        }

        var row = (await _service.SearchAsync(All() with { Search = email }, _alice, Ct)).Items.Single();
        row.FullName.ShouldBe("Anna " + _prefix + "Smith");
        row.AccountName.ShouldBe(_prefix + "Acme");
        row.OwnerName.ShouldBe("Alice");
        row.JobTitle.ShouldBe("Buyer");
        row.LastActivityAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Search_runs_every_scope_and_filter()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        await AddContactAsync("Anna", _prefix + "Smith", acme, email: Email("anna"));
        var manager = new UserContext(Alice, RoleNames.Manager, TeamId: await TeamIdAsync());

        foreach (var scope in Enum.GetValues<ListScope>())
        {
            var query = new ContactQuery(
                scope, Search: _prefix + "Smith", AccountId: acme, OwnerId: Alice, HasEmail: true);
            var result = await _service.SearchAsync(query, manager, Ct);

            result.TotalCount.ShouldBe(1, $"scope {scope}");
        }
    }

    [Fact]
    public async Task Search_scope_mine_and_the_owner_and_has_email_filters_narrow_the_list()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        await AddContactAsync("Anna", _prefix + "Mine", acme, Alice, email: Email("mine"));
        await AddContactAsync("Bob", _prefix + "Bobs", acme, Bob);

        (await _service.SearchAsync(new ContactQuery(ListScope.Mine, Search: _prefix), _alice, Ct))
            .Items.Select(i => i.FullName).ShouldBe(["Anna " + _prefix + "Mine"]);
        (await _service.SearchAsync(All() with { OwnerId = Bob }, _alice, Ct))
            .Items.Select(i => i.FullName).ShouldBe(["Bob " + _prefix + "Bobs"]);
        (await _service.SearchAsync(All() with { HasEmail = true }, _alice, Ct)).TotalCount.ShouldBe(1);
        (await _service.SearchAsync(All() with { HasEmail = false }, _alice, Ct)).TotalCount.ShouldBe(1);
        (await _service.SearchAsync(All(), _alice, Ct)).TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Search_pages_and_sorts_on_the_server_by_the_computed_full_name()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        foreach (var first in new[] { "Eve", "Adam", "Dora", "Carl", "Bella" })
        {
            await AddContactAsync(first, _prefix + "Page", acme);
        }

        var page2 = await _service.SearchAsync(All() with { Page = 2, PageSize = 2 }, _alice, Ct);

        page2.TotalCount.ShouldBe(5);
        page2.Items.Select(i => i.FullName).ShouldBe([$"Carl {_prefix}Page", $"Dora {_prefix}Page"]);
    }

    [Fact]
    public async Task FullName_is_just_the_last_name_when_there_is_no_first_name()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        await AddContactAsync(null, _prefix + "Cher", acme);

        (await _service.SearchAsync(All(), _alice, Ct)).Items.Single().FullName.ShouldBe(_prefix + "Cher");
    }

    [Fact]
    public async Task Search_is_accent_and_case_insensitive_on_names_email_and_account()
    {
        await EnsureReferenceDataAsync();
        var account = await AddAccountAsync("Αθήνα Συμβουλευτική");
        await AddContactAsync("Μαρία", "Παπαδόπουλος", account, email: Email("maria"));

        // D3: no accents, different case.
        foreach (var text in new[] { "ΜΑΡΙΑ ΠΑΠΑΔΟΠΟΥΛΟΣ", "παπαδοπουλος", "αθηνα συμβ", Email("maria").ToUpperInvariant() })
        {
            var result = await _service.SearchAsync(new ContactQuery(ListScope.All, Search: text), _alice, Ct);

            result.Items.ShouldContain(i => i.FullName == "Μαρία Παπαδόπουλος", $"search '{text}'");
        }
    }

    // ---- Soft delete ----

    [Fact]
    public async Task A_deleted_contact_and_the_contacts_of_a_deleted_account_vanish_from_the_list()
    {
        await EnsureReferenceDataAsync();
        var keep = await AddAccountAsync("Keep");
        var doomed = await AddAccountAsync("Doomed");
        var deletedContact = await AddContactAsync("Del", _prefix + "Contact", keep);
        await AddContactAsync("Stay", _prefix + "Stay", keep);
        await AddContactAsync("Orphan", _prefix + "Orphan", doomed);

        (await _service.DeleteAsync(deletedContact, _alice, Ct)).ShouldBeTrue();
        var accounts = new AccountService(_factory, new OwnerService(_factory));
        (await accounts.DeleteAsync(doomed, _alice, Ct)).Status.ShouldBe(AccountDeleteStatus.Deleted);

        var rows = (await _service.SearchAsync(All(), _alice, Ct)).Items;
        rows.Select(r => r.FullName).ShouldBe(["Stay " + _prefix + "Stay"]);
        (await _service.GetAsync(deletedContact, _alice, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task A_contact_whose_account_alone_was_soft_deleted_is_hidden_too()
    {
        await EnsureReferenceDataAsync();
        var account = await AddAccountAsync("Half deleted");
        var contact = await AddContactAsync("Half", _prefix + "Deleted", account);

        // Only the account row is flipped, as a stray manual change or a future restore bug might.
        await using (var db = _factory.CreateDbContext())
        {
            (await db.Accounts.SingleAsync(a => a.Id == account, Ct)).IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        (await _service.SearchAsync(All(), _alice, Ct)).TotalCount.ShouldBe(0);
        (await _service.GetAsync(contact, _alice, Ct)).ShouldBeNull();
    }

    // ---- Save ----

    [Fact]
    public async Task Save_and_get_round_trip_with_the_account_owner_default()
    {
        await EnsureReferenceDataAsync();
        var account = await AddAccountAsync("Bobs account", Bob);

        var model = await _service.NewAsync(_alice, account, Ct);
        model.OwnerId.ShouldBe(Bob);
        model.FirstName = "Anna";
        model.LastName = _prefix + "Smith";
        model.Email = "  " + Email("Anna").ToUpperInvariant() + " ";

        var saved = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        saved.Status.ShouldBe(SaveStatus.Saved);
        var detail = await _service.GetAsync(saved.Id, _alice, Ct);
        detail!.Email.ShouldBe(Email("anna"));
        detail.FullName.ShouldBe("Anna " + _prefix + "Smith");
        detail.OwnerName.ShouldBe("Bob");
        detail.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_duplicate_email_warns_across_active_contacts_ignoring_case()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        await AddContactAsync("Anna", _prefix + "First", acme, email: Email("dup"));
        var model = new ContactEditModel
        {
            LastName = _prefix + "Second",
            AccountId = acme,
            OwnerId = Alice,
            Email = Email("dup").ToUpperInvariant(),
        };

        var warned = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        warned.Status.ShouldBe(SaveStatus.Warning);
        warned.Warnings.ShouldBe([$"Anna {_prefix}First ({_prefix}Acme)"]);
        (await _service.SaveAsync(model, _alice, new SaveOptions { AcceptWarnings = true }, Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task A_stale_row_version_gives_a_conflict_naming_the_other_user()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        var id = await AddContactAsync("Anna", _prefix + "Contested", acme);
        var stale = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();

        // Bob saves first.
        _sql.CurrentUser.UserId = Bob;
        try
        {
            var bobs = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
            bobs.Phone = "111";
            (await _service.SaveAsync(bobs, _admin, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        }
        finally
        {
            _sql.CurrentUser.UserId = "system-test";
        }

        stale.Phone = "222";
        var result = await _service.SaveAsync(stale, _admin, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Conflict);
        result.Conflict!.ChangedBy.ShouldBe("Bob");
        result.Conflict.ChangedAtUtc.ShouldNotBeNull();

        (await _service.SaveAsync(stale.Clone(), _alice, new SaveOptions { Overwrite = true }, Ct))
            .Status.ShouldBe(SaveStatus.Conflict);
        (await _service.SaveAsync(stale.Clone(), _admin, new SaveOptions { Overwrite = true }, Ct))
            .Status.ShouldBe(SaveStatus.Saved);
    }

    // ---- Department, salutation, Do not contact ----

    private async Task<int> AddSalutationAsync(string name)
    {
        await using var db = _factory.CreateDbContext();
        var salutation = new Salutation { Name = _prefix + name };
        db.Salutations.Add(salutation);
        await db.SaveChangesAsync(Ct);
        return salutation.Id;
    }

    [Fact]
    public async Task The_new_contact_fields_round_trip_through_sql_and_Do_not_contact_defaults_to_off()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        var dr = await AddSalutationAsync("Dr");

        var model = await _service.NewAsync(_alice, acme, Ct);
        model.LastName = _prefix + "Smith";
        model.Department = "Purchasing";
        model.SalutationId = dr;
        model.DoNotContact = true;
        var saved = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        saved.Status.ShouldBe(SaveStatus.Saved);
        var detail = (await _service.GetAsync(saved.Id, _alice, Ct))!;
        detail.Department.ShouldBe("Purchasing");
        detail.SalutationName.ShouldBe(_prefix + "Dr");
        detail.DoNotContact.ShouldBeTrue();
        detail.DoNotContactSince.ShouldNotBeNull();
        (DateTime.UtcNow - detail.DoNotContactSince.Value).ShouldBeLessThan(TimeSpan.FromMinutes(5));

        // A contact inserted without the column gets the database default (off, no date).
        var plain = await AddContactAsync("Plain", _prefix + "Plain", acme);
        var plainDetail = (await _service.GetAsync(plain, _alice, Ct))!;
        plainDetail.DoNotContact.ShouldBeFalse();
        plainDetail.DoNotContactSince.ShouldBeNull();
    }

    [Fact]
    public async Task The_foreign_key_refuses_a_salutation_that_does_not_exist_even_when_the_service_is_bypassed()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        var id = await AddContactAsync("Anna", _prefix + "Smith", acme);

        await using var db = _factory.CreateDbContext();
        (await db.Contacts.SingleAsync(c => c.Id == id, Ct)).SalutationId = 999_999;

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        ex.InnerException!.Message.ShouldContain("FK_Contacts_Salutations_SalutationId");
    }

    [Fact]
    public async Task The_Do_not_contact_filter_works_in_sql_and_the_flag_reaches_the_list()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync("Acme");
        var marked = await _service.NewAsync(_alice, acme, Ct);
        marked.LastName = _prefix + "Marked";
        marked.DoNotContact = true;
        await _service.SaveAsync(marked, _alice, cancellationToken: Ct);
        await AddContactAsync("Free", _prefix + "Free", acme);

        var onlyMarked = await _service.SearchAsync(All() with { DoNotContact = true }, _alice, Ct);
        var onlyFree = await _service.SearchAsync(All() with { DoNotContact = false }, _alice, Ct);

        onlyMarked.Items.Select(i => (i.FullName, i.DoNotContact)).ShouldBe([(_prefix + "Marked", true)]);
        onlyFree.Items.Select(i => (i.FullName, i.DoNotContact)).ShouldBe([("Free " + _prefix + "Free", false)]);
        (await _service.SearchAsync(All(), _alice, Ct)).TotalCount.ShouldBe(2);
    }

    private async Task<int> TeamIdAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Teams.Where(t => t.Name == "CT team").Select(t => t.Id).SingleAsync(Ct);
    }
}
