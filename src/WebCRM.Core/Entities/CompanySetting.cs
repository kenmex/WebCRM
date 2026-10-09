namespace WebCRM.Core.Entities;

/// <summary>Company settings and branding. Single row with Id = 1.</summary>
public class CompanySetting : IModificationAudited
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>PNG or SVG, max 500 KB; SVG sanitized on upload.</summary>
    public byte[]? LogoData { get; set; }

    public string? LogoContentType { get; set; }

    /// <summary>#RRGGBB; must pass WCAG AA with white text.</summary>
    public string PrimaryColor { get; set; } = "#594AE2";

    /// <summary>#RRGGBB; must pass WCAG AA with white text.</summary>
    public string SecondaryColor { get; set; } = "#C2185B";

    public string DefaultCurrency { get; set; } = "EUR";

    /// <summary>IANA time zone id.</summary>
    public string DefaultTimeZoneId { get; set; } = "Europe/Athens";

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? DefaultCountryCode { get; set; }

    public string DateFormat { get; set; } = "dd/MM/yyyy";

    /// <summary>On only for the public demo.</summary>
    public bool DemoMode { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
