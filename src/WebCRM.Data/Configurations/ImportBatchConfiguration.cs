using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ImportBatches_Status", IsDefinedEnum<ImportStatus>("Status"));
            t.HasCheckConstraint("CK_ImportBatches_DuplicateRule", IsDefinedEnum<DuplicateRule>("DuplicateRule"));
        });

        builder.Property(e => e.Entity).HasMaxLength(20);
        builder.Property(e => e.FileName).HasMaxLength(255);
        builder.Property(e => e.Status).HasDefaultValue(ImportStatus.Queued);
        builder.Property(e => e.DuplicateRule).HasDefaultValue(DuplicateRule.Skip);
        builder.Property(e => e.TotalRows).HasDefaultValue(0);
        builder.Property(e => e.OkRows).HasDefaultValue(0);
        builder.Property(e => e.WarningRows).HasDefaultValue(0);
        builder.Property(e => e.ErrorRows).HasDefaultValue(0);
        builder.Property(e => e.ErrorReportName).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql(UtcNowSql);
        builder.HasUserForeignKey(e => e.CreatedBy);
    }
}
