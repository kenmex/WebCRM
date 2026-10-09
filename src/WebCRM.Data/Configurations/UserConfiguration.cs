using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

/// <summary>User columns per docs/data-dictionary.md (table stays AspNetUsers).</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .UseCollation("Greek_100_CI_AI")
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(u => u.TimeZoneId)
            .HasMaxLength(64)
            .IsRequired()
            .HasDefaultValue("Europe/Athens");

        builder.Property(u => u.Theme)
            .HasDefaultValue(ThemePreference.System);

        // Bit columns that default to 1: the sentinel is true, so EF sends an explicit
        // false but leaves true to the database default.
        builder.Property(u => u.EmailReminders).HasDefaultValue(true).HasSentinel(true);
        builder.Property(u => u.NotifyAssigned).HasDefaultValue(true).HasSentinel(true);
        builder.Property(u => u.NotifyTaskDue).HasDefaultValue(true).HasSentinel(true);
        builder.Property(u => u.NotifyRecordChanged).HasDefaultValue(true).HasSentinel(true);
        builder.Property(u => u.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(u => u.MustChangePassword)
            .HasDefaultValue(false);

        builder.Property(u => u.LastSignInAt)
            .HasColumnType("datetime2(0)");
    }
}
