using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Shouldly;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class RecordTabsTests : MudTestContext
{
    private int _overviewCreated;
    private int _contactsCreated;

    public RecordTabsTests() => StartProviders();

    private IReadOnlyList<RecordTabItem> Tabs() =>
    [
        new RecordTabItem("Overview", Probe("overview", () => _overviewCreated++)),
        new RecordTabItem("Contacts", Probe("contacts", () => _contactsCreated++)),
    ];

    private static RenderFragment Probe(string text, Action created) => builder =>
    {
        builder.OpenComponent<LoadProbe>(0);
        builder.AddAttribute(1, nameof(LoadProbe.Text), text);
        builder.AddAttribute(2, nameof(LoadProbe.OnCreated), created);
        builder.CloseComponent();
    };

    private IRenderedComponent<RecordTabs> RenderTabs() => Render<RecordTabs>(p => p.Add(x => x.Tabs, Tabs()));

    private static void OpenTab(IRenderedComponent<RecordTabs> cut, string title) =>
        cut.FindAll(".mud-tab").First(t => t.TextContent.Trim() == title).Click();

    // ---- Desktop: tabs ----

    [Fact]
    public void Only_the_first_tab_is_created_until_another_is_opened()
    {
        var cut = RenderTabs();

        cut.WaitForAssertion(() => cut.FindAll(".mud-tab").Count.ShouldBe(2));
        _overviewCreated.ShouldBe(1);
        _contactsCreated.ShouldBe(0);
        cut.Markup.ShouldContain("overview");
        cut.Markup.ShouldNotContain("contacts</p>");
    }

    [Fact]
    public void Opening_a_tab_creates_it_once_and_it_is_kept_when_switching_back_and_forth()
    {
        var cut = RenderTabs();
        cut.WaitForAssertion(() => cut.FindAll(".mud-tab").Count.ShouldBe(2));

        OpenTab(cut, "Contacts");
        cut.WaitForAssertion(() => _contactsCreated.ShouldBe(1));

        OpenTab(cut, "Overview");
        OpenTab(cut, "Contacts");

        _contactsCreated.ShouldBe(1);
        _overviewCreated.ShouldBe(1);
    }

    [Fact]
    public void Only_the_active_tabs_content_is_visible()
    {
        var cut = RenderTabs();
        cut.WaitForAssertion(() => cut.FindAll(".mud-tab").Count.ShouldBe(2));
        OpenTab(cut, "Contacts");

        cut.WaitForAssertion(() =>
        {
            var panels = cut.FindAll("[data-record-tab]");
            panels.Count.ShouldBe(2);
            panels.Single(p => p.GetAttribute("aria-label") == "Overview").HasAttribute("hidden").ShouldBeTrue();
            panels.Single(p => p.GetAttribute("aria-label") == "Contacts").HasAttribute("hidden").ShouldBeFalse();
        });
    }

    // ---- Phone: stacked collapsible panels ----

    [Fact]
    public void On_a_phone_the_tabs_become_panels_and_only_the_first_is_open_and_created()
    {
        Viewport.Breakpoint = Breakpoint.Xs;

        var cut = RenderTabs();

        cut.WaitForAssertion(() => cut.FindAll(".mud-expand-panel").Count.ShouldBe(2));
        cut.FindAll(".mud-tab").ShouldBeEmpty();
        _overviewCreated.ShouldBe(1);
        _contactsCreated.ShouldBe(0);
    }

    [Fact]
    public void On_a_phone_expanding_a_panel_creates_its_content_once()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var cut = RenderTabs();
        cut.WaitForAssertion(() => cut.FindAll(".mud-expand-panel").Count.ShouldBe(2));

        var header = cut.FindAll(".mud-expand-panel-header").First(h => h.TextContent.Contains("Contacts"));
        header.Click();
        cut.WaitForAssertion(() => _contactsCreated.ShouldBe(1));

        header.Click(); // collapse
        header.Click(); // expand again

        _contactsCreated.ShouldBe(1);
    }

    [Fact]
    public async Task Crossing_the_breakpoint_switches_layout_without_recreating_opened_tabs()
    {
        var cut = RenderTabs();
        cut.WaitForAssertion(() => cut.FindAll(".mud-tab").Count.ShouldBe(2));
        OpenTab(cut, "Contacts");
        cut.WaitForAssertion(() => _contactsCreated.ShouldBe(1));

        await Viewport.ResizeToAsync(Breakpoint.Xs);

        cut.WaitForAssertion(() => cut.FindAll(".mud-expand-panel").Count.ShouldBe(2));
    }
}
