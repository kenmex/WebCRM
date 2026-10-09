using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.Type).HasMaxLength(30);
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.Link).HasMaxLength(300);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql(UtcNowSql);

        // Bell: unread count and the latest first.
        builder.HasIndex(e => new { e.UserId, e.ReadAt, e.CreatedAt });
    }
}
