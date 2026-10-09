using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>The Contacts tab on the account page and the Add contact dialog (US2).</summary>
public class AccountContactsTabTests : MudTestContext
{
    private readonly FakeContactService _contacts = new();
    private readonly FakeAccountService _accounts = new();

    public AccountContactsTabTests()
    {
        _accounts.Picker.Add(new AccountPickerItem(5, "Acme Hellas"));
        _contacts.AccountOwners[5] = "owner-2";
        _contacts.OnSearch = _ => new PagedResult<ContactListItem>(
            [new ContactListItem(1, "Anna Smith", 5, "Acme Hellas", "Buyer", "anna@example.com", "210 123", null, "owner-2", "Olga Owner", true, null)],
            1);
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
    }

    private IRenderedComponent<AccountContactsTab> RenderTab() => Render<AccountContactsTab>(p => p.Add(x => x.AccountId, 5));

    private static void OpenDialog(IRenderedComponent<AccountContactsTab> cut)
    {
        cut.WaitForAssertion(() => cut.HasButton("Add contact").ShouldBeTrue());
        cut.ButtonByText("Add contact").Click();
    }

    private void InDialog(Action<IRenderedComponent<MudBlazor.MudDialogProvider>> act) =>
        DialogProvider.WaitForAssertion(() => act(DialogProvider));

    [Fact]
    public void It_lists_the_accounts_contacts_with_links_asking_only_for_that_account()
    {
        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.FindAll(".mud-table-body tr").Count.ShouldBe(1));
        cut.Markup.ShouldContain("Anna Smith");
        cut.Find("a[href='mailto:anna@example.com']").TextContent.ShouldBe("anna@example.com");
        cut.Find("a[href='tel:210123']").TextContent.ShouldBe("210 123");
        _contacts.Searches.Single().ShouldBe(new ContactQuery(ListScope.All, AccountId: 5));
    }

    [Fact]
    public void An_account_without_contacts_says_so_and_still_offers_Add_contact()
    {
        _contacts.OnSearch = _ => PagedResult<ContactListItem>.Empty;

        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("This account has no contacts yet."));
        cut.HasButton("Add contact").ShouldBeTrue();
    }

    [Fact]
    public void Add_contact_opens_a_dialog_with_the_account_filled_in_locked_and_its_owner_as_default()
    {
        var cut = RenderTab();
        OpenDialog(cut);

        InDialog(d => d.Markup.ShouldContain("Add contact"));
        InDialog(d => d.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
        DialogProvider.InputByLabel("Account").HasAttribute("disabled").ShouldBeTrue();
        InDialog(d => d.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));
        _contacts.NewCalls.ShouldBe([5]);
    }

    [Fact]
    public void Saving_the_dialog_adds_the_contact_to_this_account_closes_it_and_reloads_the_list()
    {
        var cut = RenderTab();
        cut.WaitForAssertion(() => _contacts.Searches.Count.ShouldBe(1));
        OpenDialog(cut);
        InDialog(d => d.InputByLabel("Last name"));
        DialogProvider.InputByLabel("First name").Change("Bea");
        DialogProvider.InputByLabel("Last name").Change("Jones");
        InDialog(d => d.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));

        DialogProvider.ButtonByText("Save").Click();

        cut.WaitForAssertion(() => _contacts.Saves.Count.ShouldBe(1));
        var saved = _contacts.Saves.Single().Model;
        saved.AccountId.ShouldBe(5);
        saved.LastName.ShouldBe("Jones");
        saved.OwnerId.ShouldBe("owner-2");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldNotContain("Add contact"));
        cut.WaitForAssertion(() => _contacts.Searches.Count.ShouldBe(2)); // the list reloaded
    }

    [Fact]
    public void Saving_without_a_last_name_shows_the_error_and_does_not_call_the_server()
    {
        var cut = RenderTab();
        OpenDialog(cut);
        InDialog(d => d.InputByLabel("Last name"));

        DialogProvider.ButtonByText("Save").Click();

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Last name is required."));
        _contacts.Saves.ShouldBeEmpty();
        DialogProvider.Markup.ShouldContain("Add contact"); // still open
    }

    [Fact]
    public void A_duplicate_email_warning_in_the_dialog_offers_Save_anyway()
    {
        var first = true;
        _contacts.OnSave = _ =>
        {
            var warn = first;
            first = false;
            return warn
                ? new SaveResult(SaveStatus.Warning, Warnings: ["Anna Twin (Bob Co)"], WarningMessage: "A contact with this email already exists")
                : new SaveResult(SaveStatus.Saved, Id: 9);
        };
        var cut = RenderTab();
        OpenDialog(cut);
        InDialog(d => d.InputByLabel("Last name"));
        DialogProvider.InputByLabel("Last name").Change("Jones");
        DialogProvider.InputByLabel("Email").Change("twin@example.com");

        DialogProvider.ButtonByText("Save").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("A contact with this email already exists: Anna Twin (Bob Co)."));
        DialogProvider.Markup.ShouldContain("Add contact"); // still open

        DialogProvider.ButtonByText("Save anyway").Click();

        cut.WaitForAssertion(() => _contacts.Saves.Count.ShouldBe(2));
        _contacts.Saves[1].Options.AcceptWarnings.ShouldBeTrue();
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldNotContain("Add contact"));
    }

    [Fact]
    public void Cancel_closes_the_dialog_without_saving_or_reloading()
    {
        var cut = RenderTab();
        cut.WaitForAssertion(() => _contacts.Searches.Count.ShouldBe(1));
        OpenDialog(cut);
        InDialog(d => d.InputByLabel("Last name"));

        DialogProvider.ButtonByText("Cancel").Click();

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldNotContain("Add contact"));
        _contacts.Saves.ShouldBeEmpty();
        _contacts.Searches.Count.ShouldBe(1);
    }
}
