using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ConfigureBaseColumns();

        builder.Property(e => e.FirstName).HasMaxLength(100).UseCollation(AccentInsensitive);
        builder.Property(e => e.LastName).HasMaxLength(100).UseCollation(AccentInsensitive);

        // Persisted computed column, indexed for search and sort; inherits the AI collation.
        builder.Property(e => e.FullName)
            .HasMaxLength(201)
            .HasComputedColumnSql("CONCAT_WS(' ', [FirstName], [LastName])", stored: true);

        builder.Property(e => e.JobTitle).HasMaxLength(100);
        builder.Property(e => e.Email).HasMaxLength(254).UseCollation(AccentInsensitive);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Mobile).HasMaxLength(30);

        builder.HasOne(e => e.Account).WithMany(a => a.Contacts).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ImportBatch).WithMany().HasForeignKey(e => e.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasUserForeignKey(e => e.OwnerId);

        builder.HasIndex(e => e.FullName);
        builder.HasIndex(e => e.Email);
        builder.HasIndex(e => new { e.OwnerId, e.IsActive });
    }
}
