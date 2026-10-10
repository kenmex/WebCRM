using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Layout;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Layout;

/// <summary>The app shell: search and "+ New" are for signed-in users only, and New offers the record types that exist.</summary>
public class MainLayoutTests : MudTestContext
{
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();

    public MainLayoutTests()
    {
        Services.AddSingleton<ISearchService>(new FakeSearchService());
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
    }

    // MainLayout renders its own popover, dialog and snackbar providers, so the shared StartProviders() is not used here.
    private IRenderedComponent<MainLayout> RenderLayout() => Render<MainLayout>(p => p.Add(x => x.Body, "<p class=\"page-body\">body</p>"));

    [Fact]
    public void A_signed_in_user_gets_the_search_box_and_the_New_menu_above_the_page()
    {
        AddAuthorization().SetAuthorized("alice");

        var cut = RenderLayout();

        cut.WaitForAssertion(() => cut.FindAll("input[placeholder='Search accounts and contacts']").Count.ShouldBe(1));
        cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "New");
        cut.Find(".page-body").TextContent.ShouldBe("body");
    }

    [Fact]
    public void Nobody_signed_in_means_no_search_and_no_New_menu()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        cut.Find(".page-body").TextContent.ShouldBe("body");
        cut.FindAll("input[placeholder='Search accounts and contacts']").ShouldBeEmpty();
        cut.FindAll("button").ShouldNotContain(b => b.TextContent.Trim() == "New");
    }

    [Fact]
    public void The_New_menu_offers_Account_Contact_and_Lead_with_their_create_links()
    {
        AddAuthorization().SetAuthorized("alice");
        var cut = RenderLayout();
        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "New"));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "New").Click();

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("a.mud-menu-item");
            items.Select(i => i.TextContent.Trim()).ShouldBe(["Account", "Contact", "Lead"]);
            items.Select(i => i.GetAttribute("href")).ShouldBe(["accounts/new", "contacts/new", "leads/new"]);
        });
    }
}
