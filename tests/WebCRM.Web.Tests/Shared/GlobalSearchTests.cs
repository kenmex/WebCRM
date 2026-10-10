using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class GlobalSearchTests : MudTestContext
{
    private readonly FakeSearchService _search = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public GlobalSearchTests()
    {
        Services.AddSingleton<ISearchService>(_search);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/");
    }

    private string Uri => new Uri(_navigation.Uri).PathAndQuery;

    private IRenderedComponent<GlobalSearch> RenderSearch() => Render<GlobalSearch>();

    private static AngleSharp.Dom.IElement Box(IRenderedComponent<GlobalSearch> cut)
    {
        cut.WaitForAssertion(() => cut.FindAll("input").Count.ShouldBe(1));
        return cut.Find("input");
    }

    private void TypeAndWait(IRenderedComponent<GlobalSearch> cut, string text, int calls)
    {
        Box(cut).Input(text);
        cut.WaitForAssertion(() => _search.Calls.Count.ShouldBe(calls), TimeSpan.FromSeconds(3));
    }

    private string Popover => PopoverProvider.Markup;

    private IReadOnlyList<string> HitLinks() =>
        [.. PopoverProvider.FindAll("[data-search-hit]").Select(e => e.GetAttribute("data-search-hit")!)];

    // ---- Searching ----

    [Fact]
    public void It_waits_for_two_characters_then_searches_after_the_debounce_for_five_per_type()
    {
        var cut = RenderSearch();

        Box(cut).Input("a");
        Thread.Sleep(450);
        _search.Calls.ShouldBeEmpty();

        TypeAndWait(cut, "ac", 1);

        _search.Calls.ShouldBe([("ac", 5)]);
    }

    [Fact]
    public void Results_are_grouped_with_subtitles_and_a_see_all_link_when_there_are_more()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2, contacts: 1, accountTotal: 12));
        var cut = RenderSearch();

        TypeAndWait(cut, "ac", 1);

        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));
        Popover.ShouldContain("Accounts");
        Popover.ShouldContain("Retail · Athens");
        Popover.ShouldContain("Contacts");
        Popover.ShouldContain("Acme · Buyer");
        Popover.ShouldContain("See all 12 accounts in the list");
        Popover.ShouldNotContain("See all 1 contacts"); // everything is shown, so no link
        Popover.ShouldContain("Press Enter to see all results");
        HitLinks().ShouldBe(["accounts/1", "accounts/2", "contacts/101"]);
    }

    [Fact]
    public void No_matches_say_so()
    {
        var cut = RenderSearch();

        TypeAndWait(cut, "zz", 1);

        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("No results for"));
    }

    [Fact]
    public async Task A_slow_older_search_never_overwrites_the_newer_results()
    {
        var gates = new List<TaskCompletionSource<SearchResults>>();
        _search.OnSearch = (text, _) =>
        {
            var gate = new TaskCompletionSource<SearchResults>();
            gates.Add(gate);
            return gate.Task;
        };
        var cut = RenderSearch();

        TypeAndWait(cut, "ab", 1);
        TypeAndWait(cut, "abc", 2);
        await cut.InvokeAsync(() => gates[1].SetResult(FakeSearchService.Results("abc", accounts: 1)));
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));

        // The first search finally answers, with different data.
        await cut.InvokeAsync(() => gates[0].SetResult(new SearchResults("ab", [new SearchGroup(SearchEntity.Account, [new SearchHit(SearchEntity.Account, 9, "Stale result", null)], 1)])));

        Popover.ShouldNotContain("Stale result");
        Popover.ShouldContain("Account 1");
    }

    [Fact]
    public async Task A_failed_search_shows_an_error_never_an_endless_searching_message()
    {
        var gate = new TaskCompletionSource<SearchResults>();
        _search.OnSearch = (_, _) => gate.Task;
        var cut = RenderSearch();
        TypeAndWait(cut, "ab", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Searching..."));

        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("db down")));

        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Search failed. Please try again."));
        Popover.ShouldNotContain("Searching...");
    }

    // ---- Keyboard ----

    [Fact]
    public void Enter_with_nothing_highlighted_opens_the_full_results_page()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac me", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Uri.ShouldBe("/search?q=ac%20me");
    }

    [Fact]
    public void Arrow_keys_highlight_hits_and_Enter_opens_the_highlighted_one()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2, contacts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        PopoverProvider.WaitForAssertion(() =>
            PopoverProvider.Find("[aria-selected=true]").GetAttribute("data-search-hit").ShouldBe("accounts/2"));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Uri.ShouldBe("/accounts/2");
    }

    [Fact]
    public void Arrow_up_from_the_start_wraps_to_the_last_hit_and_arrow_down_wraps_back()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1, contacts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        PopoverProvider.WaitForAssertion(() =>
            PopoverProvider.Find("[aria-selected=true]").GetAttribute("data-search-hit").ShouldBe("contacts/101"));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        PopoverProvider.WaitForAssertion(() =>
            PopoverProvider.Find("[aria-selected=true]").GetAttribute("data-search-hit").ShouldBe("accounts/1"));
    }

    [Fact]
    public void Escape_closes_the_results_without_losing_the_text()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").Count.ShouldBe(1));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "Escape" });

        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").ShouldBeEmpty());
        Box(cut).GetAttribute("value").ShouldBe("ac");
    }

    [Fact]
    public void Leaving_the_box_closes_the_results()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").Count.ShouldBe(1));

        Box(cut).Blur();

        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").ShouldBeEmpty());
    }

    // ---- Mouse ----

    [Fact]
    public void Pressing_a_hit_opens_the_record_and_clears_the_box()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1, contacts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Contact 1"));

        // Mouse-down, not click: the box loses focus (and would close the list) before a click lands.
        PopoverProvider.Find("[data-search-hit='contacts/101']").MouseDown();

        Uri.ShouldBe("/contacts/101");
        cut.WaitForAssertion(() => Box(cut).GetAttribute("value").ShouldBeNullOrEmpty());
    }

    [Fact]
    public void See_all_opens_that_list_across_everything_the_user_can_see_with_the_quick_filter_set()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1, accountTotal: 40, contacts: 1, contactTotal: 7));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac me", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("See all 40 accounts"));

        PopoverProvider.Find("[data-search-see-all=Account]").MouseDown();

        Uri.ShouldBe("/accounts?scope=all&q=ac%20me");
    }

    [Fact]
    public void Navigating_elsewhere_closes_the_results()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderSearch();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").Count.ShouldBe(1));

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-popover-open").ShouldBeEmpty());
    }

    // ---- Phone ----

    [Fact]
    public void On_a_phone_the_box_is_an_icon_that_opens_a_full_width_bar_and_closes_again()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var cut = RenderSearch();

        cut.WaitForAssertion(() => cut.FindAll("button[aria-label=Search]").Count.ShouldBe(1));
        cut.FindAll("input").ShouldBeEmpty();

        cut.Find("button[aria-label=Search]").Click();
        cut.WaitForAssertion(() => cut.FindAll("input").Count.ShouldBe(1));
        cut.Find("button[aria-label='Close search']").Click();

        cut.WaitForAssertion(() => cut.FindAll("input").ShouldBeEmpty());
    }

    [Fact]
    public void On_a_phone_searching_works_the_same_and_opening_a_hit_closes_the_bar()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderSearch();
        cut.WaitForAssertion(() => cut.FindAll("button[aria-label=Search]").Count.ShouldBe(1));
        cut.Find("button[aria-label=Search]").Click();
        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Account 1"));

        PopoverProvider.Find("[data-search-hit='accounts/1']").MouseDown();

        Uri.ShouldBe("/accounts/1");
        cut.WaitForAssertion(() => cut.FindAll("input").ShouldBeEmpty());
    }

    // ---- Favourites and recently viewed (nothing typed)

    private static SearchHit Hit(SearchEntity type, int id, string title) => new(type, id, title, null);

    private void Focus(IRenderedComponent<GlobalSearch> cut)
    {
        Box(cut);
        cut.Find("[role=search]").TriggerEvent("onfocusin", new FocusEventArgs());
    }

    [Fact]
    public void Focusing_the_empty_box_lists_favourites_and_recently_viewed_without_searching()
    {
        _favourites.Listed.Add(Hit(SearchEntity.Account, 3, "Starred Co"));
        _recents.Listed.Add(Hit(SearchEntity.Contact, 9, "Recent Person"));
        var cut = RenderSearch();

        Focus(cut);

        PopoverProvider.WaitForAssertion(() =>
        {
            Popover.ShouldContain("Favourites");
            Popover.ShouldContain("Starred Co");
            Popover.ShouldContain("Recently viewed");
            Popover.ShouldContain("Recent Person");
        });
        HitLinks().ShouldBe(["accounts/3", "contacts/9"]);
        _search.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_box_with_nothing_to_show_explains_how_to_get_entries()
    {
        var cut = RenderSearch();

        Focus(cut);

        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Star records to find them here"));
    }

    [Fact]
    public void Arrow_keys_and_Enter_open_an_entry_of_the_idle_list()
    {
        _favourites.Listed.Add(Hit(SearchEntity.Account, 3, "Starred Co"));
        _recents.Listed.Add(Hit(SearchEntity.Contact, 9, "Recent Person"));
        var cut = RenderSearch();
        Focus(cut);
        PopoverProvider.WaitForAssertion(() => HitLinks().Count.ShouldBe(2));

        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Box(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Box(cut).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Uri.ShouldBe("/contacts/9");
    }

    [Fact]
    public void Typing_replaces_the_idle_list_with_results_and_clearing_brings_it_back()
    {
        _favourites.Listed.Add(Hit(SearchEntity.Account, 3, "Starred Co"));
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderSearch();
        Focus(cut);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Starred Co"));

        TypeAndWait(cut, "ac", 1);
        PopoverProvider.WaitForAssertion(() =>
        {
            Popover.ShouldContain("Account 1");
            Popover.ShouldNotContain("Favourites");
        });

        Box(cut).Input(string.Empty);
        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Starred Co"));
    }

    [Fact]
    public void A_failing_idle_list_shows_an_empty_panel_not_an_error()
    {
        _favourites.FailWith = new InvalidOperationException("db down");
        var cut = RenderSearch();

        Focus(cut);

        PopoverProvider.WaitForAssertion(() => Popover.ShouldContain("Star records to find them here"));
        Popover.ShouldNotContain("failed");
    }
}
