using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Addresses_AddressType", IsDefinedEnum<AddressType>("AddressType")));

        // Sentinel = the default, as for the bit columns that default to 1.
        builder.Property(e => e.AddressType).HasDefaultValue(AddressType.Billing).HasSentinel(AddressType.Billing);
        builder.Property(e => e.Street).HasMaxLength(200);
        builder.Property(e => e.City).HasMaxLength(100).UseCollation(AccentInsensitive);
        builder.Property(e => e.Postcode).HasMaxLength(20);
        builder.Property(e => e.CountryCode).IsChar(2);

        builder.HasOne(e => e.Account).WithMany(a => a.Addresses).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);

        // Same soft-delete filter as the account it belongs to.
        builder.HasQueryFilter(e => e.Account.IsActive);

        // One billing and one shipping address per account.
        builder.HasIndex(e => new { e.AccountId, e.AddressType }).IsUnique();
        builder.HasIndex(e => e.City);
    }
}
