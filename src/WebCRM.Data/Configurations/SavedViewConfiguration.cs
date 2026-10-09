using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

public class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.ListKey).HasMaxLength(50);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.QueryString).HasMaxLength(2000);
        builder.Property(e => e.IsPublic).HasDefaultValue(false);

        builder.HasIndex(e => new { e.UserId, e.ListKey, e.Name }).IsUnique();
    }
}
