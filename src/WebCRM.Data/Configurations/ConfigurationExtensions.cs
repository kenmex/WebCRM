using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

/// <summary>Shared pieces of the entity configurations (conventions from docs/data-dictionary.md).</summary>
internal static class ConfigurationExtensions
{
    /// <summary>Accent- and case-insensitive collation for search columns (marked AI in the data dictionary).</summary>
    public const string AccentInsensitive = "Greek_100_CI_AI";

    public const string UtcNowSql = "sysutcdatetime()";

    /// <summary>Length of the ASP.NET Core Identity user key.</summary>
    public const int UserIdLength = 450;

    /// <summary>Base columns: CreatedAt/By, UpdatedAt/By, IsActive, RowVersion.</summary>
    public static void ConfigureBaseColumns<T>(this EntityTypeBuilder<T> builder)
        where T : BaseEntity
    {
        builder.Property(e => e.CreatedAt).HasDefaultValueSql(UtcNowSql);
        builder.HasUserForeignKey(e => e.CreatedBy);
        builder.HasUserForeignKey(e => e.UpdatedBy);

        // Default 1 with sentinel true: EF sends an explicit false, leaves true to the database default.
        builder.Property(e => e.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(e => e.RowVersion).IsRowVersion();
    }

    /// <summary>nvarchar(450) column with a Restrict FK to AspNetUsers.</summary>
    public static void HasUserForeignKey<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> property)
        where T : class
    {
        builder.Property(property).HasMaxLength(UserIdLength);

        var name = ((MemberExpression)property.Body).Member.Name;
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(name)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Fixed-length non-Unicode column: char(length).</summary>
    public static PropertyBuilder<TProperty> IsChar<TProperty>(this PropertyBuilder<TProperty> builder, int length) =>
        builder.HasMaxLength(length).IsFixedLength().IsUnicode(false);

    /// <summary>Check-constraint SQL: exactly one of the columns is not null (D1 typed links).</summary>
    public static string ExactlyOneNotNull(params string[] columns) =>
        "(" + string.Join(" + ", columns.Select(c => $"CASE WHEN [{c}] IS NULL THEN 0 ELSE 1 END")) + ") = 1";

    /// <summary>Check-constraint SQL: column is #RRGGBB.</summary>
    public static string IsHexColor(string column) =>
        $"[{column}] LIKE '#" + string.Concat(Enumerable.Repeat("[0-9A-Fa-f]", 6)) + "'";

    /// <summary>Check-constraint SQL: column holds one of the enum's values.</summary>
    public static string IsDefinedEnum<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetValues<TEnum>().Select(v => Convert.ToByte(v)))})";
}
