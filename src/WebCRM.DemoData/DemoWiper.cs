using Microsoft.Data.SqlClient;

namespace WebCRM.DemoData;

/// <summary>Something stops the tool; the message says what to do. The run is rolled back.</summary>
public sealed class DemoException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record DemoWipeReport(
    int Activities, int Leads, int Opportunities, int Contacts, int Addresses, int Accounts, int Users, int Teams)
{
    public static DemoWipeReport None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    public int Total => Activities + Leads + Opportunities + Contacts + Addresses + Accounts + Users + Teams;
}

/// <summary>
/// Deletes demo data and nothing else. What counts as demo data (no migration needed):
/// accounts and contacts carry the "Demo" ImportBatch; opportunities, leads and activities are demo data when the
/// system user created them and they belong to demo records or demo users; users are the ones whose id starts with
/// "demo-"; teams are the ones named "Demo: ...". Anything of yours that is tied to demo data (a contact you added to
/// a demo account, a record you reassigned to a demo user) stops the wipe instead of being deleted.
/// </summary>
public static class DemoWiper
{
    public const string BatchEntity = "Demo";
    public const string BatchFileName = "demo-data";

    public static async Task<DemoWipeReport> WipeAsync(
        SqlConnection connection, SqlTransaction transaction, string systemUserId, Action<string> log, CancellationToken cancellationToken)
    {
        var batchId = await ScalarAsync<int?>(
            connection, transaction, "SELECT TOP 1 Id FROM ImportBatches WHERE Entity = @entity AND FileName = @file",
            cancellationToken, ("@entity", BatchEntity), ("@file", BatchFileName));
        var demoUsers = await ScalarAsync<int>(
            connection, transaction, "SELECT COUNT(*) FROM AspNetUsers WHERE Id LIKE N'demo-%'", cancellationToken);
        var demoTeams = await ScalarAsync<int>(
            connection, transaction, "SELECT COUNT(*) FROM Teams WHERE Name LIKE N'Demo: %'", cancellationToken);

        if (batchId is null && demoUsers == 0 && demoTeams == 0)
        {
            log("No demo data to remove.");
            return DemoWipeReport.None;
        }

        try
        {
            // No parameters here on purpose: with parameters SqlClient runs the batch through sp_executesql, and temp tables
            // made in there are gone when it returns. The system user id is a fixed GUID, embedded as an escaped literal.
            await Exec(connection, transaction, cancellationToken, MarkDemoRows(batchId ?? 0, systemUserId));

            var blockers = await FindBlockersAsync(connection, transaction, batchId ?? 0, systemUserId, cancellationToken);
            if (blockers.Count > 0)
            {
                throw new DemoException(
                    "The demo data cannot be removed without touching records that are not demo data, so nothing was changed:"
                    + Environment.NewLine + string.Join(Environment.NewLine, blockers.Select(b => "  - " + b))
                    + Environment.NewLine + "Delete or reassign those records, then run again.");
            }

            var activities = await Exec(connection, transaction, cancellationToken, "DELETE a FROM Activities a JOIN #DemoActivities d ON d.Id = a.Id;");
            var leads = await Exec(connection, transaction, cancellationToken, "DELETE l FROM Leads l JOIN #DemoLeads d ON d.Id = l.Id;");
            var opportunities = await Exec(connection, transaction, cancellationToken, "DELETE o FROM Opportunities o JOIN #DemoOpps d ON d.Id = o.Id;");
            var contacts = await Exec(connection, transaction, cancellationToken, "DELETE c FROM Contacts c JOIN #DemoContacts d ON d.Id = c.Id;");
            var addresses = await Exec(connection, transaction, cancellationToken, "DELETE a FROM Addresses a JOIN #DemoAccounts d ON d.Id = a.AccountId;");
            var accounts = await Exec(connection, transaction, cancellationToken, "DELETE a FROM Accounts a JOIN #DemoAccounts d ON d.Id = a.Id;");

            // Things that belong to the demo users only.
            foreach (var table in new[] { "Favourites", "RecentViews", "Notifications", "SavedViews", "ApiTokens", "AuditLogs" })
            {
                await Exec(connection, transaction, cancellationToken, $"DELETE FROM {table} WHERE UserId LIKE N'demo-%';");
            }

            foreach (var table in new[] { "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetUserPasskeys" })
            {
                await Exec(
                    connection, transaction, cancellationToken,
                    $"IF OBJECT_ID(N'dbo.{table}') IS NOT NULL DELETE FROM {table} WHERE UserId LIKE N'demo-%';");
            }

            // Users point at teams and teams point at their manager: let go of the manager first.
            await Exec(connection, transaction, cancellationToken, "UPDATE Teams SET ManagerId = NULL WHERE Name LIKE N'Demo: %';");
            var users = await Exec(connection, transaction, cancellationToken, "DELETE FROM AspNetUsers WHERE Id LIKE N'demo-%';");
            var teams = await Exec(connection, transaction, cancellationToken, "DELETE FROM Teams WHERE Name LIKE N'Demo: %';");
            await Exec(connection, transaction, cancellationToken, "DELETE FROM ImportBatches WHERE Entity = @entity AND FileName = @file;",
                ("@entity", BatchEntity), ("@file", BatchFileName));

            var report = new DemoWipeReport(activities, leads, opportunities, contacts, addresses, accounts, users, teams);
            log($"Removed demo data: {accounts:N0} accounts, {contacts:N0} contacts, {opportunities:N0} opportunities, "
                + $"{leads:N0} leads, {activities:N0} activities, {users} users, {teams} teams.");
            return report;
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            throw new DemoException(
                "The demo data cannot be removed because something of yours still refers to a demo user or record "
                + "(for example a record that a demo user last changed). Nothing was changed. Details: " + ex.Message, ex);
        }
    }

