using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Querying;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;
using static WebCRM.Web.Tests.Shared.CrmListPageHost;

namespace WebCRM.Web.Tests.Shared;

public class CrmListPageTests : MudTestContext
{
    private readonly List<ListState> _requests = [];
    private readonly NavigationManager _navigation;
    private Func<ListState, PagedResult<Row>> _data = _ => PagedResult<Row>.Empty;

    public CrmListPageTests()
    {
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/accounts");
        StartProviders();
    }

    private Task<PagedResult<Row>> Load(ListState state, CancellationToken cancellationToken)
    {
        _requests.Add(state.Clone());
        return Task.FromResult(_data(state));
    }

    private IRenderedComponent<CrmListPageHost> RenderList() =>
        Render<CrmListPageHost>(p => p.Add(x => x.Load, Load));

    private static PagedResult<Row> Rows(int count, int total) =>
        new([.. Enumerable.Range(1, count).Select(i => new Row(i, $"Account {i}"))], total);

    private string Path => new Uri(_navigation.Uri).PathAndQuery;

    // ---- Loading ----

    [Fact]
    public void Loads_once_and_shows_the_rows_and_the_range()
    {
        _data = _ => Rows(2, 2);

        var cut = RenderList();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Select(c => c.TextContent).ShouldBe(["Account 1", "Account 2"]));
        cut.Markup.ShouldContain("1–2 of 2");
        _requests.Count.ShouldBe(1);
        _requests[0].Scope.ShouldBe(ListScope.Mine);
        _requests[0].Page.ShouldBe(1);
    }

    [Fact]
    public void Reads_page_sort_and_filters_from_the_url_on_first_load()
    {
        _navigation.NavigateTo("/accounts?scope=all&q=acme&page=2&sort=name&desc=1&status=2");
        _data = _ => Rows(2, 30);

        RenderList().WaitForAssertion(() => _requests.Count.ShouldBe(1));

        var request = _requests[0];
        request.Scope.ShouldBe(ListScope.All);
        request.Search.ShouldBe("acme");
        request.Page.ShouldBe(2);
        request.Sort.ShouldBe("name");
        request.Descending.ShouldBeTrue();
        request.GetInt("status").ShouldBe(2);
    }

    [Fact]
    public void Shows_an_error_with_Retry_when_loading_fails_and_recovers()
    {
        var fail = true;
        var cut = Render<CrmListPageHost>(p => p.Add(x => x.Load, (state, ct) =>
            fail ? throw new InvalidOperationException("db down") : Task.FromResult(Rows(1, 1))));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load accounts"));

        fail = false;
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retry").Click();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(1));
    }

    // ---- Asynchronous loads: the real database never answers synchronously ----

    [Fact]
    public async Task A_load_that_finishes_later_replaces_the_skeleton_with_the_rows()
    {
        var gate = new TaskCompletionSource<PagedResult<Row>>();
        var cut = Render<CrmListPageHost>(p => p.Add(x => x.Load, (_, _) => gate.Task));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("aria-busy"));

        await cut.InvokeAsync(() => gate.SetResult(Rows(2, 2)));

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(2));
        cut.Markup.ShouldNotContain("aria-busy");
    }

    [Fact]
    public async Task A_load_that_fails_later_shows_the_error_state_with_Retry_never_the_skeleton()
    {
        var gate = new TaskCompletionSource<PagedResult<Row>>();
        var cut = Render<CrmListPageHost>(p => p.Add(x => x.Load, (_, _) => gate.Task));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("aria-busy"));

        await cut.InvokeAsync(() => gate.SetException(new InvalidOperationException("could not be translated")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load accounts"));
        cut.Markup.ShouldNotContain("aria-busy");
        cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Retry");
    }

    [Fact]
    public async Task Retry_after_an_asynchronous_failure_loads_the_rows()
    {
        var attempts = new List<TaskCompletionSource<PagedResult<Row>>>();
        var cut = Render<CrmListPageHost>(p => p.Add(x => x.Load, (_, _) =>
        {
            var gate = new TaskCompletionSource<PagedResult<Row>>();
            attempts.Add(gate);
            return gate.Task;
        }));
        cut.WaitForAssertion(() => attempts.Count.ShouldBe(1));
        await cut.InvokeAsync(() => attempts[0].SetException(new InvalidOperationException("boom")));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load accounts"));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retry").Click();
        cut.WaitForAssertion(() => attempts.Count.ShouldBe(2));
        await cut.InvokeAsync(() => attempts[1].SetResult(Rows(1, 1)));

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(1));
        cut.Markup.ShouldNotContain("Could not load");
    }

    [Fact]
    public async Task An_error_while_loading_more_on_a_phone_shows_the_error_state_too()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var calls = 0;
        var cut = Render<CrmListPageHost>(p => p.Add(x => x.Load, async (_, _) =>
        {
            await Task.Yield();
            return ++calls == 1 ? Rows(2, 5) : throw new InvalidOperationException("boom");
        }));
        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(2));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Load more")).Click();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load more accounts"));
        cut.Markup.ShouldNotContain("aria-busy");
        await Task.CompletedTask;
    }

    [Fact]
    public void If_the_browser_size_cannot_be_read_the_list_still_loads_as_a_grid()
    {
        Viewport.Fail = true;
        _data = _ => Rows(2, 2);

        var cut = RenderList();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(2));
    }

    // ---- Empty states ----

    [Fact]
    public void Shows_an_empty_state_with_a_New_button_when_there_is_nothing_at_all()
    {
        var cut = RenderList();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No accounts here yet"));
        cut.FindAll("a").ShouldContain(a => a.GetAttribute("href") == "accounts/new");
    }

    [Fact]
    public void Shows_no_results_with_Clear_filters_when_filters_hide_everything()
    {
        _navigation.NavigateTo("/accounts?status=2&q=zzz");
        var cut = RenderList();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No results"));

        cut.FindAll("button").Last(b => b.TextContent.Trim() == "Clear filters").Click();

        cut.WaitForAssertion(() => _requests.Count.ShouldBe(2));
        Path.ShouldBe("/accounts");
        _requests[1].HasFilters.ShouldBeFalse();
    }

    // ---- State lives in the URL ----

    [Fact]
    public void Choosing_a_scope_chip_updates_the_url_and_reloads()
    {
        _data = _ => Rows(1, 1);
        var cut = RenderList();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(1));

        cut.FindAll(".mud-chip").Single(c => c.TextContent.Trim() == "All I can see").Click();

        cut.WaitForAssertion(() => _requests.Count.ShouldBe(2));
        Path.ShouldBe("/accounts?scope=all");
        _requests[1].Scope.ShouldBe(ListScope.All);
    }

    [Fact]
    public void A_typed_filter_updates_the_url_and_resets_to_page_one()
    {
        _navigation.NavigateTo("/accounts?page=3");
        _data = _ => Rows(1, 100);
        var cut = RenderList();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(1));

        cut.Find(".set-status").Click();

        cut.WaitForAssertion(() => _requests.Count.ShouldBe(2));
        Path.ShouldBe("/accounts?status=2");
        _requests[1].Page.ShouldBe(1);
        _requests[1].GetInt("status").ShouldBe(2);
    }

    [Fact]
    public void Clicking_a_sort_header_sorts_and_clicking_again_reverses()
    {
        _data = _ => Rows(1, 1);
        var cut = RenderList();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(1));

        cut.Find(".crm-sort-header").Click();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(2));
        Path.ShouldBe("/accounts?sort=name");
        _requests[1].Descending.ShouldBeFalse();

        cut.Find(".crm-sort-header").Click();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(3));
        Path.ShouldBe("/accounts?sort=name&desc=1");
        _requests[2].Descending.ShouldBeTrue();
    }

    [Fact]
    public void Browser_back_and_forward_restore_the_view()
    {
        _data = _ => Rows(1, 100);
        var cut = RenderList();
        cut.WaitForAssertion(() => _requests.Count.ShouldBe(1));

        // The URL changes without a click on the page: this is what Back does.
        _navigation.NavigateTo("/accounts?page=2");

        cut.WaitForAssertion(() => _requests.Count.ShouldBe(2));
        _requests[1].Page.ShouldBe(2);
    }

    [Fact]
    public void Paging_is_shown_only_when_there_is_more_than_one_page()
    {
        _data = _ => Rows(25, 25);
        var one = RenderList();
        one.WaitForAssertion(() => one.FindAll(".cell-name").Count.ShouldBe(25));
        one.FindAll(".mud-pagination").ShouldBeEmpty();

        _data = _ => Rows(25, 60);
        var many = RenderList();
        many.WaitForAssertion(() => many.FindAll(".mud-pagination").Count.ShouldBe(1));
        many.Markup.ShouldContain("1–25 of 60");
    }

    // ---- Phone ----

    [Fact]
    public void On_a_phone_it_shows_cards_instead_of_the_grid_and_loads_more()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        _data = state => state.Page == 1 ? Rows(2, 3) : new PagedResult<Row>([new Row(3, "Account 3")], 3);
        var cut = RenderList();

        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(2));
        cut.FindAll(".mud-table").ShouldBeEmpty();
        cut.FindAll("a.crm-card-link").Select(a => a.GetAttribute("href")).ShouldBe(["accounts/1", "accounts/2"]);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Load more")).Click();

        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(3));
        _requests.Last().Page.ShouldBe(2);
        cut.FindAll("button").ShouldNotContain(b => b.TextContent.Contains("Load more"));
    }

    [Fact]
    public async Task Crossing_the_breakpoint_switches_between_grid_and_cards_and_starts_from_page_one()
    {
        _navigation.NavigateTo("/accounts?page=2");
        _data = _ => Rows(2, 50);
        var cut = RenderList();
        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(2));

        await Viewport.ResizeToAsync(Breakpoint.Xs);

        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(2));
        _requests.Last().Page.ShouldBe(1);
    }
}
