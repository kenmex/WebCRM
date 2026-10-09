using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Entities;
using Activity = WebCRM.Core.Entities.Activity;
using WebCRM.Data;

namespace WebCRM.DemoData;

public sealed record DemoSeedReport(
    DemoWipeReport Removed,
    int Users,
    int Teams,
    int Accounts,
    int Addresses,
    int Contacts,
    int Opportunities,
    int Leads,
    int Activities,
    TimeSpan Generating,
    TimeSpan Writing);

/// <summary>Wipes the previous demo data and writes a new set, all in one transaction: either everything changes or nothing does.</summary>
public sealed class DemoSeeder(string connectionString)
{
    private static readonly string[] LeadSourceNames = ["Website", "Referral", "Trade show", "Partner", "Cold call"];
    private static readonly string[] LostReasonNames = ["Price", "Competitor", "No budget", "No decision", "Timing"];

    public static string? CheckPassword(string? password)
    {
        // The same rules as the app: 12 characters, and Identity's defaults (digit, lower, upper, symbol).
        if (string.IsNullOrEmpty(password) || password.Length < 12 || !password.Any(char.IsDigit)
            || !password.Any(char.IsLower) || !password.Any(char.IsUpper) || password.All(char.IsLetterOrDigit))
        {
            return "Demo:Password must be at least 12 characters with an upper-case letter, a lower-case letter, a digit and a symbol.";
        }

        return null;
    }

