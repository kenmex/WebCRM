using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_AuditLogs_Changes", "ISJSON([Changes]) = 1"));

        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.Action).HasMaxLength(20);
        builder.Property(e => e.EntityName).HasMaxLength(50);
        builder.Property(e => e.Changes); // nvarchar(max), per the data dictionary
        builder.Property(e => e.Source).HasMaxLength(50).HasDefaultValue("UI");
        builder.Property(e => e.ChangedAt).HasDefaultValueSql(UtcNowSql);

        // History tabs, then per-user activity.
        builder.HasIndex(e => new { e.EntityName, e.EntityId, e.ChangedAt });
        builder.HasIndex(e => new { e.UserId, e.ChangedAt });
    }
}
