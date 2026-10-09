using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Configurations;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Stages_DefaultProbability", "[DefaultProbability] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Stages_NotWonAndLost", "NOT ([IsWon] = 1 AND [IsLost] = 1)");
        });

        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.DefaultProbability).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(e => e.IsWon).HasDefaultValue(false);
        builder.Property(e => e.IsLost).HasDefaultValue(false);
        builder.Property(e => e.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.HasIndex(e => e.Name).IsUnique();

        // At most one Won and one Lost stage; "at least one" is a service rule (P22).
        builder.HasIndex(e => e.IsWon).IsUnique().HasFilter("[IsWon] = 1");
        builder.HasIndex(e => e.IsLost).IsUnique().HasFilter("[IsLost] = 1");

        // Starter set; Admin edits it on P22.
        builder.HasData(
            new Stage { Id = 1, Name = "Prospecting", SortOrder = 10, DefaultProbability = 10m },
            new Stage { Id = 2, Name = "Qualification", SortOrder = 20, DefaultProbability = 20m },
            new Stage { Id = 3, Name = "Proposal", SortOrder = 30, DefaultProbability = 50m },
            new Stage { Id = 4, Name = "Negotiation", SortOrder = 40, DefaultProbability = 75m },
            new Stage { Id = 5, Name = "Won", SortOrder = 90, DefaultProbability = 100m, IsWon = true },
            new Stage { Id = 6, Name = "Lost", SortOrder = 100, DefaultProbability = 0m, IsLost = true });
    }
}
