using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Services;
using WebCRM.Data.Tests.Interceptors;

namespace WebCRM.Data.Tests.Services;

public class AccountAddressServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly Factory _factory;
    private readonly AccountAddressService _service;
    private readonly UserContext _user = new("user-1", RoleNames.Sales, TeamId: null);
    private int _accountId;

    public AccountAddressServiceTests()
    {
        _factory = new Factory(_dbName);
        _service = new AccountAddressService(_factory);
        Seed();
    }

    private void Seed()
    {
        using var db = _factory.CreateDbContext();
        db.Users.Add(new User { Id = "user-1", UserName = "u1", DisplayName = "User One" });
        db.AccountStatuses.Add(new AccountStatus { Id = 1, Name = "Active" });
        var account = new Account { Name = "Acme", OwnerId = "user-1", AccountStatusId = 1, CreatedBy = "user-1" };
        db.Accounts.Add(account);
        db.SaveChangesAsync(Ct).GetAwaiter().GetResult(); // audited, so the interceptor needs the async save
        _accountId = account.Id;
    }

    private async Task SetCountryAsync(string? code)
    {
        await using var db = _factory.CreateDbContext();
        db.CompanySettings.Add(new CompanySetting { CompanyName = "Test Co", DefaultCountryCode = code });
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Get_returns_an_empty_form_for_an_account_without_addresses_and_null_for_a_missing_account()
    {
        var model = await _service.GetAsync(_accountId, _user, Ct);

        model.ShouldNotBeNull();
        model.HasBilling.ShouldBeFalse();
        model.HasShipping.ShouldBeFalse();
        (await _service.GetAsync(9999, _user, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Save_creates_both_addresses_and_get_returns_them()
    {
        var model = new AccountAddressesEditModel
        {
            BillingStreet = " 1 Ermou St ",
            BillingCity = "Athens",
            BillingPostcode = "10563",
            BillingCountryCode = "gr",
            ShippingCity = "Patras",
            ShippingCountryCode = "GR",
        };

        var result = await _service.SaveAsync(_accountId, model, _user, Ct);

        result.Status.ShouldBe(SaveStatus.Saved);
        var saved = await _service.GetAsync(_accountId, _user, Ct);
        saved!.BillingStreet.ShouldBe("1 Ermou St");
        saved.BillingCity.ShouldBe("Athens");
        saved.BillingCountryCode.ShouldBe("GR");
        saved.ShippingCity.ShouldBe("Patras");
        saved.ShippingStreet.ShouldBeNull();
    }

    [Fact]
    public async Task Save_updates_in_place_and_removes_an_address_when_every_field_is_cleared()
    {
        await _service.SaveAsync(_accountId, new AccountAddressesEditModel { BillingCity = "Athens", ShippingCity = "Patras" }, _user, Ct);

        await _service.SaveAsync(_accountId, new AccountAddressesEditModel { BillingCity = "Thessaloniki", ShippingCity = " " }, _user, Ct);

        var saved = await _service.GetAsync(_accountId, _user, Ct);
        saved!.BillingCity.ShouldBe("Thessaloniki");
        saved.HasShipping.ShouldBeFalse();
        await using var db = _factory.CreateDbContext();
        (await db.Addresses.CountAsync(a => a.AccountId == _accountId, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task A_missing_country_is_filled_from_the_company_default_but_only_for_an_address_that_exists()
    {
        await SetCountryAsync("GR");

        await _service.SaveAsync(_accountId, new AccountAddressesEditModel { BillingCity = "Athens" }, _user, Ct);

        var saved = await _service.GetAsync(_accountId, _user, Ct);
        saved!.BillingCountryCode.ShouldBe("GR");
        saved.ShippingCountryCode.ShouldBeNull();
        saved.HasShipping.ShouldBeFalse();
    }

    [Theory]
    [InlineData("GRC")]
    [InlineData("G")]
    [InlineData("12")]
    public async Task Save_rejects_a_country_code_that_is_not_two_letters(string code)
    {
        var result = await _service.SaveAsync(
            _accountId, new AccountAddressesEditModel { ShippingCity = "Athens", ShippingCountryCode = code }, _user, Ct);

        result.Status.ShouldBe(SaveStatus.Invalid);
        result.FieldErrors!.ShouldContainKey(nameof(AccountAddressesEditModel.ShippingCountryCode));
    }

    [Fact]
    public async Task Save_rejects_values_that_are_too_long_and_returns_not_found_for_a_missing_account()
    {
        var tooLong = new AccountAddressesEditModel { BillingPostcode = new string('1', 21) };
        (await _service.SaveAsync(_accountId, tooLong, _user, Ct)).FieldErrors!
            .ShouldContainKey(nameof(AccountAddressesEditModel.BillingPostcode));

        (await _service.SaveAsync(9999, new AccountAddressesEditModel { BillingCity = "Athens" }, _user, Ct))
            .Status.ShouldBe(SaveStatus.NotFound);
    }

    private sealed class Factory(string dbName) : IDbContextFactory<CrmDbContext>
    {
        private readonly FakeCurrentUser _currentUser = new() { UserId = "user-1" };

        public CrmDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CrmDbContext>()
                .UseInMemoryDatabase(dbName)
                .AddInterceptors(new AuditFieldsInterceptor(_currentUser, TimeProvider.System))
                .Options;
            return new CrmDbContext(options);
        }
    }
}
