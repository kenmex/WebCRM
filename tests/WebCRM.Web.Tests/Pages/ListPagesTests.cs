using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Lookups;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Components.Pages.Contacts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The Accounts and Contacts list pages: the URL becomes the query sent to the service, and the filter controls show
/// what the URL says. (A string parameter written without "@" in Razor is a literal, which once made the Owner and
/// City filters show their own source code; these tests would have caught it.)
/// </summary>
public class ListPagesTests : MudTestContext
{
    private readonly FakeContactService _contacts = new();
    private readonly FakeAccountService _accounts = new();
    private readonly FakeSavedViewService _savedViews = new();
    private readonly NavigationManager _navigation;

    public ListPagesTests()
    {
        _accounts.Picker.Add(new AccountPickerItem(5, "Acme Hellas"));
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Prospect", true), new LookupOption(2, "Active", true)));
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<ISavedViewService>(_savedViews);
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private static ContactListItem Row(int id, string name, string? phone = null, string? mobile = null, string? email = null) =>
        new(id, name, 5, "Acme Hellas", "Buyer", email, phone, mobile, "owner-2", "Olga Owner", true, null);

    // ---- Saved views ----

    [Theory]
    [InlineData("/accounts?scope=all&q=acme&page=3&sort=name", "accounts", "q=acme&scope=all&sort=name")]
    [InlineData("/contacts?scope=all&q=ann&dnc=1&page=2", "contacts", "dnc=1&q=ann&scope=all")]
    public void The_Views_menu_saves_the_filters_in_the_address_bar_for_its_own_list(string uri, string listKey, string expectedQuery)
    {
        _navigation.NavigateTo(uri);
        var cut = listKey == "accounts" ? (IRenderedComponent<IComponent>)Render<AccountsPage>() : Render<ContactsPage>();

        cut.WaitForAssertion(() => cut.Find("[data-saved-views]").GetAttribute("data-saved-views").ShouldBe(listKey));
        cut.Find("[data-saved-views] button").Click();
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-menu-item").ShouldNotBeEmpty());
        PopoverProvider.FindAll(".mud-menu-item").First(i => i.TextContent.Trim() == "Save current view...").Click();
        DialogProvider.WaitForElement("input").Input("My view");
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == "Save").Click();

        _savedViews.SaveCalls.ShouldBe([(listKey, "My view", expectedQuery, false)]);
    }

    // ---- Contacts ----

