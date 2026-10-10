using WebCRM.Core.Records;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Lookups;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The account detail page driven end to end with a fake service: edit, save, cancel and the
/// "Leave without saving?" guard, which must only fire for changes the user has really not saved.
/// </summary>
public class AccountPageTests : MudTestContext
{
    private const string LeaveDialog = "Leave without saving?";

    private readonly FakeAccountService _accounts = new();
    private readonly FakeContactService _contacts = new();
    private readonly FakeAddressService _addresses = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public AccountPageTests()
    {
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Prospect", true), new LookupOption(2, "Active", true)));
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(new OwnerOption("sales-1", "Sam Sales", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IOpportunityService>(new FakeOpportunityService());
        Services.AddSingleton<IAccountAddressService>(_addresses);
        StartProviders();

        _navigation = Services.GetRequiredService<NavigationManager>();
        _accounts.Accounts[7] = new AccountDetail(
            7, "Acme", null, null, null, 1, "Prospect", null, null, "sales-1", "Sam Sales", true,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);
        _addresses.Addresses[7] = new AccountAddressesEditModel { BillingCity = "Athens" };
    }

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    private IRenderedComponent<AccountPage> RenderNew()
    {
        _navigation.NavigateTo("/accounts/new");
        return Render<AccountPage>();
    }

    private IRenderedComponent<AccountPage> RenderExisting()
    {
        _navigation.NavigateTo("/accounts/7");
        return Render<AccountPage>(p => p.Add(x => x.Id, 7));
    }

    private static void Type(IRenderedComponent<AccountPage> cut, string selector, string text) =>
        cut.WaitForElement(selector).Change(text);

    private static void Press(IRenderedComponent<AccountPage> cut, string buttonText)
    {
        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == buttonText));
        cut.FindAll("button").First(b => b.TextContent.Trim() == buttonText).Click();
    }

