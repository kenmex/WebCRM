using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

/// <summary>Columns and indexes shared by every lookup table; each lookup has its own table.</summary>
public abstract class LookupConfiguration<T> : IEntityTypeConfiguration<T>
    where T : Lookup
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.SortOrder).HasDefaultValue(0);
        builder.Property(e => e.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.Property(e => e.SystemCode).HasMaxLength(30);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.SystemCode).IsUnique().HasFilter("[SystemCode] IS NOT NULL");
    }
}

public class IndustryConfiguration : LookupConfiguration<Industry>;

public class LeadSourceConfiguration : LookupConfiguration<LeadSource>;

public class AccountStatusConfiguration : LookupConfiguration<AccountStatus>;

public class LostReasonConfiguration : LookupConfiguration<LostReason>;

public class SalutationConfiguration : LookupConfiguration<Salutation>;

public class LeadStatusConfiguration : LookupConfiguration<LeadStatus>
{
    public override void Configure(EntityTypeBuilder<LeadStatus> builder)
    {
        base.Configure(builder);

        // P12 statuses; the coded ones are used by the code (Convert, default filter).
        builder.HasData(
            new LeadStatus { Id = 1, Name = "New", SortOrder = 10, SystemCode = LeadStatus.New },
            new LeadStatus { Id = 2, Name = "Contacted", SortOrder = 20 },
            new LeadStatus { Id = 3, Name = "Qualified", SortOrder = 30 },
            new LeadStatus { Id = 4, Name = "Disqualified", SortOrder = 80, SystemCode = LeadStatus.Disqualified },
            new LeadStatus { Id = 5, Name = "Converted", SortOrder = 90, SystemCode = LeadStatus.Converted });
    }
}

public class ActivityTypeConfiguration : LookupConfiguration<ActivityType>
{
    public override void Configure(EntityTypeBuilder<ActivityType> builder)
    {
        base.Configure(builder);

        builder.HasData(
            new ActivityType { Id = 1, Name = "Task", SortOrder = 10, SystemCode = ActivityType.Task },
            new ActivityType { Id = 2, Name = "Call", SortOrder = 20, SystemCode = ActivityType.Call },
            new ActivityType { Id = 3, Name = "Meeting", SortOrder = 30, SystemCode = ActivityType.Meeting });
    }
}
