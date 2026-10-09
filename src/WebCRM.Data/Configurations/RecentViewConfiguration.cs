using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class RecentViewConfiguration : IEntityTypeConfiguration<RecentView>
{
    public void Configure(EntityTypeBuilder<RecentView> builder)
    {
        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.EntityName).HasMaxLength(50);
        builder.Property(e => e.ViewedAt).HasDefaultValueSql(UtcNowSql);

        builder.HasIndex(e => new { e.UserId, e.EntityName, e.EntityId }).IsUnique();
    }
}
