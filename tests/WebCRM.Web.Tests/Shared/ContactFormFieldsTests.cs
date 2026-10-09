using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Lookups;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class ContactFormFieldsTests : MudTestContext
{
    private readonly FakeContactService _contacts = new();

    public ContactFormFieldsTests()
    {
        var accounts = new FakeAccountService();
        accounts.Picker.AddRange([new AccountPickerItem(5, "Acme Hellas"), new AccountPickerItem(6, "Beta Ltd")]);
        _contacts.AccountOwners[5] = "owner-2";
        _contacts.AccountOwners[6] = "owner-3";
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IAccountService>(accounts);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true),
            new OwnerOption("owner-2", "Olga Owner", true),
            new OwnerOption("owner-3", "Otto Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Mr", true), new LookupOption(2, "Ms", true), new LookupOption(3, "Dr", true)));
        StartProviders();
    }

    private static Task PickAccountAsync(IRenderedComponent<ContactFormHost> cut, int? id) =>
        cut.InvokeAsync(() => cut.FindComponent<AccountPicker>().Instance.ValueChanged.InvokeAsync(id));

    private static Task PickOwnerAsync(IRenderedComponent<ContactFormHost> cut, string id) =>
        cut.InvokeAsync(() => cut.FindComponent<OwnerPicker>().Instance.ValueChanged.InvokeAsync(id));

    [Fact]
    public async Task On_a_new_contact_choosing_an_account_sets_the_owner_to_the_account_owner()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "sales-1"));
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Sam Sales"));

        await PickAccountAsync(cut, 5);

        cut.Instance.Model.AccountId.ShouldBe(5);
        cut.Instance.Model.OwnerId.ShouldBe("owner-2");
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));
    }

    [Fact]
    public async Task Changing_the_account_again_follows_the_new_account_owner_until_the_owner_is_chosen_by_hand()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "sales-1"));

        await PickAccountAsync(cut, 5);
        await PickAccountAsync(cut, 6);
        cut.Instance.Model.OwnerId.ShouldBe("owner-3");

        await PickOwnerAsync(cut, "sales-1"); // the user chooses
        await PickAccountAsync(cut, 5);

        cut.Instance.Model.OwnerId.ShouldBe("sales-1");
    }

    [Fact]
    public async Task On_an_existing_contact_changing_the_account_leaves_the_owner_alone()
    {
        var cut = Render<ContactFormHost>(p => p
            .Add(x => x.IsNew, false)
            .Add(x => x.InitialOwnerId, "sales-1")
            .Add(x => x.InitialAccountId, 6));

        await PickAccountAsync(cut, 5);

        cut.Instance.Model.AccountId.ShouldBe(5);
        cut.Instance.Model.OwnerId.ShouldBe("sales-1");
    }

    [Fact]
    public async Task Clearing_the_account_leaves_the_owner_alone()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "owner-2").Add(x => x.InitialAccountId, 5));

        await PickAccountAsync(cut, null);

        cut.Instance.Model.AccountId.ShouldBeNull();
        cut.Instance.Model.OwnerId.ShouldBe("owner-2");
    }

    [Fact]
    public void A_locked_account_is_shown_but_cannot_be_changed()
    {
        var cut = Render<ContactFormHost>(p => p
            .Add(x => x.LockAccount, true)
            .Add(x => x.InitialAccountId, 5)
            .Add(x => x.InitialOwnerId, "owner-2"));

        cut.WaitForAssertion(() => cut.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
        cut.InputByLabel("Account").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Typing_in_the_fields_updates_the_model()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "sales-1"));

        cut.InputByLabel("First name").Change("Anna");
        cut.InputByLabel("Last name").Change("Smith");
        cut.InputByLabel("Job title").Change("Buyer");
        cut.InputByLabel("Email").Change("anna@example.com");
        cut.InputByLabel("Phone").Change("210 123 4567");
        cut.InputByLabel("Mobile").Change("694 000 1111");

        var model = cut.Instance.Model;
        (model.FirstName, model.LastName, model.JobTitle, model.Email, model.Phone, model.Mobile)
            .ShouldBe(("Anna", "Smith", "Buyer", "anna@example.com", "210 123 4567", "694 000 1111"));
    }
    [Fact]
    public void The_new_fields_update_the_model_and_the_switch_toggles_Do_not_contact()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "sales-1"));

        cut.InputByLabel("Department").Change("Purchasing");
        cut.Find("input[type=checkbox]").Change(true);

        cut.Instance.Model.Department.ShouldBe("Purchasing");
        cut.Instance.Model.DoNotContact.ShouldBeTrue();
        cut.Instance.Context.IsModified().ShouldBeTrue();

        cut.Find("input[type=checkbox]").Change(false);
        cut.Instance.Model.DoNotContact.ShouldBeFalse();
    }

    [Fact]
    public void The_salutation_select_offers_the_lookup_and_shows_the_chosen_value()
    {
        var cut = Render<ContactFormHost>(p => p.Add(x => x.InitialOwnerId, "sales-1"));
        cut.InputByLabel("Salutation").GetAttribute("value").ShouldBeNullOrEmpty();

        cut.InvokeAsync(() => cut.FindComponent<LookupSelect>().Instance.ValueChanged.InvokeAsync(3));

        cut.WaitForAssertion(() => cut.Instance.Model.SalutationId.ShouldBe(3));
        cut.WaitForAssertion(() => cut.InputByLabel("Salutation").GetAttribute("value").ShouldBe("Dr"));
    }
}
