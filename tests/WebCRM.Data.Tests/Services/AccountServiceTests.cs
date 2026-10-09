using WebCRM.Core.Records;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Services;

/// <summary>
/// AccountService on the real CrmDbContext model with the EF InMemory provider. InMemory ignores
/// collations, unique indexes and rowversion, so those are covered by the checks in the service
/// (and, for SQL Server behaviour, by manual checks against the real database).
/// </summary>
public class AccountServiceTests : IDisposable
{
    private const string Alice = "user-alice";
    private const string Bob = "user-bob";
    private const string Carol = "user-carol";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly TestFactory _factory;
    private readonly AccountService _service;

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: 1);
    private readonly UserContext _manager = new(Carol, RoleNames.Manager, TeamId: 1);
    private readonly UserContext _admin = new("user-admin", RoleNames.Admin, TeamId: null);

    public AccountServiceTests()
    {
        _factory = new TestFactory(_dbName);
        _service = new AccountService(_factory, new OwnerService(_factory));
        Seed();
    }

    public void Dispose() => _factory.Dispose();

    private void Seed()
    {
        using var db = _factory.CreateDbContext();
        db.Users.AddRange(
            new User { Id = Alice, UserName = "alice", DisplayName = "Alice", TeamId = 1 },
            new User { Id = Bob, UserName = "bob", DisplayName = "Bob", TeamId = 2 },
            new User { Id = Carol, UserName = "carol", DisplayName = "Carol", TeamId = 1 },
            new User { Id = "user-admin", UserName = "admin", DisplayName = "Admin" },
            new User { Id = "user-gone", UserName = "gone", DisplayName = "Gone", IsActive = false });
        db.AccountStatuses.AddRange(
            new AccountStatus { Id = 1, Name = "Prospect", SortOrder = 10 },
            new AccountStatus { Id = 2, Name = "Active", SortOrder = 20 },
            new AccountStatus { Id = 3, Name = "Retired", SortOrder = 30, IsActive = false });
        db.Industries.Add(new Industry { Id = 1, Name = "Retail", SortOrder = 10 });
        db.SaveChanges();
    }

    private static AccountEditModel Form(string name, string owner = Alice) => new()
    {
        Name = name,
        AccountStatusId = 1,
        OwnerId = owner,
    };

    private async Task<int> AddAsync(string name, string owner = Alice, int statusId = 1, int? industryId = null)
    {
        await using var db = _factory.CreateDbContext();
        var account = new Account
        {
            Name = name,
            OwnerId = owner,
            AccountStatusId = statusId,
            IndustryId = industryId,
            CreatedBy = owner,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    // ---- List ----

    [Fact]
    public async Task Search_scope_mine_returns_only_own_accounts()
    {
        await AddAsync("Alice Co", Alice);
        await AddAsync("Bob Co", Bob);

        var result = await _service.SearchAsync(new AccountQuery(ListScope.Mine), _alice, cancellationToken: Ct);

        result.Items.Select(i => i.Name).ShouldBe(["Alice Co"]);
        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Search_scope_team_returns_accounts_owned_by_team_members()
    {
        await AddAsync("Alice Co", Alice);
        await AddAsync("Carol Co", Carol);
        await AddAsync("Bob Co", Bob);

        var result = await _service.SearchAsync(new AccountQuery(ListScope.Team), _manager, cancellationToken: Ct);

        result.Items.Select(i => i.Name).ShouldBe(["Alice Co", "Carol Co"]);
    }

    [Fact]
    public async Task Search_scope_team_without_a_team_returns_nothing()
    {
        await AddAsync("Alice Co", Alice);

        var result = await _service.SearchAsync(new AccountQuery(ListScope.Team), _admin, cancellationToken: Ct);

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Search_scope_all_returns_everything_visible()
    {
        await AddAsync("Alice Co", Alice);
        await AddAsync("Bob Co", Bob);

        var result = await _service.SearchAsync(new AccountQuery(ListScope.All), _alice, cancellationToken: Ct);

        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Search_hides_soft_deleted_accounts()
    {
        var id = await AddAsync("Gone Co");
        await using (var db = _factory.CreateDbContext())
        {
            (await db.Accounts.FindAsync([id], Ct))!.IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        var result = await _service.SearchAsync(new AccountQuery(ListScope.All), _alice, cancellationToken: Ct);

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Search_pages_on_the_server_and_reports_the_total()
    {
        for (var i = 1; i <= 7; i++)
        {
            await AddAsync($"Account {i:00}");
        }

        var page2 = await _service.SearchAsync(new AccountQuery(ListScope.All, Page: 2, PageSize: 3), _alice, cancellationToken: Ct);

        page2.TotalCount.ShouldBe(7);
        page2.Items.Select(i => i.Name).ShouldBe(["Account 04", "Account 05", "Account 06"]);
    }

    [Fact]
    public async Task Search_sorts_descending_by_the_chosen_column()
    {
        await AddAsync("Alpha");
        await AddAsync("Bravo");

        var result = await _service.SearchAsync(
            new AccountQuery(ListScope.All, Sort: AccountSort.Name, Descending: true), _alice, cancellationToken: Ct);

        result.Items.Select(i => i.Name).ShouldBe(["Bravo", "Alpha"]);
    }

    [Fact]
    public async Task Search_filters_by_text_status_industry_owner_and_city()
    {
        var a = await AddAsync("Acme Retail", Alice, statusId: 2, industryId: 1);
        await AddAsync("Acme Other", Bob, statusId: 1);
        await AddAsync("Zenith", Alice, statusId: 2, industryId: 1);
        await using (var db = _factory.CreateDbContext())
        {
            db.Addresses.Add(new Address { AccountId = a, AddressType = AddressType.Billing, City = "Athens" });
            await db.SaveChangesAsync(Ct);
        }

        var all = new AccountQuery(ListScope.All);

        (await _service.SearchAsync(all with { Search = "Acme" }, _alice, cancellationToken: Ct)).TotalCount.ShouldBe(2);
        (await _service.SearchAsync(all with { StatusId = 2 }, _alice, cancellationToken: Ct)).TotalCount.ShouldBe(2);
        (await _service.SearchAsync(all with { IndustryId = 1 }, _alice, cancellationToken: Ct)).TotalCount.ShouldBe(2);
        (await _service.SearchAsync(all with { OwnerId = Bob }, _alice, cancellationToken: Ct)).Items.Single().Name.ShouldBe("Acme Other");
        (await _service.SearchAsync(all with { City = "Athens" }, _alice, cancellationToken: Ct)).Items.Single().Name.ShouldBe("Acme Retail");
        (await _service.SearchAsync(all with { Search = "Athens" }, _alice, cancellationToken: Ct)).Items.Single().Name.ShouldBe("Acme Retail");
    }

    [Fact]
    public async Task Search_projects_names_city_and_counts_without_loading_children()
    {
        var id = await AddAsync("Acme", Alice, statusId: 2, industryId: 1);
        await using (var db = _factory.CreateDbContext())
        {
            var open = new Stage { Id = 1, Name = "Lead" };
            var won = new Stage { Id = 2, Name = "Won", IsWon = true };
            db.Stages.AddRange(open, won);
            db.Addresses.Add(new Address { AccountId = id, AddressType = AddressType.Billing, City = "Athens" });
            db.Addresses.Add(new Address { AccountId = id, AddressType = AddressType.Shipping, City = "Patras" });
            db.Opportunities.AddRange(
                new Opportunity { Name = "O1", AccountId = id, StageId = 1, OwnerId = Alice, CreatedBy = Alice },
                new Opportunity { Name = "O2", AccountId = id, StageId = 2, OwnerId = Alice, CreatedBy = Alice });
            db.Activities.Add(new Activity
            {
                Subject = "Call",
                AccountId = id,
                ActivityTypeId = 1,
                OwnerId = Alice,
                CreatedBy = Alice,
                DoneAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            });
            db.ActivityTypes.Add(new ActivityType { Id = 1, Name = "Call" });
            await db.SaveChangesAsync(Ct);
        }

        var row = (await _service.SearchAsync(new AccountQuery(ListScope.All), _alice, cancellationToken: Ct)).Items.Single();

        row.IndustryName.ShouldBe("Retail");
        row.StatusName.ShouldBe("Active");
        row.City.ShouldBe("Athens");
        row.OwnerName.ShouldBe("Alice");
        row.OpenOpportunities.ShouldBe(1);
        row.LastActivityAt.ShouldBe(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    // ---- Detail ----

    [Fact]
    public async Task Get_returns_null_for_missing_and_deleted_accounts()
    {
        (await _service.GetAsync(999, _alice, cancellationToken: Ct)).ShouldBeNull();

        var id = await AddAsync("Soon gone");
        await using (var db = _factory.CreateDbContext())
        {
            (await db.Accounts.FindAsync([id], Ct))!.IsActive = false;
            await db.SaveChangesAsync(Ct);
        }

        (await _service.GetAsync(id, _alice, cancellationToken: Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task New_defaults_owner_to_the_current_user_and_status_to_the_first_active()
    {
        var model = await _service.NewAsync(_alice, Ct);

        model.OwnerId.ShouldBe(Alice);
        model.AccountStatusId.ShouldBe(1);
    }

    // ---- Save ----

    [Fact]
    public async Task Save_creates_an_account_and_trims_and_blanks_optional_fields()
    {
        var model = Form("  Acme  ");
        model.VatNumber = "  ";
        model.Phone = " 210 123 ";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await _service.GetAsync(result.Id, _alice, cancellationToken: Ct);
        saved!.Name.ShouldBe("Acme");
        saved.VatNumber.ShouldBeNull();
        saved.Phone.ShouldBe("210 123");
        saved.OwnerId.ShouldBe(Alice);
    }

    [Fact]
    public async Task Save_rejects_invalid_input_with_field_errors()
    {
        var model = Form("");
        model.Website = "not a url";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.Keys.ShouldContain(nameof(AccountEditModel.Name));
        result.FieldErrors.Keys.ShouldContain(nameof(AccountEditModel.Website));
    }

    [Fact]
    public async Task Save_blocks_a_duplicate_name_among_active_accounts()
    {
        await AddAsync("Acme");

        var result = await _service.SaveAsync(Form("Acme"), _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(AccountEditModel.Name));
    }

    [Fact]
    public async Task Save_allows_reusing_the_name_of_a_deleted_account()
    {
        var id = await AddAsync("Acme");
        await _service.DeleteAsync(id, _alice, cancellationToken: Ct);

        var result = await _service.SaveAsync(Form("Acme"), _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_blocks_a_duplicate_vat_number()
    {
        var first = Form("First");
        first.VatNumber = "EL094259216";
        (await _service.SaveAsync(first, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        var second = Form("Second");
        second.VatNumber = "el 094.259-216"; // the same number, written differently
        var result = await _service.SaveAsync(second, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(AccountEditModel.VatNumber));
    }

    [Fact]
    public async Task Save_warns_about_similar_names_and_saves_when_accepted()
    {
        await AddAsync("Acme Retail Ltd");

        var warned = await _service.SaveAsync(Form("Acme Retail"), _alice, cancellationToken: Ct);
        warned.Status.ShouldBe(SaveStatus.Warning);
        warned.Warnings.ShouldBe(["Acme Retail Ltd"]);

        var accepted = await _service.SaveAsync(
            Form("Acme Retail"), _alice, new SaveOptions { AcceptWarnings = true }, Ct);
        accepted.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_does_not_warn_again_when_the_name_is_unchanged_on_edit()
    {
        await AddAsync("Acme Retail Ltd");
        var id = await AddAsync("Acme Retail");
        var model = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();
        model.Phone = "210";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_updates_an_existing_account()
    {
        var id = await AddAsync("Acme");
        var model = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();
        model.Name = "Acme Hellas";
        model.IndustryId = 1;

        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        var saved = await _service.GetAsync(id, _alice, cancellationToken: Ct);
        saved!.Name.ShouldBe("Acme Hellas");
        saved.IndustryName.ShouldBe("Retail");
    }

    [Fact]
    public async Task Save_returns_not_found_for_a_missing_account()
    {
        var model = Form("Ghost");
        model.Id = 999;

        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.NotFound);
    }

    [Fact]
    public async Task Save_rejects_a_deactivated_status_unless_the_record_already_has_it()
    {
        var model = Form("Acme");
        model.AccountStatusId = 3;
        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);

        var id = await AddAsync("Old", statusId: 3);
        var existing = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();
        existing.Phone = "210";
        (await _service.SaveAsync(existing, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_only_lets_a_user_assign_owners_they_are_allowed_to()
    {
        // Sales can only own their own records.
        (await _service.SaveAsync(Form("For Bob", Bob), _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);

        // A Manager can assign within the team, but not to another team.
        (await _service.SaveAsync(Form("For Alice", Alice), _manager, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        (await _service.SaveAsync(Form("For Bob 2", Bob), _manager, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);

        // An Admin can assign to any active user, but not an inactive one.
        (await _service.SaveAsync(Form("For Bob 3", Bob), _admin, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
        (await _service.SaveAsync(Form("For Gone", "user-gone"), _admin, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Invalid);
    }

    [Fact]
    public async Task Save_keeps_an_inactive_owner_while_the_record_is_edited()
    {
        var id = await AddAsync("Orphan", owner: "user-gone");
        var model = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();
        model.Phone = "210";

        (await _service.SaveAsync(model, _alice, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_reports_a_conflict_with_who_and_when_when_the_row_version_is_stale()
    {
        var id = await AddAsync("Acme");
        var stale = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();

        // Someone else saves first. InMemory has no rowversion, so bump the token by hand.
        await using (var db = _factory.CreateDbContext())
        {
            var account = (await db.Accounts.FindAsync([id], Ct))!;
            account.Phone = "999";
            account.RowVersion = [1];
            await db.SaveChangesAsync(Ct);
        }

        stale.Phone = "210";
        var result = await _service.SaveAsync(stale, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Conflict);
        result.Conflict.ShouldNotBeNull();
    }

    [Fact]
    public async Task Save_overwrites_a_stale_version_for_an_admin_only()
    {
        var id = await AddAsync("Acme");
        var stale = (await _service.GetAsync(id, _alice, cancellationToken: Ct))!.ToEditModel();
        await using (var db = _factory.CreateDbContext())
        {
            var account = (await db.Accounts.FindAsync([id], Ct))!;
            account.RowVersion = [1];
            await db.SaveChangesAsync(Ct);
        }

        stale.Phone = "210";
        var overwrite = new SaveOptions { Overwrite = true };

        (await _service.SaveAsync(stale.Clone(), _alice, overwrite, Ct)).Status.ShouldBe(SaveStatus.Conflict);
        (await _service.SaveAsync(stale.Clone(), _admin, overwrite, Ct)).Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Save_normalises_the_vat_number_and_adds_https_to_the_website()
    {
        var model = Form("Acme");
        model.VatNumber = "el 094.259-216";
        model.Website = "  mexdb.com/about ";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await _service.GetAsync(result.Id, _alice, Ct);
        saved!.VatNumber.ShouldBe("EL094259216");
        saved.Website.ShouldBe("https://mexdb.com/about");
    }

    private async Task SetCompanyCountryAsync(string? countryCode)
    {
        await using var db = _factory.CreateDbContext();
        db.CompanySettings.Add(new CompanySetting { CompanyName = "Test Co", DefaultCountryCode = countryCode });
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task A_greek_company_stores_a_bare_9_digit_number_as_an_EL_number_and_checks_its_check_digit()
    {
        await SetCompanyCountryAsync("GR");
        var model = Form("Acme");
        model.VatNumber = "094 259 216";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        (await _service.GetAsync(result.Id, _alice, Ct))!.VatNumber.ShouldBe("EL094259216");

        var wrong = Form("Wrong");
        wrong.VatNumber = "123456789";
        var rejected = await _service.SaveAsync(wrong, _alice, cancellationToken: Ct);
        rejected.Status.ShouldBe(SaveStatus.Invalid);
        rejected.FieldErrors![nameof(AccountEditModel.VatNumber)].ShouldBe(VatNumberRules.InvalidGreekCheckDigitMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("US")]
    public async Task A_company_outside_Greece_stores_a_bare_9_digit_number_as_typed(string? country)
    {
        await SetCompanyCountryAsync(country);
        var model = Form("Acme Inc");
        model.VatNumber = "12-3456789"; // a US EIN: 9 digits, and it would fail the Greek check digit

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        (await _service.GetAsync(result.Id, _alice, Ct))!.VatNumber.ShouldBe("123456789");
    }

    [Theory]
    [InlineData("GB 123 4567 89", "GB123456789")]
    [InlineData("CHE-123.456.789 MWST", "CHE123456789MWST")]
    [InlineData("no 123 456 785 mva", "NO123456785MVA")]
    [InlineData("de 123 456 789", "DE123456789")]
    public async Task Foreign_tax_ids_are_normalised_and_saved(string typed, string stored)
    {
        var model = Form("Foreign Co");
        model.VatNumber = typed;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        (await _service.GetAsync(result.Id, _alice, Ct))!.VatNumber.ShouldBe(stored);
    }

    [Theory]
    [InlineData("123", VatNumberRules.InvalidMessage)]
    [InlineData("EL12345", VatNumberRules.InvalidGreekMessage)]
    [InlineData("EL123456789", VatNumberRules.InvalidGreekCheckDigitMessage)]
    [InlineData("DE 12 34 56 78 90 12 34 56 78 90", VatNumberRules.InvalidMessage)]
    [InlineData("DE123456789€", VatNumberRules.InvalidMessage)]
    public async Task Save_rejects_an_invalid_vat_number_with_a_clear_message_on_that_field(string vat, string message)
    {
        var model = Form("Acme");
        model.VatNumber = vat;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors![nameof(AccountEditModel.VatNumber)].ShouldBe(message);
    }

    [Theory]
    [InlineData("ftp://mexdb.com")]
    [InlineData("not a url")]
    [InlineData("https://user:pw@mexdb.com")]
    public async Task Save_rejects_a_website_that_is_still_not_an_http_or_https_url(string website)
    {
        var model = Form("Acme");
        model.Website = website;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors![nameof(AccountEditModel.Website)].ShouldBe(WebsiteRules.InvalidMessage);
    }

    [Fact]
    public async Task Save_rejects_a_website_that_is_too_long_once_https_is_added()
    {
        var model = Form("Acme");
        model.Website = new string('a', 295) + ".com"; // 299 characters: fine as typed, 307 with https://

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(AccountEditModel.Website));
    }

    // ---- Account picker ----

    [Fact]
    public async Task Picker_finds_names_containing_the_text_with_names_starting_with_it_first()
    {
        await AddAsync("Zebra Acme");
        await AddAsync("Acme Corp");
        await AddAsync("Beta Ltd");
        await AddAsync("Acme Alpha");

        var items = await _service.SearchPickerAsync("Acme", _alice, cancellationToken: Ct);

        items.Select(i => i.Name).ShouldBe(["Acme Alpha", "Acme Corp", "Zebra Acme"]);
    }

    [Fact]
    public async Task Picker_returns_at_most_ten_and_the_first_by_name_for_blank_text()
    {
        for (var i = 1; i <= 15; i++)
        {
            await AddAsync($"Account {i:00}");
        }

        var items = await _service.SearchPickerAsync(null, _alice, cancellationToken: Ct);

        items.Count.ShouldBe(10);
        items[0].Name.ShouldBe("Account 01");
        (await _service.SearchPickerAsync("Account", _alice, 3, Ct)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Picker_hides_deleted_accounts_and_resolves_a_single_item()
    {
        var keep = await AddAsync("Keep Co");
        var gone = await AddAsync("Gone Co");
        await _service.DeleteAsync(gone, _alice, Ct);

        (await _service.SearchPickerAsync("Co", _alice, cancellationToken: Ct)).Select(i => i.Name).ShouldBe(["Keep Co"]);
        (await _service.GetPickerItemAsync(keep, _alice, Ct)).ShouldBe(new AccountPickerItem(keep, "Keep Co"));
        (await _service.GetPickerItemAsync(gone, _alice, Ct)).ShouldBeNull();
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_soft_deletes_the_account_and_its_contacts()
    {
        var id = await AddAsync("Acme");
        await using (var db = _factory.CreateDbContext())
        {
            db.Contacts.Add(new Contact { LastName = "Smith", AccountId = id, OwnerId = Alice, CreatedBy = Alice });
            await db.SaveChangesAsync(Ct);
        }

        var impact = await _service.GetDeleteImpactAsync(id, _alice, cancellationToken: Ct);
        impact.ShouldBe(new AccountDeleteImpact(Contacts: 1, OpenOpportunities: 0));

        (await _service.DeleteAsync(id, _alice, cancellationToken: Ct)).Status.ShouldBe(AccountDeleteStatus.Deleted);

        await using var check = _factory.CreateDbContext();
        (await check.Accounts.IgnoreQueryFilters().SingleAsync(a => a.Id == id, Ct)).IsActive.ShouldBeFalse();
        (await check.Contacts.IgnoreQueryFilters().SingleAsync(Ct)).IsActive.ShouldBeFalse();
        (await _service.GetAsync(id, _alice, cancellationToken: Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_is_blocked_while_the_account_has_open_opportunities()
    {
        var id = await AddAsync("Acme");
        await using (var db = _factory.CreateDbContext())
        {
            db.Stages.Add(new Stage { Id = 1, Name = "Lead" });
            db.Opportunities.Add(new Opportunity { Name = "Deal", AccountId = id, StageId = 1, OwnerId = Alice, CreatedBy = Alice });
            await db.SaveChangesAsync(Ct);
        }

        var result = await _service.DeleteAsync(id, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(AccountDeleteStatus.Blocked);
        result.Message!.ShouldContain("1 open opportunity");
        (await _service.GetAsync(id, _alice, cancellationToken: Ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_returns_not_found_for_a_missing_account()
    {
        (await _service.DeleteAsync(999, _alice, cancellationToken: Ct)).Status.ShouldBe(AccountDeleteStatus.NotFound);
    }

    // ---- Test plumbing ----

    /// <summary>A context factory over one named InMemory database, stamping audit columns like the app does.</summary>
    private sealed class TestFactory(string dbName) : IDbContextFactory<CrmDbContext>, IDisposable
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

        public void Dispose()
        {
        }
    }
}
