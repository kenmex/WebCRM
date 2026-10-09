using Microsoft.Data.SqlClient;

namespace WebCRM.DemoData;

/// <summary>
/// The tool writes and deletes a lot of rows, so it only ever runs against a local development database:
/// the environment must be Development and the SQL Server must be on this machine.
/// </summary>
public static class DemoGuard
{
    public const string RequiredEnvironment = "Development";

    private static readonly HashSet<string> LocalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".", "(local)", "localhost", "127.0.0.1", "::1", "(localdb)",
    };

    /// <summary>Null when it is fine to go on, otherwise why the tool refuses.</summary>
    public static string? Refusal(string? environment, string? connectionString)
    {
        if (!string.Equals(environment, RequiredEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            return $"Refusing to run: the environment is '{environment ?? "(not set)"}', and demo data is for {RequiredEnvironment} only. "
                + "Use `dotnet run --project src/WebCRM.DemoData` (its launch profile sets DOTNET_ENVIRONMENT=Development).";
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "No connection string. Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\" "
                + "--project src/WebCRM.DemoData";
        }

        string server;
        try
        {
            server = new SqlConnectionStringBuilder(connectionString).DataSource;
        }
        catch (ArgumentException)
        {
            return "The connection string is not valid.";
        }

        return IsLocal(server)
            ? null
            : $"Refusing to run: the SQL Server '{server}' is not on this machine. Demo data only goes into a local database.";
    }

    /// <summary>"localhost", "tcp:localhost,1433", ".\SQLEXPRESS" and this machine's own name count as local.</summary>
    public static bool IsLocal(string dataSource)
    {
        var host = dataSource.Trim();
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        var cut = host.IndexOfAny([',', '\\']);
        if (cut >= 0)
        {
            host = host[..cut];
        }

        return LocalHosts.Contains(host) || string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }
}
