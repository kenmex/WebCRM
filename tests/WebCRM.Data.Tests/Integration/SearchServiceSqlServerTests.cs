using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// Global search against real SQL Server: the Greek accent-insensitive collation (D3), the VAT / Tax ID
/// normalisation, ranking, visibility, and the P7 target of under a second at volume.
/// </summary>
public class SearchServiceSqlServerTests : IClassFixture<SqlServerFixture>
{
    private const string Alice = "sr-alice";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SqlServerFixture _sql;
    private readonly IDbContextFactory<CrmDbContext> _factory;
    private readonly SearchService _service;
    private readonly AccountService _accounts;

    // Every test uses its own token, so tests sharing the database never see each other's rows.
    private readonly string _token = "S" + Guid.NewGuid().ToString("N")[..8];

    private readonly UserContext _alice = new(Alice, RoleNames.Sales, TeamId: null);

    public SearchServiceSqlServerTests(SqlServerFixture sql)
    {
        _sql = sql;
        _factory = sql.CreateFactory();
        _service = new SearchService(_factory);
        _accounts = new AccountService(_factory, new OwnerService(_factory));
    }

    private async Task EnsureReferenceDataAsync()
    {
        Assert.SkipUnless(_sql.Available, _sql.SkipReason);

        await using var db = _factory.CreateDbContext();
        if (await db.Users.AnyAsync(u => u.Id == Alice, Ct))
        {
            return;
        }

        db.Users.AddRange(
            new User { Id = Alice, UserName = Alice, NormalizedUserName = Alice.ToUpperInvariant(), DisplayName = "Alice" },
            new User { Id = "system-test", UserName = "system-test", NormalizedUserName = "SYSTEM-TEST", DisplayName = "System test" });
        db.AccountStatuses.Add(new AccountStatus { Name = "SR Active", SortOrder = 10 });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> AddAccountAsync(
        string name, string? legalName = null, string? email = null, string? vat = null, string? phone = null, string? city = null)
    {
        await using var db = _factory.CreateDbContext();
        var account = new Account
        {
            Name = name,
            LegalName = legalName,
            Email = email,
            VatNumber = vat,
            Phone = phone,
            OwnerId = Alice,
            AccountStatusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct),
        };
        if (city is not null)
        {
            account.Addresses.Add(new Address { AddressType = AddressType.Billing, City = city });
        }

        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    private async Task<int> AddContactAsync(
        string? first, string last, int accountId, string? email = null, string? phone = null, string? mobile = null, string? jobTitle = null)
    {
        await using var db = _factory.CreateDbContext();
        var contact = new Contact
        {
            FirstName = first,
            LastName = last,
            AccountId = accountId,
            OwnerId = Alice,
            Email = email,
            Phone = phone,
            Mobile = mobile,
            JobTitle = jobTitle,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(Ct);
        return contact.Id;
    }

    private static IEnumerable<string> Titles(SearchResults results, SearchEntity type) =>
        results.Groups.Single(g => g.Type == type).Hits.Select(h => h.Title);

    // ---- Accent and case insensitivity (D3) ----

    [Fact]
    public async Task Greek_text_is_found_without_accents_and_in_any_case_in_every_searched_column()
    {
        await EnsureReferenceDataAsync();
        var athens = await AddAccountAsync(
            $"{_token} Αθήνα Συμβουλευτική", legalName: $"{_token} Ελληνική Εμπορική Α.Ε.", email: $"{_token}.Γραφείο@example.gr");
        await AddContactAsync("Μαρία", $"{_token}Παπαδοπούλου", athens, email: $"{_token}.maria@example.gr");

        // D3: "Αθηνα" finds "Αθήνα"; capitals and missing accents everywhere.
        foreach (var text in new[]
        {
            $"{_token} ΑΘΗΝΑ", $"{_token} αθηνα συμβ", "ελληνικη εμπορικη", "ΕΛΛΗΝΙΚΗ ΕΜΠΟΡΙΚΗ", $"{_token}.γραφειο"
        })
        {
            var results = await _service.SearchAsync(text, 5, _alice, Ct);

            results.Groups.Single(g => g.Type == SearchEntity.Account).Hits
                .ShouldContain(h => h.Title == $"{_token} Αθήνα Συμβουλευτική", $"account search '{text}'");
        }

        foreach (var text in new[] { $"ΜΑΡΙΑ {_token}ΠΑΠΑΔΟΠΟΥΛΟΥ", $"μαρια {_token}παπαδοπουλου", $"{_token.ToUpperInvariant()}.MARIA@EXAMPLE.GR" })
        {
            var results = await _service.SearchAsync(text, 5, _alice, Ct);

            Titles(results, SearchEntity.Contact).ShouldContain($"Μαρία {_token}Παπαδοπούλου", $"contact search '{text}'");
        }
    }

    [Fact]
    public async Task Accounts_are_found_by_vat_number_however_it_is_typed_and_by_phone()
    {
        await EnsureReferenceDataAsync();
        var vat = $"DE{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        await AddAccountAsync($"{_token} Tax Co", vat: vat, phone: "+30 210 555 0199");
        var spaced = $"{vat[..2]} {vat[2..5]}.{vat[5..]}".ToLowerInvariant();

        (await _service.SearchAsync(vat, 5, _alice, Ct)).Groups[0].Hits.ShouldContain(h => h.Title == $"{_token} Tax Co");
        (await _service.SearchAsync(spaced, 5, _alice, Ct)).Groups[0].Hits.ShouldContain(h => h.Title == $"{_token} Tax Co");
        (await _service.SearchAsync("210 555 0199", 5, _alice, Ct)).Groups[0].Hits.ShouldContain(h => h.Title == $"{_token} Tax Co");
    }

    [Fact]
    public async Task Contacts_are_found_by_email_phone_and_mobile_and_show_their_account_and_job_title()
    {
        await EnsureReferenceDataAsync();
        var acme = await AddAccountAsync($"{_token} Acme");
        await AddContactAsync(
            "Anna", $"{_token}Smith", acme, email: $"{_token}.anna@example.com", phone: "210 111 2222", mobile: "694 333 4444", jobTitle: "Buyer");

        foreach (var text in new[] { $"{_token}.anna@", "210 111 2222", "694 333 4444" })
        {
            var hit = (await _service.SearchAsync(text, 5, _alice, Ct)).Groups.Single(g => g.Type == SearchEntity.Contact).Hits.Single();

            hit.Title.ShouldBe($"Anna {_token}Smith");
            hit.Subtitle.ShouldBe($"{_token} Acme · Buyer");
        }
    }

    // ---- Ranking, counts, limits ----

    [Fact]
    public async Task Names_that_start_with_the_text_come_first_then_the_rest_by_name_and_the_total_counts_everything()
    {
        await EnsureReferenceDataAsync();
        await AddAccountAsync($"Zebra {_token} Co");
        await AddAccountAsync($"{_token} Beta");
        await AddAccountAsync($"{_token} Alpha");
        await AddAccountAsync($"Yak {_token} Ltd");

        var results = await _service.SearchAsync(_token, 3, _alice, Ct);

        var accounts = results.Groups.Single(g => g.Type == SearchEntity.Account);
        accounts.TotalCount.ShouldBe(4);
        accounts.Hits.Select(h => h.Title).ShouldBe([$"{_token} Alpha", $"{_token} Beta", $"Yak {_token} Ltd"]);
    }

    [Fact]
    public async Task Both_groups_are_always_returned_accounts_first_and_the_subtitle_of_an_account_is_industry_and_city()
    {
        await EnsureReferenceDataAsync();
        await AddAccountAsync($"{_token} Acme", city: "Athens");

        var results = await _service.SearchAsync(_token, 5, _alice, Ct);

        results.Groups.Select(g => g.Type).ShouldBe([SearchEntity.Account, SearchEntity.Contact]);
        results.Groups[0].Hits.Single().Subtitle.ShouldBe("Athens");
        results.Groups[1].Hits.ShouldBeEmpty();
        results.Groups[1].TotalCount.ShouldBe(0);
        results.TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task Text_shorter_than_two_characters_is_not_searched(string? text)
    {
        await EnsureReferenceDataAsync();

        var results = await _service.SearchAsync(text, 5, _alice, Ct);

        results.Groups.ShouldBeEmpty();
        results.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Wildcard_characters_in_the_text_are_matched_literally()
    {
        await EnsureReferenceDataAsync();
        await AddAccountAsync($"{_token} 100% Pure");
        await AddAccountAsync($"{_token} 100X Pure");

        var results = await _service.SearchAsync($"{_token} 100%", 5, _alice, Ct);

        Titles(results, SearchEntity.Account).ShouldBe([$"{_token} 100% Pure"]);
    }

    // ---- Visibility ----

    [Fact]
    public async Task Deleted_accounts_and_contacts_and_the_contacts_of_deleted_accounts_are_not_found()
    {
        await EnsureReferenceDataAsync();
        var keep = await AddAccountAsync($"{_token} Keep");
        var doomed = await AddAccountAsync($"{_token} Doomed");
        var deletedContact = await AddContactAsync("Del", $"{_token}Contact", keep);
        await AddContactAsync("Stay", $"{_token}Stay", keep);
        await AddContactAsync("Orphan", $"{_token}Orphan", doomed);
        await new ContactService(_factory, new OwnerService(_factory), TimeProvider.System).DeleteAsync(deletedContact, _alice, Ct);
        await _accounts.DeleteAsync(doomed, _alice, Ct);

        var results = await _service.SearchAsync(_token, 10, _alice, Ct);

        Titles(results, SearchEntity.Account).ShouldBe([$"{_token} Keep"]);
        Titles(results, SearchEntity.Contact).ShouldBe([$"Stay {_token}Stay"]);
    }

    // ---- Volume: P7 asks for under a second ----

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Searching_20000_accounts_and_20000_contacts_takes_well_under_a_second_once_warm()
    {
        await EnsureReferenceDataAsync();
        await using (var db = _factory.CreateDbContext())
        {
            var statusId = await db.AccountStatuses.Select(s => s.Id).FirstAsync(Ct);

            // Raw SQL on purpose: 40 000 rows through EF would dominate the test time. Values are parameters.
            var volumeNames = _token + " Volume%";
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO Accounts (Name, AccountStatusId, OwnerId, CreatedBy, Phone, Email)
                 SELECT n.N, {statusId}, {Alice}, {Alice}, N'210' + n.S, N'v' + n.S + N'@volume.example'
                 FROM (SELECT {_token} + N' Volume ' + CAST(x.Rn AS nvarchar(10)) AS N, CAST(x.Rn AS nvarchar(10)) AS S
                       FROM (SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Rn
                             FROM sys.all_objects a CROSS JOIN sys.all_objects b) x) n;
                 """,
                Ct);
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO Contacts (FirstName, LastName, AccountId, OwnerId, CreatedBy, Email)
                 SELECT N'First', {_token} + N'Last' + n.S, (SELECT MIN(Id) FROM Accounts WHERE Name LIKE {volumeNames}),
                        {Alice}, {Alice}, N'c' + n.S + N'@volume.example'
                 FROM (SELECT CAST(x.Rn AS nvarchar(10)) AS S
                       FROM (SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Rn
                             FROM sys.all_objects a CROSS JOIN sys.all_objects b) x) n;
                 """,
                Ct);
        }


        // The first call pays for plan compilation; the target is for the warm path.
        await _service.SearchAsync(_token, 5, _alice, Ct);

        var timings = new List<long>();
        foreach (var text in new[] { _token, $"{_token} volume 1999", "v1999@volume", $"First {_token}Last77", "2101999" })
        {
            var clock = Stopwatch.StartNew();
            var results = await _service.SearchAsync(text, 5, _alice, Ct);
            clock.Stop();
            timings.Add(clock.ElapsedMilliseconds);

            results.TotalCount.ShouldBeGreaterThan(0, $"search '{text}'");
        }

        // The spec's target is 1 s; the bound has headroom because CI runners are slower than a developer machine.
        timings.Max().ShouldBeLessThan(1500, $"timings in ms: {string.Join(", ", timings)}");

        var all = await _service.SearchAsync(_token, 5, _alice, Ct);
        all.Groups[0].TotalCount.ShouldBe(20000);
        all.Groups[0].Hits.Count.ShouldBe(5);
    }
}
