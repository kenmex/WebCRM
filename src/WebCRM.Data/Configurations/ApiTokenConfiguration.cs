using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_ApiTokens_Scope", IsDefinedEnum<ApiTokenScope>("Scope")));

        builder.Property(e => e.Name).HasMaxLength(100);
        builder.HasUserForeignKey(e => e.UserId);
        builder.Property(e => e.TokenHash).HasMaxLength(32).IsFixedLength(); // binary(32)
        builder.Property(e => e.TokenPrefix).IsChar(8);
        // Sentinel = the default, as for the bit columns that default to 1.
        builder.Property(e => e.Scope).HasDefaultValue(ApiTokenScope.Read).HasSentinel(ApiTokenScope.Read);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql(UtcNowSql);
        builder.HasUserForeignKey(e => e.CreatedBy);

        builder.HasIndex(e => e.TokenHash).IsUnique();

        // Unique among unrevoked tokens.
        builder.HasIndex(e => e.Name).IsUnique().HasFilter("[RevokedAt] IS NULL");
    }
}
