using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Services;

/// <summary>
/// LeadService rules on the real CrmDbContext model with the EF InMemory provider. Row versions, collations and
/// the atomic Convert are covered by the SQL Server integration tests.
/// </summary>
public class LeadServiceTests
{
    private const string Alice = "user-alice"; // Sales
    private const string Bob = "user-bob"; // Sales

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryFactory _factory;
    private readonly LeadService _service;

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);

    private int _newStatus;
    private int _contactedStatus;
    private int _disqualifiedStatus;
    private int _convertedStatus;

    public LeadServiceTests()
    {
        _factory = new InMemoryFactory(_dbName);
        _service = new LeadService(_factory, new OwnerService(_factory), TimeProvider.System);
        Seed();
    }

    private void Seed()
    {
        using var db = _factory.CreateDbContext();
        db.Users.AddRange(
            new User { Id = Alice, UserName = "alice", DisplayName = "Alice" },
            new User { Id = Bob, UserName = "bob", DisplayName = "Bob" });
        db.AccountStatuses.Add(new AccountStatus { Id = 1, Name = "Active" });
        db.LeadSources.Add(new LeadSource { Id = 1, Name = "Website" });

        // The migration seeds these for SQL Server; the in-memory database starts empty.
        if (!db.LeadStatuses.Any())
        {
            db.LeadStatuses.AddRange(
                new LeadStatus { Id = 1, Name = "New", SortOrder = 10, SystemCode = LeadStatus.New },
                new LeadStatus { Id = 2, Name = "Contacted", SortOrder = 20 },
                new LeadStatus { Id = 4, Name = "Disqualified", SortOrder = 80, SystemCode = LeadStatus.Disqualified },
                new LeadStatus { Id = 5, Name = "Converted", SortOrder = 90, SystemCode = LeadStatus.Converted });
        }

        db.SaveChangesAsync(Ct).GetAwaiter().GetResult();
        _newStatus = 1;
        _contactedStatus = 2;
        _disqualifiedStatus = 4;
        _convertedStatus = 5;
    }

    private LeadEditModel Form(string name = "Maria Papadopoulou") => new()
    {
        Name = name,
        LeadStatusId = _newStatus,
        OwnerId = Alice,
    };

    private async Task<int> AddLeadAsync(
        string name, int? statusId = null, string? company = null, string? email = null, string owner = Alice,
        bool converted = false)
    {
        await using var db = _factory.CreateDbContext();
        var lead = new Lead
        {
            Name = name,
            Company = company,
            Email = email,
            LeadStatusId = statusId ?? (converted ? _convertedStatus : _newStatus),
            OwnerId = owner,
            CreatedBy = owner,
        };
        if (converted)
        {
            var account = new Account { Name = "Conv " + Guid.NewGuid(), AccountStatusId = 1, OwnerId = owner, CreatedBy = owner };
            var contact = new Contact { LastName = "C", Account = account, OwnerId = owner, CreatedBy = owner };
            lead.ConvertedAt = DateTime.UtcNow;
            lead.ConvertedAccount = account;
            lead.ConvertedContact = contact;
        }

        db.Leads.Add(lead);
        await db.SaveChangesAsync(Ct);
        return lead.Id;
    }

    // ---- New and save ----

    [Fact]
    public async Task New_starts_with_status_New_and_the_current_user_as_owner()
    {
        var model = await _service.NewAsync(_alice, Ct);

        model.LeadStatusId.ShouldBe(_newStatus);
        model.OwnerId.ShouldBe(Alice);
    }

    [Fact]
    public async Task Save_creates_a_lead_and_normalises_the_fields()
    {
        var model = Form("  Maria Papadopoulou ");
        model.Company = " Acme SA ";
        model.Email = "  Maria@ACME.gr ";
        model.Phone = " ";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await _service.GetAsync(result.Id, _alice, Ct);
        saved!.Name.ShouldBe("Maria Papadopoulou");
        saved.Company.ShouldBe("Acme SA");
        saved.Email.ShouldBe("maria@acme.gr");
        saved.Phone.ShouldBeNull();
        saved.StatusName.ShouldBe("New");
        saved.OwnerName.ShouldBe("Alice");
        saved.CanConvert.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_rejects_a_missing_name_and_a_bad_email()
    {
        var model = Form("  ");
        model.Email = "not-an-email";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(LeadEditModel.Name));
        result.FieldErrors!.ShouldContainKey(nameof(LeadEditModel.Email));
    }

    [Fact]
    public async Task Save_refuses_to_set_the_Converted_status()
    {
        var model = Form();
        model.LeadStatusId = _convertedStatus;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(LeadEditModel.LeadStatusId));
        (await _service.SearchAsync(new LeadQuery(Scope: ListScope.All, OpenOnly: false), _alice, Ct)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Save_refuses_to_change_a_converted_lead()
    {
        var id = await AddLeadAsync("Done Deal", converted: true);
        var model = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
        model.Name = "Renamed";

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(string.Empty);
        (await _service.GetAsync(id, _alice, Ct))!.Name.ShouldBe("Done Deal");
    }

    [Fact]
    public async Task Save_refuses_an_owner_the_user_may_not_assign()
    {
        var model = Form();
        model.OwnerId = Bob;

        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(LeadEditModel.OwnerId));
    }

    [Fact]
    public async Task A_disqualified_lead_can_be_reopened_by_changing_its_status()
    {
        var id = await AddLeadAsync("Maybe Later", _disqualifiedStatus);
        (await _service.GetAsync(id, _alice, Ct))!.CanConvert.ShouldBeFalse();

        var model = (await _service.GetAsync(id, _alice, Ct))!.ToEditModel();
        model.LeadStatusId = _contactedStatus;
        var result = await _service.SaveAsync(model, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        (await _service.GetAsync(id, _alice, Ct))!.CanConvert.ShouldBeTrue();
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_hides_a_lead_but_never_a_converted_one()
    {
        var open = await AddLeadAsync("Open");
        var converted = await AddLeadAsync("Converted", converted: true);

        (await _service.DeleteAsync(open, _alice, Ct)).ShouldBeTrue();
        (await _service.DeleteAsync(converted, _alice, Ct)).ShouldBeFalse();

        (await _service.GetAsync(open, _alice, Ct)).ShouldBeNull();
        (await _service.GetAsync(converted, _alice, Ct)).ShouldNotBeNull();
    }

    // ---- List ----

    [Fact]
    public async Task The_list_hides_Converted_and_Disqualified_leads_unless_asked()
    {
        await AddLeadAsync("Open one");
        await AddLeadAsync("Disqualified one", _disqualifiedStatus);
        await AddLeadAsync("Converted one", converted: true);

        var open = await _service.SearchAsync(new LeadQuery(Scope: ListScope.All), _alice, Ct);
        var all = await _service.SearchAsync(new LeadQuery(Scope: ListScope.All, OpenOnly: false), _alice, Ct);
        var converted = await _service.SearchAsync(new LeadQuery(Scope: ListScope.All, StatusId: _convertedStatus), _alice, Ct);

        open.Items.Select(l => l.Name).ShouldBe(["Open one"]);
        all.TotalCount.ShouldBe(3);
        converted.Items.Select(l => l.Name).ShouldBe(["Converted one"]);
    }

    [Fact]
    public async Task The_list_scope_Mine_shows_only_the_users_own_leads()
    {
        await AddLeadAsync("Mine", owner: Alice);
        await AddLeadAsync("Theirs", owner: Bob);

        var mine = await _service.SearchAsync(new LeadQuery(Scope: ListScope.Mine), _alice, Ct);

        mine.Items.Select(l => l.Name).ShouldBe(["Mine"]);
    }

    // ---- Convert prefill and account suggestions ----

    [Fact]
    public async Task Prefill_splits_the_name_and_suggests_accounts_by_company_name_and_email_domain()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var byName = new Account { Name = "Acme SA", AccountStatusId = 1, OwnerId = Alice, CreatedBy = Alice };
            var byDomain = new Account { Name = "Other Name Ltd", AccountStatusId = 1, OwnerId = Alice, CreatedBy = Alice };
            byDomain.Contacts.Add(new Contact { LastName = "Smith", Email = "jo@acme.gr", OwnerId = Alice, CreatedBy = Alice });
            var both = new Account { Name = "Acme Hellas", AccountStatusId = 1, OwnerId = Alice, CreatedBy = Alice, Email = "info@acme.gr" };
            db.Accounts.AddRange(byName, byDomain, both, new Account { Name = "Unrelated", AccountStatusId = 1, OwnerId = Alice, CreatedBy = Alice });
            await db.SaveChangesAsync(Ct);
        }

        var id = await AddLeadAsync("Maria Papadopoulou", company: "Acme", email: "maria@acme.gr");

        var prefill = await _service.GetConvertPrefillAsync(id, _alice, Ct);

        prefill.ShouldNotBeNull();
        prefill.ContactFirstName.ShouldBe("Maria");
        prefill.ContactLastName.ShouldBe("Papadopoulou");
        prefill.AccountName.ShouldBe("Acme");
        prefill.OpportunityName.ShouldBe("Acme deal");
        prefill.AccountMatches.Select(m => m.Name).ShouldBe(["Acme Hellas", "Acme SA", "Other Name Ltd"]);
        prefill.AccountMatches[0].Reasons.ShouldBe(AccountMatchReason.CompanyName | AccountMatchReason.EmailDomain);
        prefill.AccountMatches[1].Reasons.ShouldBe(AccountMatchReason.CompanyName);
        prefill.AccountMatches[2].Reasons.ShouldBe(AccountMatchReason.EmailDomain);
    }

    [Fact]
    public async Task Prefill_does_not_match_accounts_by_a_free_mailbox_domain()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var account = new Account { Name = "Some Firm", AccountStatusId = 1, OwnerId = Alice, CreatedBy = Alice };
            account.Contacts.Add(new Contact { LastName = "Smith", Email = "jo@gmail.com", OwnerId = Alice, CreatedBy = Alice });
            db.Accounts.Add(account);
            await db.SaveChangesAsync(Ct);
        }

        var id = await AddLeadAsync("Maria Papadopoulou", email: "maria@gmail.com");

        (await _service.GetConvertPrefillAsync(id, _alice, Ct))!.AccountMatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Prefill_is_null_for_a_lead_that_cannot_be_converted()
    {
        var disqualified = await AddLeadAsync("No", _disqualifiedStatus);
        var converted = await AddLeadAsync("Done", converted: true);

        (await _service.GetConvertPrefillAsync(disqualified, _alice, Ct)).ShouldBeNull();
        (await _service.GetConvertPrefillAsync(converted, _alice, Ct)).ShouldBeNull();
        (await _service.GetConvertPrefillAsync(9999, _alice, Ct)).ShouldBeNull();
    }

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
