using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Personal;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

/// <summary>The Views menu of a list page and its two dialogs (save current view, manage views).</summary>
public class SavedViewsTests : MudTestContext
{
    private static readonly UserContext Admin = new("admin-1", RoleNames.Admin, TeamId: null);

    private readonly FakeSavedViewService _views = new();
    private readonly NavigationManager _navigation;

    public SavedViewsTests()
    {
        Services.AddSingleton<ISavedViewService>(_views);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/accounts?scope=all&page=3");

        _views.Mine.Add(new SavedViewItem(1, "Big retailers", "industry=2&scope=all", false, "sales-1", "Sam", true));
        _views.Shared.Add(new SavedViewItem(2, "Team pipeline", "scope=team", true, "admin-1", "Alma Admin", false));
    }

    private string Uri => new Uri(_navigation.Uri).PathAndQuery;

    private IRenderedComponent<SavedViewsMenu> RenderMenu(string currentQuery = "scope=all")
    {
        var cut = Render<SavedViewsMenu>(p => p.Add(x => x.ListKey, "accounts").Add(x => x.CurrentQuery, currentQuery));
        cut.WaitForElement("button");
        return cut;
    }

    private void OpenMenu(IRenderedComponent<SavedViewsMenu> cut) => cut.Find("button").Click();

    private void ClickMenuItem(string text)
    {
        PopoverProvider.WaitForAssertion(() =>
            PopoverProvider.FindAll(".mud-menu-item").ShouldContain(i => i.TextContent.Trim() == text));
        PopoverProvider.FindAll(".mud-menu-item").First(i => i.TextContent.Trim() == text).Click();
    }

