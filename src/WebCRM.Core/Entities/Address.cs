namespace WebCRM.Core.Entities;

/// <summary>Billing or shipping address of an account (no base columns, per the data dictionary).</summary>
public class Address
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public AddressType AddressType { get; set; } = AddressType.Billing;

    public string? Street { get; set; }

    public string? City { get; set; }

    public string? Postcode { get; set; }

    /// <summary>ISO 3166-1 alpha-2; defaults to the company default (set by the service).</summary>
    public string? CountryCode { get; set; }
}
