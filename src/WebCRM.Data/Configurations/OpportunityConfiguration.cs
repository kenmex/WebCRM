using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Opportunities_Amount", "[Amount] >= 0");
            t.HasCheckConstraint("CK_Opportunities_Probability", "[Probability] BETWEEN 0 AND 100");
        });

        builder.ConfigureBaseColumns();

        builder.Property(e => e.Name).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.Amount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(e => e.Currency).IsChar(3).HasDefaultValue("EUR");
        builder.Property(e => e.Probability).HasPrecision(5, 2);
        builder.Property(e => e.ProbabilityOverridden).HasDefaultValue(false);

        builder.HasOne(e => e.Account).WithMany(a => a.Opportunities).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PrimaryContact).WithMany().HasForeignKey(e => e.PrimaryContactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Stage).WithMany().HasForeignKey(e => e.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.LostReason).WithMany().HasForeignKey(e => e.LostReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasUserForeignKey(e => e.OwnerId);

        // Pipeline board, then closing-soon and overdue.
        builder.HasIndex(e => new { e.StageId, e.OwnerId, e.IsActive });
        builder.HasIndex(e => e.CloseDate);
    }
}