    [Fact]
    public void The_contacts_url_becomes_the_query_and_the_filters_show_their_values()
    {
        _navigation.NavigateTo("/contacts?scope=all&q=ann&account=5&owner=owner-2&email=1&dnc=1&sort=account&desc=1&page=2");

        var cut = Render<ContactsPage>();

        cut.WaitForAssertion(() => _contacts.Searches.Count.ShouldBe(1));
        var query = _contacts.Searches[0];
        query.ShouldBe(new ContactQuery(
            ListScope.All, Search: "ann", AccountId: 5, OwnerId: "owner-2", HasEmail: true, DoNotContact: true,
            Sort: ContactSort.Account, Descending: true, Page: 2));

        // The controls show the filter values, not the code that produced them.
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));
        cut.InputByLabel("Email").GetAttribute("value").ShouldBe("Has email");
        cut.InputByLabel("Do not contact").GetAttribute("value").ShouldBe("Do not contact");
        cut.WaitForAssertion(() => cut.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("", null)]
    public void The_email_filter_maps_to_has_email(string value, bool? expected)
    {
        _navigation.NavigateTo("/contacts?scope=all" + (value.Length > 0 ? $"&email={value}" : string.Empty));

        Render<ContactsPage>().WaitForAssertion(() => _contacts.Searches.Single().HasEmail.ShouldBe(expected));
    }

    [Fact]
    public async Task A_manager_starts_on_my_team_a_sales_user_on_mine_and_an_admin_on_all()
    {
        foreach (var (role, expected) in new[]
        {
            (RoleNames.Manager, ListScope.Team), (RoleNames.Sales, ListScope.Mine), (RoleNames.Admin, ListScope.All),
        })
        {
            var contacts = new FakeContactService();
            await using var context = new ListPagesContext(contacts, new UserContext("u", role, 1));
            context.Navigate("/contacts");

            var cut = context.Render<ContactsPage>();

            cut.WaitForAssertion(() => contacts.Searches.Single().Scope.ShouldBe(expected), TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public void Rows_link_the_email_and_the_phone_falling_back_to_the_mobile()
    {
        _contacts.OnSearch = _ => new PagedResult<ContactListItem>(
            [
                Row(1, "Anna Smith", phone: "+30 210 123 4567", email: "anna@example.com"),
                Row(2, "Bob Jones", mobile: "694 000 1111"),
            ],
            2);
        _navigation.NavigateTo("/contacts?scope=all");

        var cut = Render<ContactsPage>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(2));
        cut.Find("a[href='mailto:anna@example.com']").TextContent.ShouldBe("anna@example.com");
        cut.Find("a[href='tel:+302101234567']").TextContent.ShouldBe("+30 210 123 4567");
        cut.Find("a[href='tel:6940001111']").TextContent.ShouldBe("694 000 1111");
        cut.FindAll("a[href^='mailto:']").Count.ShouldBe(1); // Bob has no email, so no empty link
    }


    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", null)]
    public void The_do_not_contact_filter_maps_to_the_query(string value, bool? expected)
    {
        _navigation.NavigateTo("/contacts?scope=all" + (value.Length > 0 ? $"&dnc={value}" : string.Empty));

        Render<ContactsPage>().WaitForAssertion(() => _contacts.Searches.Single().DoNotContact.ShouldBe(expected));
    }

    [Fact]
    public void A_do_not_contact_contact_shows_the_warning_chip_in_its_row()
    {
        _contacts.OnSearch = _ => new PagedResult<ContactListItem>(
            [Row(1, "Anna Smith") with { DoNotContact = true }, Row(2, "Bob Jones")],
            2);
        _navigation.NavigateTo("/contacts?scope=all");

        var cut = Render<ContactsPage>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(2));
        var rows = cut.FindAll("tbody tr");
        rows[0].QuerySelectorAll("[data-do-not-contact]").Length.ShouldBe(1);
        rows[1].QuerySelectorAll("[data-do-not-contact]").Length.ShouldBe(0);
    }

    [Fact]
    public async Task On_a_phone_the_card_shows_the_warning_chip_too()
    {
        Viewport.Breakpoint = MudBlazor.Breakpoint.Xs;
        _contacts.OnSearch = _ => new PagedResult<ContactListItem>([Row(1, "Anna Smith") with { DoNotContact = true }], 1);
        _navigation.NavigateTo("/contacts?scope=all");

        var cut = Render<ContactsPage>();

        cut.WaitForAssertion(() => cut.FindAll("a.crm-card-link [data-do-not-contact]").Count.ShouldBe(1));
        await Task.CompletedTask;
    }

    // ---- Accounts ----

    [Fact]
    public void The_accounts_url_becomes_the_query_and_the_owner_and_city_filters_show_their_values()
    {
        _navigation.NavigateTo("/accounts?scope=all&status=2&owner=owner-2&city=Athens");

        var cut = Render<AccountsPage>();

        cut.WaitForAssertion(() => _accounts.Searches.Count.ShouldBe(1));
        _accounts.Searches[0].ShouldBe(new AccountQuery(
            ListScope.All, StatusId: 2, OwnerId: "owner-2", City: "Athens"));

        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));
        cut.InputByLabel("Status").GetAttribute("value").ShouldBe("Active");
        cut.InputByLabel("City").GetAttribute("value").ShouldBe("Athens");
    }

    /// <summary>A separate bUnit context, because the user (and so the services) differ per case.</summary>
    private sealed class ListPagesContext : MudTestContext
    {
        public ListPagesContext(FakeContactService contacts, UserContext user)
        {
            Services.AddSingleton<IContactService>(contacts);
            Services.AddSingleton<IAccountService>(new FakeAccountService());
            Services.AddSingleton<IOwnerService>(new FakeOwnerService(new OwnerOption("u", "User", true)));
            Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider(user));
            Services.AddSingleton<ISavedViewService>(new FakeSavedViewService());
            StartProviders();
        }

        public void Navigate(string uri) => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }
}
