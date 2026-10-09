using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ConfigureBaseColumns();

        builder.Property(e => e.Name).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.VatNumber).HasMaxLength(20);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Website).HasMaxLength(300);

        builder.HasOne(e => e.Industry).WithMany().HasForeignKey(e => e.IndustryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AccountStatus).WithMany().HasForeignKey(e => e.AccountStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ImportBatch).WithMany().HasForeignKey(e => e.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasUserForeignKey(e => e.OwnerId);

        // Unique among active accounts; VAT number only when filled.
        builder.HasIndex(e => e.Name).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasIndex(e => e.VatNumber).IsUnique().HasFilter("[VatNumber] IS NOT NULL AND [IsActive] = 1");
        builder.HasIndex(e => new { e.OwnerId, e.IsActive });
    }
}