    public async Task<DemoWipeReport> WipeAsync(Action<string> log, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var report = await DemoWiper.WipeAsync(connection, transaction, SystemUser.Id, log, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return report;
    }

    public async Task<DemoSeedReport> SeedAsync(
        DemoOptions options, string password, Action<string> log, CancellationToken cancellationToken = default)
    {
        if (CheckPassword(password) is { } problem)
        {
            throw new DemoException(problem);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var contextOptions = new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(connection).Options;
        await using var db = new CrmDbContext(contextOptions);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            throw new DemoException(
                "The database is not up to date. Apply migrations first: dotnet ef database update --project src/WebCRM.Data --startup-project src/WebCRM.Web");
        }

        if (!await db.Users.AnyAsync(u => u.Id == SystemUser.Id, cancellationToken)
            || await db.Roles.CountAsync(r => r.Name != null && RoleNames.All.Contains(r.Name), cancellationToken) < RoleNames.All.Length)
        {
            throw new DemoException("The system user and the roles are missing. Start the web app once so its seeder creates them, then run again.");
        }

        var removed = await DemoWiper.WipeAsync(connection, transaction, SystemUser.Id, log, cancellationToken);

        await EnsureLookupsAsync(db, log, cancellationToken);
        var batch = new ImportBatch
        {
            Entity = DemoWiper.BatchEntity,
            FileName = DemoWiper.BatchFileName,
            Status = ImportStatus.Completed,
            TotalRows = options.Accounts + options.Contacts,
            OkRows = options.Accounts + options.Contacts,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SystemUser.Id,
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        var reference = await LoadReferenceDataAsync(db, batch.Id, cancellationToken);
        var bases = await LoadIdBasesAsync(connection, transaction, cancellationToken);

        var clock = Stopwatch.StartNew();
        log($"Generating (seed {options.Seed}, today {options.Today:yyyy-MM-dd})...");
        var set = new DemoDataGenerator(options, reference, bases).Generate();
        var generating = clock.Elapsed;
        log($"Generated in {generating.TotalSeconds:N1} s. Writing...");

        clock.Restart();
        var model = db.Model;
        // Teams point at their manager and users point at their team: insert the teams first, without managers.
        var managers = set.Teams.ToDictionary(t => t.Id, t => t.ManagerId);
        foreach (var team in set.Teams)
        {
            team.ManagerId = null;
        }

        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Team))!, set.Teams, cancellationToken);
        await WriteUsersAsync(db, set, password, cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE Teams SET ManagerId = @manager WHERE Id = @id";
            command.Parameters.Add(new SqlParameter("@manager", System.Data.SqlDbType.NVarChar, 450));
            command.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.Int));
            foreach (var (teamId, managerId) in managers)
            {
                command.Parameters["@manager"].Value = managerId ?? (object)DBNull.Value;
                command.Parameters["@id"].Value = teamId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Account))!, set.Accounts, cancellationToken);
        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Address))!, set.Addresses, cancellationToken);
        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Contact))!, set.Contacts, cancellationToken);
        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Opportunity))!, set.Opportunities, cancellationToken);
        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Lead))!, set.Leads, cancellationToken);
        await BulkWriter.WriteAsync(connection, transaction, model.FindEntityType(typeof(Activity))!, set.Activities, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var writing = clock.Elapsed;

        // After a big load the optimiser needs fresh numbers, or the first queries run on a wrong picture of the data.
        foreach (var table in new[] { "Accounts", "Addresses", "Contacts", "Opportunities", "Leads", "Activities", "Teams" })
        {
            await using var stats = connection.CreateCommand();
            stats.CommandText = $"UPDATE STATISTICS dbo.{table} WITH FULLSCAN";
            stats.CommandTimeout = 0;
            await stats.ExecuteNonQueryAsync(cancellationToken);
        }

        return new DemoSeedReport(
            removed, set.Users.Count, set.Teams.Count, set.Accounts.Count, set.Addresses.Count, set.Contacts.Count,
            set.Opportunities.Count, set.Leads.Count, set.Activities.Count, generating, writing);
    }

    /// <summary>Adds lead sources and lost reasons that are missing (by name); existing values are left alone.</summary>
    private static async Task EnsureLookupsAsync(CrmDbContext db, Action<string> log, CancellationToken cancellationToken)
    {
        var sources = await db.LeadSources.Select(s => s.Name).ToListAsync(cancellationToken);
        var reasons = await db.LostReasons.Select(r => r.Name).ToListAsync(cancellationToken);
        var added = 0;

        for (var i = 0; i < LeadSourceNames.Length; i++)
        {
            if (!sources.Contains(LeadSourceNames[i], StringComparer.OrdinalIgnoreCase))
            {
                db.LeadSources.Add(new LeadSource { Name = LeadSourceNames[i], SortOrder = (i + 1) * 10 });
                added++;
            }
        }

        for (var i = 0; i < LostReasonNames.Length; i++)
        {
            if (!reasons.Contains(LostReasonNames[i], StringComparer.OrdinalIgnoreCase))
            {
                db.LostReasons.Add(new LostReason { Name = LostReasonNames[i], SortOrder = (i + 1) * 10 });
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            log($"Added {added} missing lead source / lost reason values.");
        }
    }

    private static async Task<DemoReferenceData> LoadReferenceDataAsync(CrmDbContext db, int batchId, CancellationToken cancellationToken)
    {
        var statuses = await db.AccountStatuses.Where(s => s.IsActive).OrderBy(s => s.SortOrder).Select(s => s.Id).ToListAsync(cancellationToken);
        var industries = await db.Industries.Where(i => i.IsActive).OrderBy(i => i.SortOrder).Select(i => i.Id).ToListAsync(cancellationToken);
        var salutations = await db.Salutations.ToListAsync(cancellationToken);
        if (statuses.Count == 0 || industries.Count == 0)
        {
            throw new DemoException("Account statuses or industries are missing. Start the web app once so its seeder adds them, then run again.");
        }

        int Salutation(string name) => salutations.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new DemoException($"The salutation '{name}' is missing. Start the web app once so its seeder adds Mr, Ms and Dr, then run again.");

        var leadStatuses = await db.LeadStatuses.Where(s => s.IsActive).ToListAsync(cancellationToken);
        int LeadStatus(string code) => leadStatuses.FirstOrDefault(s => s.SystemCode == code)?.Id
            ?? throw new DemoException($"Lead status {code} is missing.");

        var types = await db.ActivityTypes.ToListAsync(cancellationToken);
        int Type(string code) => types.FirstOrDefault(t => t.SystemCode == code)?.Id ?? throw new DemoException($"Activity type {code} is missing.");

        var stages = await db.Stages.Where(s => s.IsActive).OrderBy(s => s.SortOrder)
            .Select(s => new StageInfo(s.Id, s.DefaultProbability, s.IsWon, s.IsLost)).ToListAsync(cancellationToken);

        return new DemoReferenceData(
            statuses,
            industries,
            Salutation("Mr"),
            Salutation("Ms"),
            Salutation("Dr"),
            await db.LeadSources.Where(s => s.IsActive).OrderBy(s => s.SortOrder).Select(s => s.Id).ToListAsync(cancellationToken),
            leadStatuses.Select(s => s.Id).ToList(),
            LeadStatus(Core.Entities.LeadStatus.New),
            LeadStatus(Core.Entities.LeadStatus.Converted),
            LeadStatus(Core.Entities.LeadStatus.Disqualified),
            await db.LostReasons.Where(r => r.IsActive).OrderBy(r => r.SortOrder).Select(r => r.Id).ToListAsync(cancellationToken),
            Type(ActivityType.Task),
            Type(ActivityType.Call),
            Type(ActivityType.Meeting),
            stages,
            SystemUser.Id,
            batchId);
    }

    private static async Task<IdBases> LoadIdBasesAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        async Task<int> Max(string table)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT ISNULL(MAX(Id), 0) FROM {table}";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        return new IdBases(
            await Max("Teams"), await Max("Accounts"), await Max("Addresses"), await Max("Contacts"),
            await Max("Leads"), await Max("Opportunities"), await Max("Activities"));
    }

    private static async Task WriteUsersAsync(CrmDbContext db, DemoDataSet set, string password, CancellationToken cancellationToken)
    {
        var hasher = new PasswordHasher<User>();
        var roleIds = await db.Roles.Where(r => r.Name != null).ToDictionaryAsync(r => r.Name!, r => r.Id, cancellationToken);

        foreach (var user in set.Users)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
        }

        db.Users.AddRange(set.Users);
        db.UserRoles.AddRange(set.Roles.Select(r => new IdentityUserRole<string> { UserId = r.Key, RoleId = roleIds[r.Value] }));
        await db.SaveChangesAsync(cancellationToken);
    }
}