    private void ClickInDialog(string buttonText)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == buttonText));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == buttonText).Click();
    }

    // ---- The menu

    [Fact]
    public void The_menu_lists_my_views_and_the_ones_shared_with_everyone()
    {
        var cut = RenderMenu();

        OpenMenu(cut);

        PopoverProvider.WaitForAssertion(() =>
        {
            PopoverProvider.Markup.ShouldContain("Big retailers");
            PopoverProvider.Markup.ShouldContain("Team pipeline");
            PopoverProvider.Markup.ShouldContain("My views");
            PopoverProvider.Markup.ShouldContain("Shared with everyone");
        });
    }

    [Fact]
    public void Choosing_a_view_opens_the_same_list_with_its_filters_and_no_page()
    {
        var cut = RenderMenu();
        OpenMenu(cut);

        ClickMenuItem("Big retailers");

        Uri.ShouldBe("/accounts?industry=2&scope=all");
    }

    [Fact]
    public void A_view_without_filters_opens_the_bare_list()
    {
        _views.Mine.Add(new SavedViewItem(3, "Everything", string.Empty, false, "sales-1", "Sam", true));
        var cut = RenderMenu();
        OpenMenu(cut);

        ClickMenuItem("Everything");

        Uri.ShouldBe("/accounts");
    }

    [Fact]
    public void The_view_matching_the_current_filters_is_marked()
    {
        var cut = RenderMenu(currentQuery: "scope=all&industry=2&page=4");
        OpenMenu(cut);

        PopoverProvider.WaitForAssertion(() =>
        {
            var items = PopoverProvider.FindAll(".mud-menu-item");
            items.First(i => i.TextContent.Contains("Big retailers")).InnerHtml.ShouldContain("mud-icon-root");
            items.First(i => i.TextContent.Contains("Team pipeline")).InnerHtml.ShouldNotContain("mud-icon-root");
        });
    }

    [Fact]
    public void A_menu_that_cannot_load_says_so_and_does_not_break_the_page()
    {
        _views.FailWith = new InvalidOperationException("db down");
        var cut = RenderMenu();

        OpenMenu(cut);

        PopoverProvider.WaitForAssertion(() => PopoverProvider.Markup.ShouldContain("Views are unavailable"));
    }

    // ---- Save current view

    [Fact]
    public void Saving_the_current_view_stores_its_filters_without_the_page_under_the_typed_name()
    {
        var cut = RenderMenu(currentQuery: "scope=all&q=acme&page=3");
        OpenMenu(cut);
        ClickMenuItem("Save current view...");

        DialogProvider.WaitForElement("input").Input("Acme search");
        ClickInDialog("Save");

        _views.SaveCalls.ShouldBe([("accounts", "Acme search", "q=acme&scope=all", false)]);
        // the menu shows the new view afterwards
        OpenMenu(cut);
        PopoverProvider.WaitForAssertion(() => PopoverProvider.Markup.ShouldContain("Acme search"));
    }

    [Fact]
    public void A_duplicate_name_is_refused_with_an_offer_to_replace_the_existing_view()
    {
        _views.TakenNames.Add("Big retailers");
        var cut = RenderMenu();
        OpenMenu(cut);
        ClickMenuItem("Save current view...");
        DialogProvider.WaitForElement("input").Input("Big retailers");

        ClickInDialog("Save");

        DialogProvider.WaitForAssertion(() =>
        {
            DialogProvider.Markup.ShouldContain(PersonalRules.DuplicateViewNameMessage);
            DialogProvider.Markup.ShouldContain("Replace the existing view");
        });

        ClickInDialog("Replace the existing view");

        _views.SaveCalls.Last().ShouldBe(("accounts", "Big retailers", "scope=all", true));
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldNotContain("Replace the existing view"));
    }


    // ---- Manage views

    private void OpenManage(IRenderedComponent<SavedViewsMenu> cut)
    {
        OpenMenu(cut);
        ClickMenuItem("Manage views...");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("My views"));
    }

    [Fact]
    public void A_user_can_rename_one_of_their_views()
    {
        var cut = RenderMenu();
        OpenManage(cut);

        DialogProvider.Find("button[aria-label='Rename Big retailers']").Click();
        DialogProvider.WaitForElement("input").Input("Large retailers");
        DialogProvider.Find("button[aria-label='Save name']").Click();

        _views.RenameCalls.ShouldBe([(1, "Large retailers")]);
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Large retailers"));
    }

    [Fact]
    public void Deleting_a_view_asks_first_and_only_deletes_when_confirmed()
    {
        var cut = RenderMenu();
        OpenManage(cut);

        DialogProvider.Find("button[aria-label='Delete Big retailers']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Delete the view"));
        ClickInDialog("Cancel");
        _views.DeleteCalls.ShouldBeEmpty();

        DialogProvider.Find("button[aria-label='Delete Big retailers']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Delete the view"));
        ClickInDialog("Delete");

        DialogProvider.WaitForAssertion(() => _views.DeleteCalls.ShouldBe([1]));
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("You have no saved views yet"));
    }

    [Fact]
    public void A_regular_user_sees_shared_views_read_only_with_a_copy_button_and_no_publish_or_delete()
    {
        var cut = RenderMenu();
        OpenManage(cut);

        DialogProvider.Markup.ShouldContain("Team pipeline");
        DialogProvider.FindAll("button[aria-label='Save a copy of Team pipeline']").Count.ShouldBe(1);
        DialogProvider.FindAll("button[aria-label='Delete Team pipeline']").ShouldBeEmpty();
        DialogProvider.FindAll("button[aria-label='Publish']").ShouldBeEmpty();
        DialogProvider.FindAll("button[aria-label='Rename Team pipeline']").ShouldBeEmpty();
    }

    [Fact]
    public void Saving_a_copy_of_a_shared_view_stores_its_filters_under_a_new_name_for_the_user()
    {
        var cut = RenderMenu();
        OpenManage(cut);

        DialogProvider.Find("button[aria-label='Save a copy of Team pipeline']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("input").Any(i => i.GetAttribute("value") == "Team pipeline (copy)").ShouldBeTrue());
        ClickInDialog("Save");

        _views.SaveCalls.ShouldBe([("accounts", "Team pipeline (copy)", "scope=team", false)]);
    }

    [Fact]
    public async Task An_admin_can_publish_unpublish_and_delete_shared_views()
    {
        // A separate user context is needed: the provider is registered for a regular user in the constructor.
        await using var admin = new AdminContext(_views);
        var cut = admin.RenderMenu();
        admin.OpenManage(cut);

        admin.DialogProvider2.Find("button[aria-label='Publish']").Click();
        admin.DialogProvider2.WaitForAssertion(() => _views.PublishCalls.ShouldBe([(1, true)]));
        admin.DialogProvider2.WaitForAssertion(() => admin.DialogProvider2.FindAll("button[aria-label='Unpublish']").Count.ShouldBe(1));
        admin.DialogProvider2.FindAll("button[aria-label='Delete Team pipeline']").Count.ShouldBe(1);
    }

    private sealed class AdminContext : MudTestContext
    {
        public AdminContext(FakeSavedViewService views)
        {
            Services.AddSingleton<ISavedViewService>(views);
            Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider(Admin));
            StartProviders();
            Services.GetRequiredService<NavigationManager>().NavigateTo("/accounts");
        }

        public IRenderedComponent<MudBlazor.MudDialogProvider> DialogProvider2 => DialogProvider;

        public IRenderedComponent<SavedViewsMenu> RenderMenu()
        {
            var cut = Render<SavedViewsMenu>(p => p.Add(x => x.ListKey, "accounts"));
            cut.WaitForElement("button");
            return cut;
        }

        public void OpenManage(IRenderedComponent<SavedViewsMenu> cut)
        {
            cut.Find("button").Click();
            PopoverProvider.WaitForAssertion(() =>
                PopoverProvider.FindAll(".mud-menu-item").ShouldContain(i => i.TextContent.Trim() == "Manage views..."));
            PopoverProvider.FindAll(".mud-menu-item").First(i => i.TextContent.Trim() == "Manage views...").Click();
            DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("My views"));
        }
    }
}