    /// <summary>Temp tables with the ids of every demo row, so the checks and the deletes use the same definition.</summary>
    private static string MarkDemoRows(int batchId, string systemUserId) => $"""
        SELECT Id INTO #DemoAccounts FROM Accounts WHERE ImportBatchId = {batchId};
        SELECT Id INTO #DemoContacts FROM Contacts WHERE ImportBatchId = {batchId};
        SELECT o.Id INTO #DemoOpps FROM Opportunities o
            WHERE o.CreatedBy = {Literal(systemUserId)} AND o.AccountId IN (SELECT Id FROM #DemoAccounts);
        SELECT Id INTO #DemoLeads FROM Leads WHERE CreatedBy = {Literal(systemUserId)} AND OwnerId LIKE N'demo-%';
        SELECT a.Id INTO #DemoActivities FROM Activities a
            WHERE a.CreatedBy = {Literal(systemUserId)}
              AND (a.AccountId IN (SELECT Id FROM #DemoAccounts) OR a.ContactId IN (SELECT Id FROM #DemoContacts)
                   OR a.OpportunityId IN (SELECT Id FROM #DemoOpps) OR a.LeadId IN (SELECT Id FROM #DemoLeads));
        """;

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";

    private static async Task<List<string>> FindBlockersAsync(
        SqlConnection connection, SqlTransaction transaction, int batchId, string systemUserId, CancellationToken cancellationToken)
    {
        var checks = new (string Description, string Sql)[]
        {
            ("accounts owned by a demo user that are not demo accounts",
                $"SELECT COUNT(*) FROM Accounts WHERE OwnerId LIKE N'demo-%' AND Id NOT IN (SELECT Id FROM #DemoAccounts)"),
            ("contacts owned by a demo user that are not demo contacts",
                "SELECT COUNT(*) FROM Contacts WHERE OwnerId LIKE N'demo-%' AND Id NOT IN (SELECT Id FROM #DemoContacts)"),
            ("opportunities owned by a demo user that are not demo opportunities",
                "SELECT COUNT(*) FROM Opportunities WHERE OwnerId LIKE N'demo-%' AND Id NOT IN (SELECT Id FROM #DemoOpps)"),
            ("leads owned by a demo user that are not demo leads",
                "SELECT COUNT(*) FROM Leads WHERE OwnerId LIKE N'demo-%' AND Id NOT IN (SELECT Id FROM #DemoLeads)"),
            ("activities owned by a demo user that are not demo activities",
                "SELECT COUNT(*) FROM Activities WHERE OwnerId LIKE N'demo-%' AND Id NOT IN (SELECT Id FROM #DemoActivities)"),
            ("contacts that belong to a demo account but are not demo contacts",
                "SELECT COUNT(*) FROM Contacts WHERE AccountId IN (SELECT Id FROM #DemoAccounts) AND Id NOT IN (SELECT Id FROM #DemoContacts)"),
            ("opportunities on a demo account that are not demo opportunities",
                "SELECT COUNT(*) FROM Opportunities WHERE AccountId IN (SELECT Id FROM #DemoAccounts) AND Id NOT IN (SELECT Id FROM #DemoOpps)"),
            ("activities on demo records that were not created by the tool",
                """
                SELECT COUNT(*) FROM Activities
                WHERE Id NOT IN (SELECT Id FROM #DemoActivities)
                  AND (AccountId IN (SELECT Id FROM #DemoAccounts) OR ContactId IN (SELECT Id FROM #DemoContacts)
                       OR OpportunityId IN (SELECT Id FROM #DemoOpps) OR LeadId IN (SELECT Id FROM #DemoLeads))
                """),
            ("notes on demo records",
                """
                SELECT COUNT(*) FROM Notes
                WHERE AccountId IN (SELECT Id FROM #DemoAccounts) OR ContactId IN (SELECT Id FROM #DemoContacts)
                   OR OpportunityId IN (SELECT Id FROM #DemoOpps) OR LeadId IN (SELECT Id FROM #DemoLeads)
                """),
            ("attachments on demo records",
                """
                SELECT COUNT(*) FROM Attachments
                WHERE AccountId IN (SELECT Id FROM #DemoAccounts) OR ContactId IN (SELECT Id FROM #DemoContacts)
                   OR OpportunityId IN (SELECT Id FROM #DemoOpps)
                """),
            ("leads of yours converted into demo records",
                """
                SELECT COUNT(*) FROM Leads
                WHERE Id NOT IN (SELECT Id FROM #DemoLeads)
                  AND (ConvertedAccountId IN (SELECT Id FROM #DemoAccounts) OR ConvertedContactId IN (SELECT Id FROM #DemoContacts)
                       OR ConvertedOpportunityId IN (SELECT Id FROM #DemoOpps))
                """),
            ("users of yours in a demo team",
                "SELECT COUNT(*) FROM AspNetUsers WHERE TeamId IN (SELECT Id FROM Teams WHERE Name LIKE N'Demo: %') AND Id NOT LIKE N'demo-%'"),
        };

        var blockers = new List<string>();
        foreach (var (description, sql) in checks)
        {
            var count = await ScalarAsync<int>(connection, transaction, sql, cancellationToken);
            if (count > 0)
            {
                blockers.Add($"{count:N0} {description}");
            }
        }

        return blockers;
    }

    private static async Task<int> Exec(
        SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 0;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ScalarAsync<T>(
        SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }
}
