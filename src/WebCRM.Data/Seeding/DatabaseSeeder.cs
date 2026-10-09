using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebCRM.Core.Entities;
using WebCRM.Data.Interceptors;

namespace WebCRM.Data.Seeding;

/// <summary>
/// Idempotent start-up seed: system user, roles, CompanySetting row and the first Admin.
/// Every step checks before it inserts, so it is safe to run on every start in any environment.
/// It does not apply migrations.
/// </summary>
public static class DatabaseSeeder
{
    public const string DefaultCompanyName = "MexCrm";

    public static async Task SeedAsync(
        IServiceProvider services, string connectionString, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSeeder));

        // Own context so audited writes are stamped with the system user, not the web user.
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new AuditFieldsInterceptor(new SystemCurrentUser(), TimeProvider.System))
            .Options;
        await using var db = new CrmDbContext(options);

        if (!await db.Database.CanConnectAsync(cancellationToken)
            || (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            throw new InvalidOperationException(
                "The database is missing or not up to date. Apply migrations first: dotnet ef database update --project src/WebCRM.Data --startup-project src/WebCRM.Web");
        }

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureSystemUserAsync(userManager, logger);
        await EnsureRolesAsync(roleManager, logger);
        await EnsureCompanySettingAsync(db, logger, cancellationToken);
        await EnsureFirstAdminAsync(userManager, provider.GetRequiredService<IConfiguration>(), logger);
    }

    private static async Task EnsureSystemUserAsync(UserManager<User> userManager, ILogger logger)
    {
        if (await userManager.FindByIdAsync(SystemUser.Id) is not null)
        {
            return;
        }

        // No password and no email, locked out for good: it can never sign in.
        var user = new User
        {
            Id = SystemUser.Id,
            UserName = SystemUser.UserName,
            DisplayName = SystemUser.DisplayName,
            IsActive = false,
            EmailReminders = false,
            NotifyAssigned = false,
            NotifyTaskDue = false,
            NotifyRecordChanged = false,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue,
        };
        ThrowIfFailed(await userManager.CreateAsync(user), "create the system user");
        logger.LogInformation("Seeded the system user.");
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var name in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(name))
            {
                continue;
            }

            ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole(name)), $"create role {name}");
            logger.LogInformation("Seeded role {Role}.", name);
        }
    }

    private static async Task EnsureCompanySettingAsync(CrmDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.CompanySettings.AnyAsync(c => c.Id == CompanySetting.SingletonId, ct))
        {
            return;
        }

        db.CompanySettings.Add(new CompanySetting { CompanyName = DefaultCompanyName });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded CompanySetting.");
    }

    private static async Task EnsureFirstAdminAsync(
        UserManager<User> userManager, IConfiguration configuration, ILogger logger)
    {
        if ((await userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count > 0)
        {
            return;
        }

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        var displayName = configuration["Seed:AdminDisplayName"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(displayName))
        {
            logger.LogWarning(
                "No Admin exists and Seed:AdminEmail, Seed:AdminPassword and Seed:AdminDisplayName are not all set. Skipping the first Admin.");
            return;
        }

        // An existing account with this email (e.g. a registered user): promote it instead of failing.
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                MustChangePassword = false,
            };
            ThrowIfFailed(await userManager.CreateAsync(user, password), "create the first Admin");
        }

        ThrowIfFailed(await userManager.AddToRoleAsync(user, RoleNames.Admin), "add the first Admin to the Admin role");
        logger.LogInformation("Seeded the first Admin {Email}.", email);
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeder could not {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
