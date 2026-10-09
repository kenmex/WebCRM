using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class FavouriteConfiguration : IEntityTypeConfiguration<Favourite>
{
    public void Configure(EntityTypeBuilder<Favourite> builder)
    {
        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.EntityName).HasMaxLength(50);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql(UtcNowSql);

        builder.HasIndex(e => new { e.UserId, e.EntityName, e.EntityId }).IsUnique();
    }
}
