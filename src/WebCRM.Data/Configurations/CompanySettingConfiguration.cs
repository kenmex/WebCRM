using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebCRM.Core.Entities;
using static WebCRM.Data.Configurations.ConfigurationExtensions;

namespace WebCRM.Data.Configurations;

public class CompanySettingConfiguration : IEntityTypeConfiguration<CompanySetting>
{
    public void Configure(EntityTypeBuilder<CompanySetting> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CompanySettings_SingleRow", $"[Id] = {CompanySetting.SingletonId}");
            t.HasCheckConstraint("CK_CompanySettings_PrimaryColor", IsHexColor("PrimaryColor"));
            t.HasCheckConstraint("CK_CompanySettings_SecondaryColor", IsHexColor("SecondaryColor"));
        });

        // Single row: the key is always 1, not an identity.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CompanyName).HasMaxLength(200);
        builder.Property(e => e.LogoData); // varbinary(max), per the data dictionary
        builder.Property(e => e.LogoContentType).HasMaxLength(50);
        builder.Property(e => e.PrimaryColor).IsChar(7).HasDefaultValue("#594AE2");
        builder.Property(e => e.SecondaryColor).IsChar(7).HasDefaultValue("#C2185B");
        builder.Property(e => e.DefaultCurrency).IsChar(3).HasDefaultValue("EUR");
        builder.Property(e => e.DefaultTimeZoneId).HasMaxLength(64).HasDefaultValue("Europe/Athens");
        builder.Property(e => e.DefaultCountryCode).IsChar(2);
        builder.Property(e => e.DateFormat).HasMaxLength(20).HasDefaultValue("dd/MM/yyyy");
        builder.Property(e => e.DemoMode).HasDefaultValue(false);
        builder.HasUserForeignKey(e => e.UpdatedBy);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
