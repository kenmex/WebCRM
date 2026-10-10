using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// LeadService against real SQL Server. The Convert tests are the point of this class: one transaction, nothing
/// half-created when a step fails, and exactly one winner when two people convert the same lead at once.
/// </summary>
public class LeadServiceSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Alice = "ld-alice";
    private const string Bob = "ld-bob";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly LeadService _service;

    // Every test uses its own prefix, so tests sharing the database never see each other's rows.
    private readonly string _prefix = "L" + Guid.NewGuid().ToString("N")[..8] + " ";

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);
    private readonly UserContext _bob = new(Bob, RoleNames.Sales, TeamId: null);

    public LeadServiceSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _service = new LeadService(_factory, new OwnerService(_factory), TimeProvider.System);
    }

    private async Task EnsureReferenceDataAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        await using var db = _factory.CreateDbContext();
        if (await db.Users.AnyAsync(u => u.Id == Alice, Ct))
        {
            return;
        }

        // Users first: every audited insert needs its CreatedBy user to exist.
        db.Users.AddRange(User(Alice, "Alice"), User(Bob, "Bob"), User("system-test", "System test"));
        await db.SaveChangesAsync(Ct);

        db.AccountStatuses.Add(new AccountStatus { Name = "LD Active", SortOrder = 10 });
        await db.SaveChangesAsync(Ct);
    }

    private static User User(string id, string name) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        DisplayName = name,
    };

    private async Task<int> AddLeadAsync(string suffix = "Maria Papadopoulou", string? statusCode = null, string owner = Alice)
    {
        await EnsureReferenceDataAsync();
        await using var db = _factory.CreateDbContext();
        var statusId = await db.LeadStatuses.Where(s => s.SystemCode == (statusCode ?? LeadStatus.New)).Select(s => s.Id).SingleAsync(Ct);
        var lead = new Lead
        {
            Name = _prefix + suffix,
            Company = _prefix + "Co",
            Email = "maria@example.com",
            LeadStatusId = statusId,
            OwnerId = owner,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync(Ct);
        return lead.Id;
    }

    /// <summary>An activity (one of them soft-deleted) and a note on the lead.</summary>
    private async Task AddHistoryAsync(int leadId)
    {
        await using var db = _factory.CreateDbContext();
        var callTypeId = await db.ActivityTypes.Where(t => t.SystemCode == ActivityType.Call).Select(t => t.Id).SingleAsync(Ct);
        db.Activities.AddRange(
            new Activity { ActivityTypeId = callTypeId, Subject = _prefix + "Intro call", OwnerId = Alice, LeadId = leadId },
            new Activity { ActivityTypeId = callTypeId, Subject = _prefix + "Deleted call", OwnerId = Alice, LeadId = leadId, IsActive = false });
        db.Notes.Add(new Note { Body = _prefix + "Wants a quote", LeadId = leadId });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> AddAccountAsync(string name, string owner = Alice, bool withContact = false)
    {
        await using var db = _factory.CreateDbContext();
        var statusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct);
        var account = new Account { Name = _prefix + name, AccountStatusId = statusId, OwnerId = owner };
        if (withContact)
        {
            account.Contacts.Add(new Contact { FirstName = "Existing", LastName = _prefix + "Contact", OwnerId = owner });
        }

        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    private LeadConvertRequest NewAccountRequest(int leadId, string accountName = "Acct", bool opportunity = true) => new()
    {
        LeadId = leadId,
        NewAccountName = _prefix + accountName,
        ContactFirstName = "Maria",
        ContactLastName = _prefix + "Papadopoulou",
        ContactEmail = "Maria@Example.com",
        ContactJobTitle = "Buyer",
        CreateOpportunity = opportunity,
        OpportunityName = _prefix + "deal",
    };

    private async Task<Counts> CountsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return new Counts(
            await db.Accounts.IgnoreQueryFilters().CountAsync(a => a.Name.StartsWith(_prefix), Ct),
            await db.Contacts.IgnoreQueryFilters().CountAsync(c => c.LastName.StartsWith(_prefix), Ct),
            await db.Opportunities.IgnoreQueryFilters().CountAsync(o => o.Name.StartsWith(_prefix), Ct));
    }

    private sealed record Counts(int Accounts, int Contacts, int Opportunities);

    // ---- Success ----

    [Fact]
    public async Task Convert_creates_the_account_contact_and_opportunity_and_marks_the_lead_converted()
    {
        var leadId = await AddLeadAsync();
        await AddHistoryAsync(leadId);

        var result = await _service.ConvertAsync(NewAccountRequest(leadId), _alice, Ct);

        result.Status.ShouldBe(ConvertStatus.Converted);
        (await CountsAsync()).ShouldBe(new Counts(1, 1, 1));

        await using var db = _factory.CreateDbContext();
        var account = await db.Accounts.SingleAsync(a => a.Id == result.AccountId, Ct);
        account.Name.ShouldBe(_prefix + "Acct");
        account.OwnerId.ShouldBe(Alice);

        var contact = await db.Contacts.SingleAsync(c => c.Id == result.ContactId, Ct);
        contact.AccountId.ShouldBe(account.Id);
        contact.Email.ShouldBe("maria@example.com");
        contact.JobTitle.ShouldBe("Buyer");
        contact.OwnerId.ShouldBe(Alice);

        // The first open stage by sort order is Prospecting (10%), and the close date is set.
        var opportunity = await db.Opportunities.Include(o => o.Stage).SingleAsync(o => o.Id == result.OpportunityId, Ct);
        opportunity.Name.ShouldBe(_prefix + "deal");
        opportunity.AccountId.ShouldBe(account.Id);
        opportunity.PrimaryContactId.ShouldBe(contact.Id);
        opportunity.Stage.Name.ShouldBe("Prospecting");
        opportunity.Probability.ShouldBe(10m);
        opportunity.Amount.ShouldBe(0m);
        opportunity.OwnerId.ShouldBe(Alice);
        opportunity.CloseDate.ShouldBeGreaterThan(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28));

        var detail = (await _service.GetAsync(leadId, _alice, Ct))!;
        detail.StatusCode.ShouldBe(LeadStatus.Converted);
        detail.IsConverted.ShouldBeTrue();
        detail.CanConvert.ShouldBeFalse();
        detail.ConvertedAt.ShouldNotBeNull();
        detail.ConvertedAccountId.ShouldBe(account.Id);
        detail.ConvertedAccountName.ShouldBe(account.Name);
        detail.ConvertedContactId.ShouldBe(contact.Id);
        detail.ConvertedContactName.ShouldBe("Maria " + _prefix + "Papadopoulou");
        detail.ConvertedOpportunityId.ShouldBe(opportunity.Id);
        detail.ConvertedOpportunityName.ShouldBe(opportunity.Name);
    }

    [Fact]
    public async Task Convert_moves_the_leads_activities_and_notes_to_the_contact()
    {
        var leadId = await AddLeadAsync();
        await AddHistoryAsync(leadId);

        var result = await _service.ConvertAsync(NewAccountRequest(leadId, opportunity: false), _alice, Ct);

        result.Status.ShouldBe(ConvertStatus.Converted);
        result.OpportunityId.ShouldBeNull();
        await using var db = _factory.CreateDbContext();

        // Both activities moved, the soft-deleted one too; nothing is left on the lead.
        var activities = await db.Activities.IgnoreQueryFilters().Where(a => a.Subject.StartsWith(_prefix)).ToListAsync(Ct);
        activities.Count.ShouldBe(2);
        activities.ShouldAllBe(a => a.ContactId == result.ContactId && a.LeadId == null);
        var note = await db.Notes.SingleAsync(n => n.Body.StartsWith(_prefix), Ct);
        note.ContactId.ShouldBe(result.ContactId);
        note.LeadId.ShouldBeNull();
    }

    [Fact]
    public async Task Convert_into_an_existing_account_and_contact_creates_nothing_new()
    {
        var leadId = await AddLeadAsync();
        await AddHistoryAsync(leadId);

        // A teammate's account: linking is allowed when it is visible (D7).
        var accountId = await AddAccountAsync("Bobs account", owner: Bob, withContact: true);
        int contactId;
        await using (var db = _factory.CreateDbContext())
        {
            contactId = await db.Contacts.Where(c => c.AccountId == accountId).Select(c => c.Id).SingleAsync(Ct);
        }

        var result = await _service.ConvertAsync(
            new LeadConvertRequest { LeadId = leadId, ExistingAccountId = accountId, ExistingContactId = contactId },
            _alice, Ct);

        result.Status.ShouldBe(ConvertStatus.Converted);
        result.AccountId.ShouldBe(accountId);
        result.ContactId.ShouldBe(contactId);
        result.OpportunityId.ShouldBeNull();
        (await CountsAsync()).ShouldBe(new Counts(1, 1, 0));

        await using var check = _factory.CreateDbContext();
        var lead = await check.Leads.Include(l => l.LeadStatus).SingleAsync(l => l.Id == leadId, Ct);
        lead.LeadStatus.SystemCode.ShouldBe(LeadStatus.Converted);
        lead.ConvertedAccountId.ShouldBe(accountId);
        lead.ConvertedContactId.ShouldBe(contactId);
        (await check.Activities.IgnoreQueryFilters().Where(a => a.Subject.StartsWith(_prefix)).ToListAsync(Ct))
            .ShouldAllBe(a => a.ContactId == contactId && a.LeadId == null);
    }

    [Fact]
    public async Task Convert_into_an_existing_account_with_a_new_contact_and_opportunity()
    {
        var leadId = await AddLeadAsync();
        var accountId = await AddAccountAsync("Existing", owner: Bob);
        var request = NewAccountRequest(leadId);
        request.NewAccountName = null;
        request.ExistingAccountId = accountId;

        var result = await _service.ConvertAsync(request, _alice, Ct);

        result.Status.ShouldBe(ConvertStatus.Converted);
        result.AccountId.ShouldBe(accountId);
        (await CountsAsync()).ShouldBe(new Counts(1, 1, 1));
        await using var db = _factory.CreateDbContext();
        var contact = await db.Contacts.SingleAsync(c => c.Id == result.ContactId, Ct);
        contact.AccountId.ShouldBe(accountId);
        contact.OwnerId.ShouldBe(Alice); // D7: owned by the converting user, not the account's owner
        (await db.Opportunities.SingleAsync(o => o.Id == result.OpportunityId, Ct)).AccountId.ShouldBe(accountId);
    }

    // ---- Refusals: nothing is created ----

    [Fact]
    public async Task A_disqualified_lead_cannot_be_converted()
    {
        var leadId = await AddLeadAsync(statusCode: LeadStatus.Disqualified);

        var result = await _service.ConvertAsync(NewAccountRequest(leadId), _alice, Ct);

        result.Status.ShouldBe(ConvertStatus.Disqualified);
        (await CountsAsync()).ShouldBe(new Counts(0, 0, 0));
    }

    [Fact]
    public async Task A_converted_lead_cannot_be_converted_again()
    {
        var leadId = await AddLeadAsync();
        (await _service.ConvertAsync(NewAccountRequest(leadId), _alice, Ct)).Status.ShouldBe(ConvertStatus.Converted);

        var again = await _service.ConvertAsync(NewAccountRequest(leadId, accountName: "Second"), _alice, Ct);

        again.Status.ShouldBe(ConvertStatus.AlreadyConverted);
        (await CountsAsync()).ShouldBe(new Counts(1, 1, 1));
    }

    [Fact]
    public async Task Convert_of_a_missing_lead_is_not_found()
    {
        await EnsureReferenceDataAsync();

        (await _service.ConvertAsync(NewAccountRequest(int.MaxValue), _alice, Ct)).Status.ShouldBe(ConvertStatus.NotFound);
    }

    [Fact]
    public async Task Invalid_input_creates_nothing_and_names_the_fields()
    {
        var leadId = await AddLeadAsync();
        var existing = await AddAccountAsync("Taken");
        var otherAccount = await AddAccountAsync("Other", withContact: true);
        int otherContact;
        await using (var db = _factory.CreateDbContext())
        {
            otherContact = await db.Contacts.Where(c => c.AccountId == otherAccount).Select(c => c.Id).SingleAsync(Ct);
        }

        var before = await CountsAsync();

        // A new account with a name that is already taken, no last name and a bad email.
        var request = NewAccountRequest(leadId, accountName: "Taken");
        request.ContactLastName = " ";
        request.ContactEmail = "nope";
        request.OpportunityName = "";
        var result = await _service.ConvertAsync(request, _alice, Ct);
        result.Status.ShouldBe(ConvertStatus.Invalid);
        result.FieldErrors!.Keys.ShouldBe(
            [nameof(LeadConvertRequest.NewAccountName), nameof(LeadConvertRequest.ContactLastName),
                nameof(LeadConvertRequest.ContactEmail), nameof(LeadConvertRequest.OpportunityName)],
            ignoreOrder: true);

        // A contact that belongs to a different account, and an account that does not exist.
        var wrongContact = await _service.ConvertAsync(
            new LeadConvertRequest { LeadId = leadId, ExistingAccountId = existing, ExistingContactId = otherContact }, _alice, Ct);
        wrongContact.Status.ShouldBe(ConvertStatus.Invalid);
        wrongContact.FieldErrors!.ShouldContainKey(nameof(LeadConvertRequest.ExistingContactId));

        var noAccount = await _service.ConvertAsync(
            new LeadConvertRequest { LeadId = leadId, ExistingAccountId = int.MaxValue, ContactLastName = "X" }, _alice, Ct);
        noAccount.Status.ShouldBe(ConvertStatus.Invalid);
        noAccount.FieldErrors!.ShouldContainKey(nameof(LeadConvertRequest.ExistingAccountId));

        (await CountsAsync()).ShouldBe(before);
        (await _service.GetAsync(leadId, _alice, Ct))!.IsConverted.ShouldBeFalse();
    }

    // ---- Rollback ----

    [Fact]
    public async Task A_failure_while_creating_the_opportunity_rolls_everything_back()
    {
        var leadId = await AddLeadAsync();
        await AddHistoryAsync(leadId);
        var failing = new FailingCommands("INSERT INTO [Opportunities]");
        var service = ServiceWith(failing);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ConvertAsync(NewAccountRequest(leadId), _alice, Ct));

        // Not a vacuous pass: the account and contact inserts had already run when the opportunity insert failed.
        failing.Executed.ShouldContain(c => c.Contains("INSERT INTO [Accounts]"));
        failing.Executed.ShouldContain(c => c.Contains("INSERT INTO [Contacts]"));
        await AssertNothingConvertedAsync(leadId);
    }

    [Fact]
    public async Task A_failure_while_updating_the_lead_rolls_everything_back()
    {
        var leadId = await AddLeadAsync();
        await AddHistoryAsync(leadId);
        var failing = new FailingCommands("UPDATE [Leads]");
        var service = ServiceWith(failing);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ConvertAsync(NewAccountRequest(leadId), _alice, Ct));

        failing.Executed.ShouldContain(c => c.Contains("INSERT INTO [Opportunities]"));
        await AssertNothingConvertedAsync(leadId);
    }

    private async Task AssertNothingConvertedAsync(int leadId)
    {
        (await CountsAsync()).ShouldBe(new Counts(0, 0, 0));

        await using var db = _factory.CreateDbContext();
        var lead = await db.Leads.Include(l => l.LeadStatus).SingleAsync(l => l.Id == leadId, Ct);
        lead.LeadStatus.SystemCode.ShouldBe(LeadStatus.New);
        lead.ConvertedAt.ShouldBeNull();
        lead.ConvertedAccountId.ShouldBeNull();
        lead.ConvertedContactId.ShouldBeNull();
        lead.ConvertedOpportunityId.ShouldBeNull();

        // The history is still on the lead.
        (await db.Activities.IgnoreQueryFilters().Where(a => a.Subject.StartsWith(_prefix)).ToListAsync(Ct))
            .ShouldAllBe(a => a.LeadId == leadId && a.ContactId == null);
        (await db.Notes.SingleAsync(n => n.Body.StartsWith(_prefix), Ct)).LeadId.ShouldBe(leadId);
    }

    // ---- Two people converting the same lead at once ----

    [Theory]
    [InlineData("different account names")]
    [InlineData("the same account name")]
    public async Task Converting_the_same_lead_twice_at_once_has_exactly_one_winner(string scenario)
    {
        var sameName = scenario.StartsWith("the same", StringComparison.Ordinal);

        for (var round = 0; round < 3; round++)
        {
            var leadId = await AddLeadAsync($"Racer {round}");
            await AddHistoryAsync(leadId);
            var before = await CountsAsync();

            // Both converts are held at their first account insert until both have arrived, so both have already
            // passed the "is this lead still unconverted?" check: the database has to pick the winner.
            var rendezvous = new Rendezvous("INSERT INTO [Accounts]", parties: 2);
            var service = ServiceWith(rendezvous);

            var first = service.ConvertAsync(NewAccountRequest(leadId, sameName ? $"Race {round}" : $"Race {round} A"), _alice, Ct);
            var second = service.ConvertAsync(NewAccountRequest(leadId, sameName ? $"Race {round}" : $"Race {round} B"), _bob, Ct);
            var results = await Task.WhenAll(first, second);

            results.Count(r => r.Status == ConvertStatus.Converted).ShouldBe(1);
            var loser = results.Single(r => r.Status != ConvertStatus.Converted);
            loser.Status.ShouldBeOneOf(ConvertStatus.AlreadyConverted, ConvertStatus.Conflict);

            // One account, one contact, one opportunity more than before: the loser left nothing behind.
            var after = await CountsAsync();
            (after.Accounts - before.Accounts).ShouldBe(1);
            (after.Contacts - before.Contacts).ShouldBe(1);
            (after.Opportunities - before.Opportunities).ShouldBe(1);

            var winner = results.Single(r => r.Status == ConvertStatus.Converted);
            var detail = (await _service.GetAsync(leadId, _alice, Ct))!;
            detail.ConvertedAccountId.ShouldBe(winner.AccountId);
            detail.ConvertedContactId.ShouldBe(winner.ContactId);
            detail.ConvertedOpportunityId.ShouldBe(winner.OpportunityId);
        }
    }

    // ---- The database refuses an inconsistent lead, whatever the code does ----

    [Fact]
    public async Task The_database_rejects_a_lead_converted_without_its_account_and_contact()
    {
        var leadId = await AddLeadAsync();

        await using var db = _factory.CreateDbContext();
        var ex = await Should.ThrowAsync<SqlException>(
            () => db.Database.ExecuteSqlAsync($"UPDATE Leads SET ConvertedAt = SYSUTCDATETIME() WHERE Id = {leadId}", Ct));

        ex.Message.ShouldContain("CK_Leads_ConvertedConsistent");
    }

    // ---- List, save and concurrency (collations, rowversion) ----

    [Fact]
    public async Task The_list_finds_leads_ignoring_case_and_accents_and_hides_closed_ones_by_default()
    {
        await EnsureReferenceDataAsync();
        var id = await AddLeadAsync("Αθηνά Παπαδοπούλου");
        await AddLeadAsync("Disqualified one", LeadStatus.Disqualified);

        var found = await _service.SearchAsync(
            new LeadQuery(Scope: ListScope.All, Search: _prefix + "αθηνα παπαδοπουλου"), _alice, Ct);
        var open = await _service.SearchAsync(new LeadQuery(Scope: ListScope.All, Search: _prefix), _alice, Ct);
        var everything = await _service.SearchAsync(new LeadQuery(Scope: ListScope.All, Search: _prefix, OpenOnly: false), _alice, Ct);

        found.Items.Select(l => l.Id).ShouldBe([id]);
        open.TotalCount.ShouldBe(1);
        everything.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Saving_with_an_old_row_version_is_a_conflict()
    {
        var leadId = await AddLeadAsync();
        var mine = (await _service.GetAsync(leadId, _alice, Ct))!.ToEditModel();
        var theirs = (await _service.GetAsync(leadId, _bob, Ct))!.ToEditModel();

        theirs.Name = _prefix + "Changed by Bob";
        (await _service.SaveAsync(theirs, _bob, cancellationToken: Ct)).Status.ShouldBe(SaveStatus.Saved);

        mine.Name = _prefix + "Changed by Alice";
        var result = await _service.SaveAsync(mine, _alice, cancellationToken: Ct);

        result.Status.ShouldBe(SaveStatus.Conflict);
        (await _service.GetAsync(leadId, _alice, Ct))!.Name.ShouldBe(_prefix + "Changed by Bob");
    }

    // ---- Test interceptors ----

    private LeadService ServiceWith(params IInterceptor[] interceptors)
    {
        var factory = _sql.CreateFactory(interceptors);
        return new LeadService(factory, new OwnerService(factory), TimeProvider.System);
    }

    /// <summary>Records every SQL command and throws when one contains the given text (as the store would on an error).</summary>
    private sealed class FailingCommands(string failOn) : DbCommandInterceptor
    {
        private readonly List<string> _executed = [];

        public IReadOnlyList<string> Executed
        {
            get
            {
                lock (_executed)
                {
                    return [.. _executed];
                }
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Check(DbCommand command)
        {
            if (command.CommandText.Contains(failOn, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated failure of: " + failOn);
            }

            lock (_executed)
            {
                _executed.Add(command.CommandText);
            }
        }
    }

    /// <summary>Holds the first <paramref name="parties"/> commands containing the text until all of them have arrived.</summary>
    private sealed class Rendezvous(string waitOn, int parties) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(waitOn, StringComparison.Ordinal) && Interlocked.Increment(ref _arrived) <= parties)
            {
                if (_arrived == parties)
                {
                    _allArrived.TrySetResult();
                }

                await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }

            return result;
        }
    }
}
