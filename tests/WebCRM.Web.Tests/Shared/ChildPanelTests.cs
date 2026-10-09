using Bunit;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Querying;
using WebCRM.Web.Tests.TestSupport;
using static WebCRM.Web.Tests.Shared.ChildPanelHost;

namespace WebCRM.Web.Tests.Shared;

public class ChildPanelTests : MudTestContext
{
    private readonly List<int> _pagesRequested = [];
    private Func<int, PagedResult<Row>> _data = _ => PagedResult<Row>.Empty;

    public ChildPanelTests() => StartProviders();

    private Task<PagedResult<Row>> Load(int page, CancellationToken cancellationToken)
    {
        _pagesRequested.Add(page);
        return Task.FromResult(_data(page));
    }

    private IRenderedComponent<ChildPanelHost> RenderPanel(Action<ComponentParameterCollectionBuilder<ChildPanelHost>>? more = null) =>
        Render<ChildPanelHost>(p =>
        {
            p.Add(x => x.Load, Load);
            more?.Invoke(p);
        });

    private static PagedResult<Row> Rows(int count, int total, int from = 1) =>
        new([.. Enumerable.Range(from, count).Select(i => new Row(i, $"Contact {i}"))], total);

    [Fact]
    public void It_loads_the_first_page_once_and_shows_the_rows()
    {
        _data = _ => Rows(2, 2);

        var cut = RenderPanel();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Select(c => c.TextContent).ShouldBe(["Contact 1", "Contact 2"]));
        _pagesRequested.ShouldBe([1]);
    }

    [Fact]
    public void It_shows_the_empty_text_when_there_is_nothing()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No contacts yet."));
    }

    [Fact]
    public void The_Add_button_raises_OnAdd_and_is_hidden_without_a_handler()
    {
        var added = 0;
        var withAdd = RenderPanel(p => p.Add(x => x.OnAdd, () => added++));
        withAdd.WaitForAssertion(() => withAdd.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Add contact"));

        withAdd.FindAll("button").First(b => b.TextContent.Trim() == "Add contact").Click();
        added.ShouldBe(1);

        var without = RenderPanel();
        without.WaitForAssertion(() => without.Markup.ShouldContain("No contacts yet."));
        without.FindAll("button").ShouldNotContain(b => b.TextContent.Trim() == "Add contact");
    }

    [Fact]
    public void Paging_requests_the_chosen_page()
    {
        _data = page => Rows(2, 5, from: (page - 1) * 2 + 1);
        var cut = RenderPanel();
        cut.WaitForAssertion(() => cut.FindAll(".mud-pagination").Count.ShouldBe(1));

        cut.FindAll(".mud-pagination button").First(b => b.TextContent.Trim() == "2").Click();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Select(c => c.TextContent).ShouldBe(["Contact 3", "Contact 4"]));
        _pagesRequested.ShouldBe([1, 2]);
    }

    [Fact]
    public void Changing_Version_reloads_from_page_one()
    {
        _data = _ => Rows(1, 1);
        var cut = RenderPanel(p => p.Add(x => x.Version, 0));
        cut.WaitForAssertion(() => _pagesRequested.Count.ShouldBe(1));

        cut.Render(p => p.Add(x => x.Load, Load).Add(x => x.Version, 1));

        cut.WaitForAssertion(() => _pagesRequested.Count.ShouldBe(2));
    }

    // ---- Asynchronous loads: the real database never answers synchronously ----

    [Fact]
    public async Task A_load_that_finishes_later_replaces_the_skeleton_with_the_rows()
    {
        var gate = new TaskCompletionSource<PagedResult<Row>>();
        var cut = Render<ChildPanelHost>(p => p.Add(x => x.Load, (_, _) => gate.Task));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("aria-busy"));

        await cut.InvokeAsync(() => gate.SetResult(Rows(2, 2)));

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(2));
        cut.Markup.ShouldNotContain("aria-busy");
    }

    [Fact]
    public async Task A_load_that_fails_later_shows_the_error_with_Retry_never_the_skeleton_and_Retry_recovers()
    {
        var attempts = new List<TaskCompletionSource<PagedResult<Row>>>();
        var cut = Render<ChildPanelHost>(p => p.Add(x => x.Load, (_, _) =>
        {
            var gate = new TaskCompletionSource<PagedResult<Row>>();
            attempts.Add(gate);
            return gate.Task;
        }));
        cut.WaitForAssertion(() => attempts.Count.ShouldBe(1));

        await cut.InvokeAsync(() => attempts[0].SetException(new InvalidOperationException("could not be translated")));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Could not load this list."));
        cut.Markup.ShouldNotContain("aria-busy");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Retry").Click();
        cut.WaitForAssertion(() => attempts.Count.ShouldBe(2));
        await cut.InvokeAsync(() => attempts[1].SetResult(Rows(1, 1)));

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(1));
        cut.Markup.ShouldNotContain("Could not load");
    }

    [Fact]
    public void If_the_browser_size_cannot_be_read_the_panel_still_loads_as_a_grid()
    {
        Viewport.Fail = true;
        _data = _ => Rows(2, 2);

        var cut = RenderPanel();

        cut.WaitForAssertion(() => cut.FindAll(".cell-name").Count.ShouldBe(2));
    }

    // ---- Phone ----

    [Fact]
    public void On_a_phone_it_shows_linked_cards_and_loads_more()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        _data = page => page == 1 ? Rows(2, 3) : Rows(1, 3, from: 3);
        var cut = RenderPanel();

        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(2));
        cut.FindAll(".mud-table").ShouldBeEmpty();
        cut.FindAll("a.crm-card-link").Select(a => a.GetAttribute("href")).ShouldBe(["contacts/1", "contacts/2"]);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Load more")).Click();

        cut.WaitForAssertion(() => cut.FindAll(".card-name").Count.ShouldBe(3));
        _pagesRequested.ShouldBe([1, 2]);
        cut.FindAll("button").ShouldNotContain(b => b.TextContent.Contains("Load more"));
    }
}
