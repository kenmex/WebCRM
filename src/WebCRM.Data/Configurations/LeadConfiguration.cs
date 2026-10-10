using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        // Convert sets all of these together and nothing else does: a lead is either not converted, or has the
        // time and the account and contact it was converted into (the opportunity is optional).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Leads_ConvertedConsistent",
            "([ConvertedAt] IS NULL AND [ConvertedAccountId] IS NULL AND [ConvertedContactId] IS NULL AND [ConvertedOpportunityId] IS NULL)"
            + " OR ([ConvertedAt] IS NOT NULL AND [ConvertedAccountId] IS NOT NULL AND [ConvertedContactId] IS NOT NULL)"));

        builder.ConfigureBaseColumns();

        builder.Property(e => e.Name).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.Company).HasMaxLength(200).UseCollation(AccentInsensitive);
        builder.Property(e => e.Email).HasMaxLength(254).UseCollation(AccentInsensitive);
        builder.Property(e => e.Phone).HasMaxLength(30);

        builder.HasOne(e => e.LeadSource).WithMany().HasForeignKey(e => e.LeadSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.LeadStatus).WithMany().HasForeignKey(e => e.LeadStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasUserForeignKey(e => e.OwnerId);

        builder.HasOne(e => e.ConvertedAccount).WithMany().HasForeignKey(e => e.ConvertedAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ConvertedContact).WithMany().HasForeignKey(e => e.ConvertedContactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ConvertedOpportunity).WithMany().HasForeignKey(e => e.ConvertedOpportunityId).OnDelete(DeleteBehavior.Restrict);
    }
}
