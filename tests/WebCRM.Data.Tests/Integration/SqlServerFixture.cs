using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebCRM.Core.Entities;
using WebCRM.Core.Interfaces;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Integration;

/// <summary>
/// A throwaway SQL Server database, created from the real migrations and dropped afterwards, so tests
/// see what production sees: SQL translation, collations, unique indexes, rowversion.
/// </summary>
/// <remarks>
/// The server comes from the WEBCRM_TEST_SQLSERVER environment variable (a connection string without a
/// database name); the default is the local default instance with Windows authentication. When the server
/// cannot be reached the tests are skipped, not failed, so the suite still runs on a machine without SQL Server.
/// The context is registered the way Program.cs does it (Identity stores, schema version 3), because the
/// Identity options change the model and the migrations only match that model.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ServerVariable = "WEBCRM_TEST_SQLSERVER";

    public const string RequireVariable = "WEBCRM_TEST_REQUIRE_SQLSERVER";

    /// <summary>The default of the SQL Server Linux container image, which CI uses.</summary>
    private const string DatabaseCollation = "SQL_Latin1_General_CP1_CI_AS";

    private const string DefaultServer = "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _databaseName = "WebCRM_Test_" + Guid.NewGuid().ToString("N");

    /// <summary>The connection string of the throwaway database (for tools that open their own connection).</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    private ServiceProvider? _services;

    public bool Available { get; private set; }

    public string SkipReason { get; private set; } = string.Empty;

    public FakeCurrentUser CurrentUser { get; } = new() { UserId = "system-test" };

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable) is { Length: > 0 } value ? value : DefaultServer;
        var serverOnly = new SqlConnectionStringBuilder(server) { InitialCatalog = "master", ConnectTimeout = 5 }.ConnectionString;
        ConnectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = _databaseName,
            ConnectTimeout = 5,
            MultipleActiveResultSets = true,
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditFieldsInterceptor>();
        services.AddDbContextFactory<CrmDbContext>(
            (sp, options) => options
                .UseSqlServer(ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>()),
            ServiceLifetime.Scoped);
        services.AddIdentityCore<User>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CrmDbContext>();
        _services = services.BuildServiceProvider();

        try
        {
            // The database gets an explicit collation: the SQL Server Linux image (CI) defaults to
            // SQL_Latin1_General_CP1_CI_AS while a developer machine may use another (e.g. Greek_CI_AS), and the tests
            // must not depend on which. Columns that need Greek_100_CI_AI or a binary collation say so themselves.
            await using (var master = new SqlConnection(serverOnly))
            {
                await master.OpenAsync();
                await using var create = master.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{_databaseName}] COLLATE {DatabaseCollation}";
                await create.ExecuteNonQueryAsync();
            }

            await using var db = CreateContext();
            await db.Database.MigrateAsync();
            Available = true;
        }
        catch (SqlException ex)
        {
            SkipReason = $"SQL Server is not available ({ServerVariable} or localhost): {ex.Message}";

            // CI sets this so a missing SQL Server fails the build instead of quietly skipping the tests.
            if (Environment.GetEnvironmentVariable(RequireVariable) == "1")
            {
                throw new InvalidOperationException(SkipReason, ex);
            }
        }
    }

    public CrmDbContext CreateContext() => Factory.CreateDbContext();

    public IDbContextFactory<CrmDbContext> CreateFactory() => Factory;

    private IDbContextFactory<CrmDbContext> Factory =>
        _services!.CreateScope().ServiceProvider.GetRequiredService<IDbContextFactory<CrmDbContext>>();

    public async ValueTask DisposeAsync()
    {
        if (Available)
        {
            await using (var db = CreateContext())
            {
                await db.Database.EnsureDeletedAsync();
            }

            SqlConnection.ClearAllPools();
        }

        _services?.Dispose();
    }
}
