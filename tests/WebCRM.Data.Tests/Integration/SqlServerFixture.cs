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

    private const string DefaultServer = "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _databaseName = "WebCRM_Test_" + Guid.NewGuid().ToString("N");

    private ServiceProvider? _services;

    public bool Available { get; private set; }

    public string SkipReason { get; private set; } = string.Empty;

    public FakeCurrentUser CurrentUser { get; } = new() { UserId = "system-test" };

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable) is { Length: > 0 } value ? value : DefaultServer;
        var connectionString = new SqlConnectionStringBuilder(server)
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
                .UseSqlServer(connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditFieldsInterceptor>()),
            ServiceLifetime.Scoped);
        services.AddIdentityCore<User>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CrmDbContext>();
        _services = services.BuildServiceProvider();

        try
        {
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
