using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Services;

/// <summary>
/// ContactService save rules on the real CrmDbContext model with the EF InMemory provider. InMemory does not
/// compute FullName or collations, so list search and sorting are covered by the SQL Server integration tests.
/// </summary>
public class ContactServiceTests
{
    private const string Alice = "user-alice"; // Sales, team 1
    private const string Bob = "user-bob"; // Sales, team 2
    private const string Carol = "user-carol"; // Manager, team 1

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryFactory _factory;
    private readonly ContactService _service;

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: 1);
    private readonly UserContext _manager = new(Carol, RoleNames.Manager, TeamId: 1);
    private readonly UserContext _admin = new("user-admin", RoleNames.Admin, TeamId: null);

    private int _aliceAccount;
    private int _bobAccount;

    public ContactServiceTests()
    {
        _factory = new InMemoryFactory(_dbName);
        _service = new ContactService(_factory, new OwnerService(_factory));
        Seed();
    }

    private void Seed()
    {
        using var db = _factory.CreateDbContext();
        db.Users.AddRange(
            new User { Id = Alice, UserName = "alice", DisplayName = "Alice", TeamId = 1 },
            new User { Id = Bob, UserName = "bob", DisplayName = "Bob", TeamId = 2 },
            new User { Id = Carol, UserName = "carol", DisplayName = "Carol", TeamId = 1 },
            new User { Id = "user-admin", UserName = "admin", DisplayName = "Admin" });
        db.AccountStatuses.Add(new AccountStatus { Id = 1, Name = "Active" });
        var aliceAccount = new Account { Name = "Alice Co", OwnerId = Alice, AccountStatusId = 1, CreatedBy = Alice };
        var bobAccount = new Account { Name = "Bob Co", OwnerId = Bob, AccountStatusId = 1, CreatedBy = Bob };
        db.Accounts.AddRange(aliceAccount, bobAccount);
        db.SaveChangesAsync(Ct).GetAwaiter().GetResult(); // audited, so the interceptor needs the async save
        _aliceAccount = aliceAccount.Id;
        _bobAccount = bobAccount.Id;
    }

    private ContactEditModel Form(string lastName = "Smith", int? accountId = null, string owner = Alice) => new()
    {
        FirstName = "Anna",
        LastName = lastName,
        AccountId = accountId ?? _aliceAccount,
        OwnerId = owner,
    };

    private async Task<int> AddAsync(string lastName, int? accountId = null, string owner = Alice, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var contact = new Contact
        {
            FirstName = "Anna",
            LastName = lastName,
            AccountId = accountId ?? _aliceAccount,
            OwnerId = owner,
            Email = email,
            CreatedBy = owner,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(Ct);
        return contact.Id;
    }

    // ---- Create ----

    [Fact]
    public async Task Save_creates_a_contact_and_normalises_the_fields()
    {
        var model = Form();
        model.FirstName = "  Anna ";
        model.Email = "  Anna.Smith@Example.COM ";
        model.JobTitle = " ";
        model.Phone = " 210 123 4567 ";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await _service.GetAsync(result.Id, _alice, Ct);
        saved!.FirstName.ShouldBe("Anna");
        saved.Email.ShouldBe("anna.smith@example.com");
        saved.JobTitle.ShouldBeNull();
        saved.Phone.ShouldBe("210 123 4567");
        saved.AccountName.ShouldBe("Alice Co");
        saved.OwnerName.ShouldBe("Alice");
    }

    [Fact]
    public async Task Save_rejects_invalid_input_with_field_errors()
    {
        var model = Form(lastName: " ");
        model.Email = "not an email";
        model.AccountId = null;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.Keys.ShouldBe(
            [nameof(ContactEditModel.LastName), nameof(ContactEditModel.Email), nameof(ContactEditModel.AccountId)],
            ignoreOrder: true);
        result.FieldErrors[nameof(ContactEditModel.Email)].ShouldBe(ContactRules.InvalidEmailMessage);
    }

    [Fact]
    public async Task Save_rejects_an_account_that_does_not_exist_or_was_deleted()
    {
        var missing = await _service.SaveAsync(Form(accountId: 9999), _alice, cancellationToken: Ct);
        missing.FieldErrors!.ShouldContainKey(nameof(ContactEditModel.AccountId));

        await using (var db = _factory.CreateDbContext())
        {
            (await db.Accounts.FindAsync([_bobAccount], Ct))!.IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        var deleted = await _service.SaveAsync(Form(accountId: _bobAccount, owner: Bob), _admin, cancellationToken: Ct);
        deleted.FieldErrors!.ShouldContainKey(nameof(ContactEditModel.AccountId));
    }

    // ---- Owner defaults and the owner rule ----

    [Fact]
    public async Task New_with_an_account_fills_the_account_and_defaults_the_owner_to_the_account_owner()
    {
        var model = await _service.NewAsync(_alice, _bobAccount, Ct);

        model.AccountId.ShouldBe(_bobAccount);
        model.OwnerId.ShouldBe(Bob);
    }

    [Fact]
    public async Task New_without_an_account_defaults_the_owner_to_the_current_user()
    {
        var model = await _service.NewAsync(_alice, cancellationToken: Ct);

        model.AccountId.ShouldBeNull();
        model.OwnerId.ShouldBe(Alice);
    }

    [Fact]
    public async Task New_with_an_account_the_user_cannot_see_is_not_prefilled()
    {
        var model = await _service.NewAsync(_alice, 9999, Ct);

        model.AccountId.ShouldBeNull();
        model.OwnerId.ShouldBe(Alice);
    }

    [Fact]
    public async Task On_create_the_account_owner_is_always_an_allowed_owner()
    {
        // Alice is Sales and could only assign to herself, but Bob owns the account.
        var result = await _service.SaveAsync(Form(accountId: _bobAccount, owner: Bob), _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task On_create_any_other_owner_still_follows_the_usual_rule()
    {
        // Carol is on Alice's team, but a Sales user may only assign to themselves.
        var salesForCarol = await _service.SaveAsync(Form(owner: Carol), _alice, cancellationToken: Ct);
        salesForCarol.FieldErrors!.ShouldContainKey(nameof(ContactEditModel.OwnerId));

        // A Manager may assign within the team, but not to another team (Bob) unless Bob owns the account.
        (await _service.SaveAsync(Form(owner: Alice), _manager, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        var outsideTeam = await _service.SaveAsync(Form(owner: Bob), _manager, cancellationToken: Ct);
        outsideTeam.FieldErrors!.ShouldContainKey(nameof(ContactEditModel.OwnerId));

        // An Admin may assign to any active user.
        (await _service.SaveAsync(Form(owner: Bob), _admin, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task On_edit_the_account_owner_exception_does_not_apply_but_the_current_owner_may_stay()
    {
        var id = await AddAsync("Smith", _bobAccount, owner: Bob);
        var model = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();

        // Unchanged owner (Bob) is fine even though Alice could not assign to Bob...
        model.JobTitle = "Buyer";
        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        // ...and changing it to someone Alice may not assign to is not.
        var changed = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
        changed.OwnerId = Carol;
        (await _service.SaveAsync(changed, _alice, cancellationToken: Ct)).FieldErrors!
            .ShouldContainKey(nameof(ContactEditModel.OwnerId));
    }

    [Fact]
    public async Task GetDefaultOwner_returns_the_account_owner_or_null()
    {
        (await _service.GetDefaultOwnerAsync(_bobAccount, _alice, Ct)).ShouldBe(Bob);
        (await _service.GetDefaultOwnerAsync(9999, _alice, Ct)).ShouldBeNull();
    }

    // ---- Duplicate email warning ----

    [Fact]
    public async Task Save_warns_about_a_duplicate_email_and_saves_when_accepted()
    {
        await AddAsync("Existing", _bobAccount, Bob, "anna@example.com");
        var model = Form();
        model.Email = "Anna@Example.com";

        var warned = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        warned.Status.ShouldBe(SaveStatus.Warning);
        warned.WarningMessage.ShouldBe("A contact with this email already exists");
        warned.Warnings.ShouldBe(["Anna Existing (Bob Co)"]);

        var accepted = await _service.SaveAsync(model, _alice, new SaveOptions { AcceptWarnings = true }, Ct);
        accepted.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_does_not_warn_when_the_email_is_unchanged_on_edit_or_the_other_contact_is_deleted()
    {
        await AddAsync("Twin", _bobAccount, Bob, "anna@example.com");
        var id = await AddAsync("Smith", email: "anna@example.com");
        var model = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
        model.JobTitle = "Buyer";

        // Unchanged email: no warning even though a twin exists.
        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        // A new contact with that email once the twin is deleted.
        await using (var db = _factory.CreateDbContext())
        {
            foreach (var c in await db.Contacts.Where(c => c.Id != id).ToListAsync(Ct))
            {
                c.IsActive = false;
            }

            await db.SaveChangesAsync(Ct);
        }

        var other = Form("Other");
        other.Email = "other@example.com";
        (await _service.SaveAsync(other, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    // ---- Edit, move, conflict, delete ----

    [Fact]
    public async Task Save_updates_an_existing_contact_and_can_move_it_to_another_account()
    {
        var id = await AddAsync("Smith");
        var model = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
        model.LastName = "Smithson";
        model.AccountId = _bobAccount;
        model.OwnerId = Alice; // keep the owner

        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        var saved = await _service.GetAsync(id, _alice, Ct);
        saved!.LastName.ShouldBe("Smithson");
        saved.AccountName.ShouldBe("Bob Co");
    }

    [Fact]
    public async Task Save_returns_not_found_for_a_missing_contact()
    {
        var model = Form();
        model.Id = 9999;

        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.NotFound);
    }

    [Fact]
    public async Task Save_reports_a_conflict_when_the_row_version_is_stale_and_lets_only_an_admin_overwrite()
    {
        var id = await AddAsync("Contested");
        var stale = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();

        // Someone else saves first. InMemory has no rowversion, so bump the token by hand.
        await using (var db = _factory.CreateDbContext())
        {
            var contact = (await db.Contacts.FindAsync([id], Ct))!;
            contact.Phone = "999";
            contact.RowVersion = [1];
            await db.SaveChangesAsync(Ct);
        }

        stale.Phone = "210";
        var overwrite = new SaveOptions { Overwrite = true };

        (await _service.SaveAsync(stale.Clone(), _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Conflict);
        (await _service.SaveAsync(stale.Clone(), _alice, overwrite, Ct)).Status.ShouldBe(SaveStatus.Conflict);
        (await _service.SaveAsync(stale.Clone(), _admin, overwrite, Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_hides_the_contact()
    {
        var id = await AddAsync("Doomed");

        (await _service.DeleteAsync(id, _alice, Ct)).ShouldBeTrue();

        (await _service.GetAsync(id, _alice, Ct)).ShouldBeNull();
        await using var db = _factory.CreateDbContext();
        (await db.Contacts.IgnoreQueryFilters().SingleAsync(c => c.Id == id, Ct)).IsActive.ShouldBeFalse();
        (await _service.DeleteAsync(9999, _alice, Ct)).ShouldBeFalse();
    }

    // ---- Test plumbing ----

    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CrmDbContext>
    {
        private readonly FakeCurrentUser _currentUser = new() { UserId = Alice };

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
