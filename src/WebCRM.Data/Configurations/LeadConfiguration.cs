using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
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
