using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Data;
using WebCRM.Data.Services;

namespace WebCRM.DemoData;

public sealed record Measurement(string Group, string Scenario, int Rows, double MedianMs, double P95Ms, double MaxMs, double BudgetMs)
{
    public bool WithinBudget => P95Ms <= BudgetMs;
}

/// <summary>
/// Times the list queries and global search against the demo data, as the real app calls them (the same services, the same
/// queries). This is the server's data-loading time; Blazor rendering and the network come on top of it.
/// </summary>
/// <param name="Only">Run only scenarios whose group or name contains this text.</param>
/// <param name="ShowSql">Run each scenario once and print the SQL that EF sends.</param>
/// <param name="Runs">Timed runs per scenario.</param>
public sealed record MeasureOptions(string? Only = null, bool ShowSql = false, int? Runs = null);

public sealed class DemoMeasure(string connectionString)
{
    public const double ListBudgetMs = 300;
    public const double SearchBudgetMs = 1000;
    public const int Warmups = 3;
    public const int Runs = 20;

    private sealed class Factory(DbContextOptions<CrmDbContext> options) : IDbContextFactory<CrmDbContext>
    {
        public CrmDbContext CreateDbContext() => new(options);
    }

    public async Task<IReadOnlyList<Measurement>> RunAsync(
        Action<string> log, MeasureOptions? measureOptions = null, CancellationToken cancellationToken = default)
    {
        measureOptions ??= new MeasureOptions();
        var warmups = measureOptions.ShowSql ? 0 : Warmups;
        var runs = measureOptions.ShowSql ? 1 : measureOptions.Runs ?? Runs;
        var builder = new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(connectionString);
        if (measureOptions.ShowSql)
        {
            builder.LogTo(log, [DbLoggerCategory.Database.Command.Name], Microsoft.Extensions.Logging.LogLevel.Information);
        }

        var options = builder.Options;
        var factory = new Factory(options);
        var owners = new OwnerService(factory);
        var accounts = new AccountService(factory, owners);
        var contacts = new ContactService(factory, owners, TimeProvider.System);
        var search = new SearchService(factory);

        var facts = await LoadFactsAsync(options, cancellationToken);
        var results = new List<Measurement>();

        async Task Time(string group, string scenario, double budget, Func<Task<int>> run)
        {
            if (measureOptions.Only is { Length: > 0 } only
                && !(group + " " + scenario).Contains(only, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (measureOptions.ShowSql)
            {
                log($"--- {group}: {scenario}");
            }

            for (var i = 0; i < warmups; i++)
            {
                await run();
            }

            var rows = 0;
            var samples = new List<double>();
            for (var i = 0; i < runs; i++)
            {
                var clock = Stopwatch.StartNew();
                rows = await run();
                clock.Stop();
                samples.Add(clock.Elapsed.TotalMilliseconds);
            }

            samples.Sort();
            var measurement = new Measurement(group, scenario, rows, samples[samples.Count / 2], samples[(int)Math.Ceiling(samples.Count * 0.95) - 1], samples[^1], budget);
            results.Add(measurement);
            log($"  {(measurement.WithinBudget ? "ok  " : "SLOW")} {group,-9} {scenario,-52} p95 {measurement.P95Ms,7:N0} ms  ({rows:N0} rows)");
        }

        // ---- Accounts ----
        log("Accounts list:");
        var allAccounts = new AccountQuery(ListScope.All);
        await Time("Accounts", "Default view, Admin (All)", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Default view, Manager (My team)", ListBudgetMs, async () => (await accounts.SearchAsync(new AccountQuery(ListScope.Team), facts.Manager, cancellationToken)).TotalCount);
        await Time("Accounts", "Default view, Sales (Mine)", ListBudgetMs, async () => (await accounts.SearchAsync(new AccountQuery(ListScope.Mine), facts.Sales, cancellationToken)).TotalCount);
        await Time("Accounts", "Page 200 of 400 (All)", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { Page = 200 }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Filter: status", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { StatusId = facts.StatusId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Filter: industry", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { IndustryId = facts.IndustryId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Filter: owner", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { OwnerId = facts.Sales.UserId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Filter: city 'αθηνα' (no accent)", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { City = "αθηνα" }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Filter: status + industry + owner", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { StatusId = facts.StatusId, IndustryId = facts.IndustryId, OwnerId = facts.Sales.UserId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Quick filter: Greek word, no accents", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { Search = facts.GreekWord }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Accounts", "Quick filter: 'Α.Ε.' (thousands)", ListBudgetMs, async () => (await accounts.SearchAsync(allAccounts with { Search = "Α.Ε." }, facts.Admin, cancellationToken)).TotalCount);
        foreach (var sort in Enum.GetValues<AccountSort>())
        {
            foreach (var descending in new[] { false, true })
            {
                await Time("Accounts", $"Sort {sort} {(descending ? "desc" : "asc")}", ListBudgetMs,
                    async () => (await accounts.SearchAsync(allAccounts with { Sort = sort, Descending = descending }, facts.Admin, cancellationToken)).TotalCount);
            }
        }

        await Time("Accounts", "Sort LastActivity desc, page 200", ListBudgetMs,
            async () => (await accounts.SearchAsync(allAccounts with { Sort = AccountSort.LastActivity, Descending = true, Page = 200 }, facts.Admin, cancellationToken)).TotalCount);

        // ---- Contacts ----
        log("Contacts list:");
        var allContacts = new ContactQuery(ListScope.All);
        await Time("Contacts", "Default view, Admin (All)", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Default view, Manager (My team)", ListBudgetMs, async () => (await contacts.SearchAsync(new ContactQuery(ListScope.Team), facts.Manager, cancellationToken)).TotalCount);
        await Time("Contacts", "Default view, Sales (Mine)", ListBudgetMs, async () => (await contacts.SearchAsync(new ContactQuery(ListScope.Mine), facts.Sales, cancellationToken)).TotalCount);
        await Time("Contacts", "Page 1000 of 2000 (All)", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { Page = 1000 }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: account (the one with most contacts)", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { AccountId = facts.BusiestAccountId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: owner", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { OwnerId = facts.Sales.UserId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: has email", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { HasEmail = true }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: no email", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { HasEmail = false }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: Do not contact", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { DoNotContact = true }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Filter: owner + has email + account", ListBudgetMs,
            async () => (await contacts.SearchAsync(allContacts with { OwnerId = facts.Sales.UserId, HasEmail = true, AccountId = facts.BusiestAccountId }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Quick filter: Greek surname, no accents", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { Search = facts.GreekSurname }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Quick filter: email fragment", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { Search = facts.EmailFragment }, facts.Admin, cancellationToken)).TotalCount);
        await Time("Contacts", "Quick filter: 'an' (matches many)", ListBudgetMs, async () => (await contacts.SearchAsync(allContacts with { Search = "an" }, facts.Admin, cancellationToken)).TotalCount);
        foreach (var sort in Enum.GetValues<ContactSort>())
        {
            foreach (var descending in new[] { false, true })
            {
                await Time("Contacts", $"Sort {sort} {(descending ? "desc" : "asc")}", ListBudgetMs,
                    async () => (await contacts.SearchAsync(allContacts with { Sort = sort, Descending = descending }, facts.Admin, cancellationToken)).TotalCount);
            }
        }

        await Time("Contacts", "Sort LastActivity desc, page 1000", ListBudgetMs,
            async () => (await contacts.SearchAsync(allContacts with { Sort = ContactSort.LastActivity, Descending = true, Page = 1000 }, facts.Admin, cancellationToken)).TotalCount);

        // ---- Global search ----
        log("Global search (top bar = 5 per type, results page = 20 per type):");
        var terms = new (string Label, string Text)[]
        {
            ("Greek company word, no accents", facts.GreekWord),
            ("English company word", facts.EnglishWord),
            ("Greek surname, no accents", facts.GreekSurname),
            ("Email fragment", facts.EmailFragment),
            ("VAT / Tax ID, typed with spaces", facts.VatSpaced),
            ("Phone fragment", facts.PhoneFragment),
            ("Two letters 'an' (matches thousands)", "an"),
            ("'Α.Ε.' (matches thousands)", "Α.Ε."),
            ("No match at all", "zzqxzzqx"),
        };
        foreach (var (label, text) in terms)
        {
            await Time("Search", $"Top bar (5/type): {label}", SearchBudgetMs, async () => (await search.SearchAsync(text, 5, facts.Admin, cancellationToken)).TotalCount);
            await Time("Search", $"Results page (20/type): {label}", SearchBudgetMs, async () => (await search.SearchAsync(text, 20, facts.Admin, cancellationToken)).TotalCount);
        }

        return results;
    }

    private sealed record Facts(
        UserContext Admin,
        UserContext Manager,
        UserContext Sales,
        int StatusId,
        int IndustryId,
        int BusiestAccountId,
        string GreekWord,
        string EnglishWord,
        string GreekSurname,
        string EmailFragment,
        string VatSpaced,
        string PhoneFragment);

    /// <summary>Picks real values from the demo data, so every scenario searches for something that exists.</summary>
    private async Task<Facts> LoadFactsAsync(DbContextOptions<CrmDbContext> options, CancellationToken cancellationToken)
    {
        await using var db = new CrmDbContext(options);

        var users = await db.Users.AsNoTracking().Where(u => u.Id.StartsWith("demo-")).ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            throw new DemoException("There is no demo data yet. Run: dotnet run --project src/WebCRM.DemoData -- seed");
        }

        UserContext Find(string id, string role) => users.Where(u => u.Id == id).Select(u => new UserContext(u.Id, role, u.TeamId)).First();

        var demoAccounts = db.Accounts.AsNoTracking().Where(a => a.ImportBatch != null && a.ImportBatch.Entity == "Demo");
        var statusId = await demoAccounts.GroupBy(a => a.AccountStatusId).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstAsync(cancellationToken);
        var industryId = await demoAccounts.Where(a => a.IndustryId != null).GroupBy(a => a.IndustryId!.Value).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstAsync(cancellationToken);
        var busiest = await db.Contacts.AsNoTracking().GroupBy(c => c.AccountId).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstAsync(cancellationToken);

        var greekName = await demoAccounts.Where(a => a.TaxOffice != null).Select(a => a.Name).FirstAsync(cancellationToken);
        var englishName = await demoAccounts.Where(a => a.TaxOffice == null && a.Name.CompareTo("a") >= 0 && a.Name.CompareTo("ω") < 0)
            .Select(a => a.Name).FirstAsync(cancellationToken);
        var surname = await db.Contacts.AsNoTracking().Where(c => c.Email != null).Select(c => c.LastName).Skip(100).FirstAsync(cancellationToken);
        var email = await db.Contacts.AsNoTracking().Where(c => c.Email != null).Select(c => c.Email!).Skip(500).FirstAsync(cancellationToken);
        var vat = await demoAccounts.Where(a => a.VatNumber != null && a.VatNumber.StartsWith("DE")).Select(a => a.VatNumber!).FirstAsync(cancellationToken);
        var phone = await demoAccounts.Where(a => a.Phone != null && a.Phone.StartsWith("+30 210")).Select(a => a.Phone!).FirstAsync(cancellationToken);

        return new Facts(
            Find("demo-admin", RoleNames.Admin),
            Find("demo-manager-athens", RoleNames.Manager),
            Find("demo-sales-athens-1", RoleNames.Sales),
            statusId,
            industryId,
            busiest,
            Pools.Fold(FirstWord(greekName)),
            FirstWord(englishName),
            Pools.Fold(surname),
            email[..email.IndexOf('@')],
            $"{vat[..2]} {vat[2..5]}.{vat[5..]}".ToLowerInvariant(),
            phone[^8..]);
    }

    private static string FirstWord(string text) =>
        text.Split([' ', ',', '-', '&'], StringSplitOptions.RemoveEmptyEntries).OrderByDescending(w => w.Length).First();

    public static string ToMarkdown(IReadOnlyList<Measurement> measurements)
    {
        var lines = new List<string>
        {
            "| Group | Scenario | Rows | Median ms | p95 ms | Max ms | Budget ms | |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |",
        };
        lines.AddRange(measurements.Select(m =>
            $"| {m.Group} | {m.Scenario} | {m.Rows:N0} | {m.MedianMs:N0} | {m.P95Ms:N0} | {m.MaxMs:N0} | {m.BudgetMs:N0} | {(m.WithinBudget ? "ok" : "SLOW")} |"));
        return string.Join(Environment.NewLine, lines);
    }
}
