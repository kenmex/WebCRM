using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    private const string Bin = " COLLATE Latin1_General_100_BIN2";

    private static readonly string VatNumberCheckSql =
        "[VatNumber] IS NULL OR ("
        + "LEN([VatNumber]) BETWEEN 4 AND 20"
        + " AND [VatNumber]" + Bin + " NOT LIKE '%[^A-Z0-9]%'"
        + " AND ([VatNumber]" + Bin + " NOT LIKE 'EL%'"
        + " OR [VatNumber]" + Bin + " LIKE 'EL" + string.Concat(Enumerable.Repeat("[0-9]", 9)) + "'))";

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ConfigureBaseColumns();

        // Same rule as VatNumberRules (the checksum stays in code). The binary collation makes the A-Z and 0-9
        // ranges mean exactly those characters: under the database collation (case-insensitive) a lowercase
        // letter would pass.
        builder.ToTable(t => t.HasCheckConstraint("CK_Accounts_VatNumber", VatNumberCheckSql));

        builder.Property(e => e.Name).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.VatNumber).HasMaxLength(20);
        builder.Property(e => e.LegalName).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(254).UseCollation(AccentInsensitive);
        builder.Property(e => e.TaxOffice).HasMaxLength(100);
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
