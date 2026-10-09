using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Attachments_ExactlyOneLink", ExactlyOneNotNull("AccountId", "ContactId", "OpportunityId"));
            t.HasCheckConstraint("CK_Attachments_SizeBytes", $"[SizeBytes] <= {Attachment.MaxSizeBytes}");
        });

        builder.ConfigureBaseColumns();

        builder.Property(e => e.StoredName).HasMaxLength(100);
        builder.Property(e => e.OriginalName).HasMaxLength(255);
        builder.Property(e => e.ContentType).HasMaxLength(100);

        // Typed links (D1, no Lead); EF indexes each FK column.
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Opportunity).WithMany().HasForeignKey(e => e.OpportunityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.StoredName).IsUnique();
    }
}
