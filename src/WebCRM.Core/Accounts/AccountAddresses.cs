using System.ComponentModel.DataAnnotations;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Core.Accounts;

/// <summary>An account's billing and shipping address, flat so one form (and one set of field errors) covers both.</summary>
public sealed class AccountAddressesEditModel
{
    [StringLength(200)]
    public string? BillingStreet { get; set; }

    [StringLength(100)]
    public string? BillingCity { get; set; }

    [StringLength(20)]
    public string? BillingPostcode { get; set; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use a 2-letter country code, e.g. GR.")]
    public string? BillingCountryCode { get; set; }

    [StringLength(200)]
    public string? ShippingStreet { get; set; }

    [StringLength(100)]
    public string? ShippingCity { get; set; }

    [StringLength(20)]
    public string? ShippingPostcode { get; set; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use a 2-letter country code, e.g. GR.")]
    public string? ShippingCountryCode { get; set; }

    public bool HasBilling => Any(BillingStreet, BillingCity, BillingPostcode, BillingCountryCode);

    public bool HasShipping => Any(ShippingStreet, ShippingCity, ShippingPostcode, ShippingCountryCode);

    public AccountAddressesEditModel Clone() => (AccountAddressesEditModel)MemberwiseClone();

    private static bool Any(params string?[] values) => values.Any(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>
/// Billing and shipping addresses of an account (the Overview tab on P9). Addresses have no RowVersion in the
/// data dictionary, so there is no conflict check: the last save wins.
/// </summary>
public interface IAccountAddressService
{
    /// <summary>Null when the account does not exist or the user cannot see it.</summary>
    Task<AccountAddressesEditModel?> GetAsync(int accountId, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves both addresses. An address with every field blank is removed. A missing country code is filled from the
    /// company's default country. Results: Saved, Invalid (field errors) or NotFound.
    /// </summary>
    Task<SaveResult> SaveAsync(
        int accountId, AccountAddressesEditModel model, UserContext user, CancellationToken cancellationToken = default);
}
