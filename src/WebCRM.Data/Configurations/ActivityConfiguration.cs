using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Activities_ExactlyOneLink", ExactlyOneNotNull("AccountId", "ContactId", "OpportunityId", "LeadId"));
            t.HasCheckConstraint("CK_Activities_DurationMinutes", "[DurationMinutes] > 0");
        });

        builder.ConfigureBaseColumns();

        builder.Property(e => e.Subject).HasMaxLength(200);
        builder.Property(e => e.Description); // nvarchar(max), per the data dictionary

        builder.HasOne(e => e.ActivityType).WithMany().HasForeignKey(e => e.ActivityTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasUserForeignKey(e => e.OwnerId);

        // Typed links (D1); EF indexes each FK column.
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Opportunity).WithMany().HasForeignKey(e => e.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Lead).WithMany().HasForeignKey(e => e.LeadId).OnDelete(DeleteBehavior.Restrict);

        // My tasks and overdue counts.
        builder.HasIndex(e => new { e.OwnerId, e.DoneAt, e.DueAt });
    }
}
