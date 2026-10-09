using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Tests.Model;

/// <summary>
/// Guards on the real CrmDbContext model (SQL Server provider, no connection needed),
/// so later changes cannot quietly drift from docs/data-dictionary.md conventions.
/// </summary>
public class CrmModelTests
{
    // nvarchar(max) / varbinary(max) only where the data dictionary says so.
    private static readonly HashSet<string> AllowedMaxColumns =
    [
        "Activity.Description",
        "AuditLog.Changes",
        "CompanySetting.LogoData",
    ];

    private static IModel DesignTimeModel()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var db = new CrmDbContext(options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    // Our own entities and our own properties (not the ones inherited from IdentityUser).
    private static bool IsOurs(Type type) => type.Assembly == typeof(BaseEntity).Assembly;

    [Fact]
    public void Only_allowed_columns_are_max_length()
    {
        var offenders = DesignTimeModel().GetEntityTypes()
            .Where(t => IsOurs(t.ClrType))
            .SelectMany(t => t.GetProperties()
                .Where(p => p.PropertyInfo?.DeclaringType is { } declaring && IsOurs(declaring))
                .Select(p => (Name: $"{t.ClrType.Name}.{p.Name}", Type: p.GetColumnType())))
            .Where(c => c.Type.Contains("max", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .Where(name => !AllowedMaxColumns.Contains(name))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void All_our_foreign_keys_are_restrict()
    {
        var notRestrict = DesignTimeModel().GetEntityTypes()
            .Where(t => IsOurs(t.ClrType))
            .SelectMany(t => t.GetDeclaredForeignKeys())
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Restrict)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}({string.Join(", ", fk.Properties.Select(p => p.Name))})")
            .ToList();

        notRestrict.ShouldBeEmpty();
    }

    [Fact]
    public void Audit_columns_have_foreign_keys_but_no_indexes()
    {
        string[] auditColumns = [nameof(ICreationAudited.CreatedBy), nameof(IModificationAudited.UpdatedBy)];
        var auditProperties = DesignTimeModel().GetEntityTypes()
            .Where(t => IsOurs(t.ClrType))
            .SelectMany(t => t.GetProperties().Where(p => auditColumns.Contains(p.Name)))
            .ToList();

        auditProperties.ShouldNotBeEmpty();
        auditProperties
            .Where(p => !p.IsForeignKey())
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
            .ShouldBeEmpty();
        auditProperties
            .Where(p => p.IsIndex())
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
            .ShouldBeEmpty();
    }

    [Fact]
    public void All_times_are_datetime2_0()
    {
        var wrong = DesignTimeModel().GetEntityTypes()
            .Where(t => IsOurs(t.ClrType))
            .SelectMany(t => t.GetProperties()
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                .Select(p => (Name: $"{t.ClrType.Name}.{p.Name}", Type: p.GetColumnType())))
            .Where(c => c.Type != "datetime2(0)")
            .Select(c => $"{c.Name}: {c.Type}")
            .ToList();

        wrong.ShouldBeEmpty();
    }
}