    private void PressInDialog(string buttonText)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == buttonText));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == buttonText).Click();
    }

    // ---- The reported bug: after saving a new account the guard fired and the page was stuck ----

    [Fact]
    public void Saving_a_new_account_goes_to_its_page_without_the_leave_prompt()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        Press(cut, "Save");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts/42"));
        _accounts.Saves.Single().Name.ShouldBe("Acme Hellas");
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Control_leaving_a_dirty_new_form_does_ask_and_staying_keeps_the_page()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
        PressInDialog("Stay");
        Path.ShouldBe("/accounts/new");
    }

    [Fact]
    public void Cancelling_a_dirty_new_form_asks_once_then_leaves_without_a_second_prompt()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        Press(cut, "Cancel");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Discard changes?"));
        PressInDialog("Discard");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Cancelling_a_clean_new_form_leaves_without_any_prompt()
    {
        var cut = RenderNew();

        Press(cut, "Cancel");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
        DialogProvider.Markup.ShouldNotContain("Discard changes?");
    }

    [Fact]
    public void Saving_an_existing_account_returns_to_read_mode_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "210 123 4567");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Edit"));
        _accounts.Saves.Single().Phone.ShouldBe("210 123 4567");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Cancelling_an_edit_of_an_existing_account_discards_it_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "999");

        Press(cut, "Cancel");
        PressInDialog("Discard");

        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Edit"));
        _accounts.Saves.ShouldBeEmpty();

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_save_the_server_rejects_keeps_the_form_open_and_dirty_and_shows_the_message()
    {
        _accounts.OnSave = _ => new SaveResult(
            SaveStatus.Invalid,
            FieldErrors: new Dictionary<string, string> { [nameof(AccountEditModel.VatNumber)] = VatNumberRules.InvalidMessage });
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "210");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain(VatNumberRules.InvalidMessage));
        cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Save");

        // Still dirty, so leaving still asks.
        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
    }

    // ---- Tabs (RecordTabs): Overview, Contacts and Opportunities, loaded when first opened ----

    [Fact]
    public void An_existing_account_shows_the_Overview_Contacts_and_Opportunities_tabs_and_loads_contacts_only_when_opened()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.FindAll(".mud-tab").Select(t => t.TextContent.Trim()).ShouldBe(["Overview", "Contacts", "Opportunities"]));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Athens")); // the Overview tab is open
        _addresses.Gets.ShouldBe(1);
        _contacts.Searches.ShouldBeEmpty(); // the Contacts tab has not been opened

        cut.FindAll(".mud-tab").First(t => t.TextContent.Trim() == "Contacts").Click();

        cut.WaitForAssertion(() => _contacts.Searches.Count.ShouldBe(1));
        _contacts.Searches[0].AccountId.ShouldBe(7);
    }

    [Fact]
    public void The_account_page_offers_New_opportunity_with_the_account_filled_in()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.HasButton("New opportunity").ShouldBeTrue());
        cut.ButtonByText("New opportunity").GetAttribute("href").ShouldBe("opportunities/new?accountId=7");
    }

    [Fact]
    public void A_new_account_has_no_tabs_until_it_is_saved()
    {
        var cut = RenderNew();

        cut.WaitForAssertion(() => cut.HasButton("Save").ShouldBeTrue());
        cut.FindAll(".mud-tab").ShouldBeEmpty();
        _addresses.Gets.ShouldBe(0);
    }
    // ---- Legal name, email, tax office ----

    [Fact]
    public void Read_mode_shows_the_legal_name_the_tax_office_and_an_email_link()
    {
        _accounts.Accounts[7] = _accounts.Accounts[7] with
        {
            LegalName = "Acme Hellas A.E.",
            TaxOffice = "ΔΟΥ Κηφισιάς",
            Email = "info@acme.gr",
        };

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Acme Hellas A.E."));
        cut.Markup.ShouldContain("Tax office (ΔΟΥ)");
        cut.Markup.ShouldContain("ΔΟΥ Κηφισιάς");
        cut.Find("a.mud-link[href='mailto:info@acme.gr']").TextContent.ShouldBe("info@acme.gr");
    }

    [Fact]
    public void The_form_has_legal_name_email_and_tax_office_and_saves_them()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Legal name"));

        cut.InputByLabel("Legal name").Change("Acme Hellas A.E.");
        cut.InputByLabel("Email").Change("info@acme.gr");
        cut.InputByLabel("Tax office (ΔΟΥ)").Change("ΔΟΥ Κηφισιάς");
        Press(cut, "Save");

        cut.WaitForAssertion(() => _accounts.Saves.Count.ShouldBe(1));
        var saved = _accounts.Saves.Single();
        saved.LegalName.ShouldBe("Acme Hellas A.E.");
        saved.Email.ShouldBe("info@acme.gr");
        saved.TaxOffice.ShouldBe("ΔΟΥ Κηφισιάς");
    }

    [Fact]
    public void An_invalid_account_email_is_rejected_by_the_form_before_the_server_is_called()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Email"));
        cut.InputByLabel("Email").Change("not an email");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Enter a valid email address, e.g. name@example.com."));
        _accounts.Saves.ShouldBeEmpty();
    }

    [Fact]
    public void Opening_the_page_records_a_recent_view_but_the_new_form_does_not()
    {
        RenderNew();
        _recents.Recorded.ShouldBeEmpty();

        var cut = RenderExisting();
        cut.WaitForAssertion(() => _recents.Recorded.ShouldBe([(SearchEntity.Account, 7)]));
    }

    [Fact]
    public void A_failing_recent_view_write_does_not_stop_the_page_from_showing()
    {
        _recents.FailWith = new InvalidOperationException("db down");

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldNotBeNullOrWhiteSpace());
    }

    [Fact]
    public void The_star_shows_the_saved_state_and_toggles_both_ways()
    {
        _favourites.Starred.Add((SearchEntity.Account, 7));
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']").GetAttribute("aria-pressed").ShouldBe("true"));

        cut.Find("button[aria-label='Remove from favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']"));
        cut.Find("button[aria-label='Add to favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']"));

        _favourites.SetCalls.ShouldBe([(SearchEntity.Account, 7, false), (SearchEntity.Account, 7, true)]);
    }

    [Fact]
    public void The_star_goes_back_when_saving_it_fails()
    {
        var cut = RenderExisting();
        cut.WaitForElement("button[aria-label='Add to favourites']");
        _favourites.FailWith = new InvalidOperationException("db down");

        cut.Find("button[aria-label='Add to favourites']").Click();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']").GetAttribute("aria-pressed").ShouldBe("false"));
    }

    [Fact]
    public void There_is_no_star_on_the_new_form()
    {
        var cut = RenderNew();

        cut.WaitForElement("h1");
        cut.FindAll("button[aria-label$='favourites']").ShouldBeEmpty();
    }
}
