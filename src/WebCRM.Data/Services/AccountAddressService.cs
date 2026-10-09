using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Data.Services;

public sealed class AccountAddressService(IDbContextFactory<CrmDbContext> factory) : IAccountAddressService
{
    public async Task<AccountAddressesEditModel?> GetAsync(
        int accountId, UserContext user, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        if (!await db.Accounts.AsNoTracking().VisibleTo(user).AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return null;
        }

        var addresses = await db.Addresses.AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .ToListAsync(cancellationToken);
        var billing = addresses.FirstOrDefault(a => a.AddressType == AddressType.Billing);
        var shipping = addresses.FirstOrDefault(a => a.AddressType == AddressType.Shipping);

        return new AccountAddressesEditModel
        {
            BillingStreet = billing?.Street,
            BillingCity = billing?.City,
            BillingPostcode = billing?.Postcode,
            BillingCountryCode = billing?.CountryCode,
            ShippingStreet = shipping?.Street,
            ShippingCity = shipping?.City,
            ShippingPostcode = shipping?.Postcode,
            ShippingCountryCode = shipping?.CountryCode,
        };
    }

    public async Task<SaveResult> SaveAsync(
        int accountId, AccountAddressesEditModel model, UserContext user, CancellationToken cancellationToken = default)
    {
        Normalise(model);

        var errors = new Dictionary<string, string>();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                errors.TryAdd(member, result.ErrorMessage ?? "Invalid value.");
            }
        }

        if (errors.Count > 0)
        {
            return new SaveResult(SaveStatus.Invalid, FieldErrors: errors);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        if (!await db.Accounts.VisibleTo(user).AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return new SaveResult(SaveStatus.NotFound);
        }

        var defaultCountry = await db.CompanySettings.AsNoTracking()
            .Select(c => c.DefaultCountryCode).FirstOrDefaultAsync(cancellationToken);
        var existing = await db.Addresses.Where(a => a.AccountId == accountId).ToListAsync(cancellationToken);

        Apply(db, existing, accountId, AddressType.Billing, model.HasBilling,
            model.BillingStreet, model.BillingCity, model.BillingPostcode, model.BillingCountryCode, defaultCountry);
        Apply(db, existing, accountId, AddressType.Shipping, model.HasShipping,
            model.ShippingStreet, model.ShippingCity, model.ShippingPostcode, model.ShippingCountryCode, defaultCountry);

        await db.SaveChangesAsync(cancellationToken);
        return new SaveResult(SaveStatus.Saved, accountId);
    }

    private static void Apply(
        CrmDbContext db, List<Address> existing, int accountId, AddressType type, bool hasValues,
        string? street, string? city, string? postcode, string? country, string? defaultCountry)
    {
        var current = existing.FirstOrDefault(a => a.AddressType == type);

        // Nothing typed: the address does not exist (the unique index allows one of each type at most).
        if (!hasValues)
        {
            if (current is not null)
            {
                db.Addresses.Remove(current);
            }

            return;
        }

        if (current is null)
        {
            current = new Address { AccountId = accountId, AddressType = type };
            db.Addresses.Add(current);
        }

        current.Street = street;
        current.City = city;
        current.Postcode = postcode;
        current.CountryCode = country ?? defaultCountry?.Trim().ToUpperInvariant();
    }

    private static void Normalise(AccountAddressesEditModel model)
    {
        model.BillingStreet = Blank(model.BillingStreet);
        model.BillingCity = Blank(model.BillingCity);
        model.BillingPostcode = Blank(model.BillingPostcode);
        model.BillingCountryCode = Blank(model.BillingCountryCode)?.ToUpperInvariant();
        model.ShippingStreet = Blank(model.ShippingStreet);
        model.ShippingCity = Blank(model.ShippingCity);
        model.ShippingPostcode = Blank(model.ShippingPostcode);
        model.ShippingCountryCode = Blank(model.ShippingCountryCode)?.ToUpperInvariant();
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
