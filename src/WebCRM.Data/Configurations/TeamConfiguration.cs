using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ConfigureBaseColumns();

        builder.Property(e => e.Name).HasMaxLength(100);
        builder.HasUserForeignKey(e => e.ManagerId);

        // Unique among active teams.
        builder.HasIndex(e => e.Name).IsUnique().HasFilter("[IsActive] = 1");
    }
}
