using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

public class AccountOverviewTabTests : MudTestContext
{
    private const string LeaveDialog = "Leave without saving?";

    private readonly FakeAddressService _addresses = new();
    private readonly NavigationManager _navigation;

    public AccountOverviewTabTests()
    {
        _addresses.Addresses[5] = new AccountAddressesEditModel
        {
            BillingStreet = "1 Ermou St",
            BillingCity = "Athens",
            BillingPostcode = "10563",
            BillingCountryCode = "GR",
        };
        Services.AddSingleton<IAccountAddressService>(_addresses);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/accounts/5");
    }

    private IRenderedComponent<AccountOverviewTab> RenderTab(int accountId = 5) =>
        Render<AccountOverviewTab>(p => p.Add(x => x.AccountId, accountId));

    private static void Press(IRenderedComponent<AccountOverviewTab> cut, string text)
    {
        cut.WaitForAssertion(() => cut.HasButton(text).ShouldBeTrue());
        cut.ButtonByText(text).Click();
    }

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    [Fact]
    public void Read_mode_shows_the_billing_address_and_a_dash_for_the_missing_shipping_address()
    {
        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("1 Ermou St"));
        cut.Markup.ShouldContain("10563 Athens");
        cut.FindAll("address").Count.ShouldBe(1); // shipping has none, so only a dash
        cut.FindAll("[aria-label=empty]").Count.ShouldBe(1);
    }

    [Fact]
    public void An_account_without_addresses_shows_two_dashes_and_can_still_be_edited()
    {
        _addresses.Addresses[5] = new AccountAddressesEditModel();

        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.FindAll("[aria-label=empty]").Count.ShouldBe(2));
        cut.HasButton("Edit addresses").ShouldBeTrue();
    }

    [Fact]
    public void Edit_shows_all_eight_fields_with_the_current_values()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");

        cut.WaitForAssertion(() => cut.InputByLabel("Billing city").GetAttribute("value").ShouldBe("Athens"));
        cut.InputByLabel("Billing street").GetAttribute("value").ShouldBe("1 Ermou St");
        cut.InputByLabel("Billing postcode").GetAttribute("value").ShouldBe("10563");
        cut.InputByLabel("Billing country code").GetAttribute("value").ShouldBe("GR");
        cut.InputByLabel("Shipping street");
        cut.InputByLabel("Shipping city");
        cut.InputByLabel("Shipping postcode");
        cut.InputByLabel("Shipping country code");
    }

    [Fact]
    public void Save_sends_the_form_returns_to_read_mode_and_shows_the_new_address_without_a_leave_prompt()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");
        cut.WaitForAssertion(() => cut.InputByLabel("Shipping city"));
        cut.InputByLabel("Shipping city").Change("Patras");
        cut.InputByLabel("Billing city").Change("Thessaloniki");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.HasButton("Edit addresses").ShouldBeTrue());
        var (accountId, saved) = _addresses.Saves.Single();
        accountId.ShouldBe(5);
        saved.ShippingCity.ShouldBe("Patras");
        saved.BillingCity.ShouldBe("Thessaloniki");
        cut.Markup.ShouldContain("Thessaloniki");
        cut.Markup.ShouldContain("Patras");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_server_error_is_shown_on_its_field_and_the_form_stays_open()
    {
        _addresses.OnSave = _ => new SaveResult(
            SaveStatus.Invalid,
            FieldErrors: new Dictionary<string, string> { [nameof(AccountAddressesEditModel.BillingCountryCode)] = "Server says no." });
        var cut = RenderTab();
        Press(cut, "Edit addresses");
        cut.WaitForAssertion(() => cut.InputByLabel("Billing city"));
        cut.InputByLabel("Billing city").Change("Athens 2");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Server says no."));
        cut.HasButton("Save").ShouldBeTrue();
    }

    [Fact]
    public void A_country_code_that_is_not_two_letters_is_rejected_by_the_form_before_the_server_is_called()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");
        cut.WaitForAssertion(() => cut.InputByLabel("Billing country code"));
        cut.InputByLabel("Billing country code").Change("GRC");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Use a 2-letter country code, e.g. GR."));
        _addresses.Saves.ShouldBeEmpty();
    }

    [Fact]
    public void Cancelling_a_clean_form_just_closes_it()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");

        Press(cut, "Cancel");

        cut.WaitForAssertion(() => cut.HasButton("Edit addresses").ShouldBeTrue());
        DialogProvider.Markup.ShouldNotContain("Discard changes?");
    }

    [Fact]
    public void Cancelling_a_dirty_form_asks_once_then_discards_and_leaving_does_not_prompt_again()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");
        cut.WaitForAssertion(() => cut.InputByLabel("Billing city"));
        cut.InputByLabel("Billing city").Change("Elsewhere");

        Press(cut, "Cancel");
        PressInDialog("Discard");

        cut.WaitForAssertion(() => cut.HasButton("Edit addresses").ShouldBeTrue());
        _addresses.Saves.ShouldBeEmpty();
        cut.Markup.ShouldContain("Athens"); // the original is still there

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));
        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Control_leaving_with_unsaved_address_changes_does_ask()
    {
        var cut = RenderTab();
        Press(cut, "Edit addresses");
        cut.WaitForAssertion(() => cut.InputByLabel("Billing city"));
        cut.InputByLabel("Billing city").Change("Elsewhere");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
        PressInDialog("Stay");
        Path.ShouldBe("/accounts/5");
    }

    [Fact]
    public void A_failed_load_shows_the_error_with_Retry_and_Retry_recovers()
    {
        _addresses.FailGet = true;
        var cut = RenderTab();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load the addresses."));
        cut.Markup.ShouldNotContain("aria-busy");

        _addresses.FailGet = false;
        cut.ButtonByText("Retry").Click();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("1 Ermou St"));
    }

    [Fact]
    public void An_account_that_cannot_be_seen_shows_the_same_error()
    {
        var cut = RenderTab(accountId: 999);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load the addresses."));
    }
}
