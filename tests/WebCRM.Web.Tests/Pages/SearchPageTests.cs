using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>P7: results grouped by type, up to 20 per group, "See all in list" with the quick filter set.</summary>
public class SearchPageTests : MudTestContext
{
    private readonly FakeSearchService _search = new();
    private readonly NavigationManager _navigation;

    public SearchPageTests()
    {
        Services.AddSingleton<ISearchService>(_search);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private IRenderedComponent<SearchPage> RenderFor(string? query)
    {
        _navigation.NavigateTo(query is null ? "/search" : $"/search?q={Uri.EscapeDataString(query)}");
        return Render<SearchPage>();
    }

    [Fact]
    public void It_asks_for_twenty_per_type_and_shows_both_groups_with_counts_and_links()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2, contacts: 1, accountTotal: 55));

        var cut = RenderFor("acme");

        cut.WaitForAssertion(() => cut.FindAll("[data-search-hit]").Count.ShouldBe(3));
        _search.Calls.ShouldBe([("acme", 20)]);
        cut.Find("[data-search-group=Account] h2").TextContent.ShouldContain("Accounts");
        cut.Find("[data-search-group=Account] h2").TextContent.ShouldContain("(55)");
        cut.Find("[data-search-group=Contact] h2").TextContent.ShouldContain("(1)");
        cut.FindAll("[data-search-hit]").Select(a => a.GetAttribute("href")).ShouldBe(["accounts/1", "accounts/2", "contacts/101"]);
        cut.Markup.ShouldContain("Retail · Athens");
    }

    [Fact]
    public void See_all_links_to_the_list_with_the_quick_filter_set_and_only_when_there_are_more()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2, contacts: 1, accountTotal: 55));

        var cut = RenderFor("ac me");

        cut.WaitForAssertion(() => cut.FindAll("[data-search-see-all]").Count.ShouldBe(1));
        var link = cut.Find("[data-search-see-all=Account]");
        link.GetAttribute("href").ShouldBe("accounts?scope=all&q=ac%20me");
        link.TextContent.Trim().ShouldBe("See all 55 accounts in the list");
    }

    [Fact]
    public void A_group_with_no_matches_says_so_while_the_other_group_still_shows()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 0, contacts: 2));

        var cut = RenderFor("smith");

        cut.WaitForAssertion(() => cut.FindAll("[data-search-hit]").Count.ShouldBe(2));
        cut.Find("[data-search-group=Account]").TextContent.ShouldContain("No accounts match.");
    }

    [Fact]
    public void Nothing_found_shows_a_friendly_message_with_the_text()
    {
        var cut = RenderFor("zzzz");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No results"));
        cut.Markup.ShouldContain("zzzz");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    public void Without_enough_text_it_asks_for_more_and_does_not_search(string? query)
    {
        var cut = RenderFor(query);

        cut.Markup.ShouldContain("Type at least 2 characters to search.");
        _search.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_load_that_finishes_later_replaces_the_skeleton_with_the_results()
    {
        var gate = new TaskCompletionSource<SearchResults>();
        _search.OnSearch = (_, _) => gate.Task;
        var cut = RenderFor("acme");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("aria-busy"));

        await cut.InvokeAsync(() => gate.SetResult(FakeSearchService.Results("acme", accounts: 1)));

        cut.WaitForAssertion(() => cut.FindAll("[data-search-hit]").Count.ShouldBe(1));
        cut.Markup.ShouldNotContain("aria-busy");
    }

    [Fact]
    public async Task A_search_that_fails_later_shows_the_error_with_Retry_never_the_skeleton_and_Retry_recovers()
    {
        var gates = new List<TaskCompletionSource<SearchResults>>();
        _search.OnSearch = (_, _) =>
        {
            var gate = new TaskCompletionSource<SearchResults>();
            gates.Add(gate);
            return gate.Task;
        };
        var cut = RenderFor("acme");
        cut.WaitForAssertion(() => gates.Count.ShouldBe(1));

        await cut.InvokeAsync(() => gates[0].SetException(new InvalidOperationException("db down")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Search failed."));
        cut.Markup.ShouldNotContain("aria-busy");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retry").Click();
        cut.WaitForAssertion(() => gates.Count.ShouldBe(2));
        await cut.InvokeAsync(() => gates[1].SetResult(FakeSearchService.Results("acme", accounts: 1)));

        cut.WaitForAssertion(() => cut.FindAll("[data-search-hit]").Count.ShouldBe(1));
        cut.Markup.ShouldNotContain("Search failed.");
    }

    [Fact]
    public void The_box_on_the_page_shows_the_query_and_Enter_searches_again()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 1));
        var cut = RenderFor("acme");
        cut.WaitForAssertion(() => cut.Find("input").GetAttribute("value").ShouldBe("acme"));

        cut.Find("input").Input("beta ltd");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        new Uri(_navigation.Uri).PathAndQuery.ShouldBe("/search?q=beta%20ltd");
    }
}
