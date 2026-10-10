using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Lookups;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Opportunities;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The pipeline board (P15): columns with count and value, the optimistic drag and drop with snap-back, the
/// Won/Lost dialog, and the phone layout with a Stage dropdown instead of dragging.
/// </summary>
public class PipelinePageTests : MudTestContext
{
    private readonly FakeOpportunityService _opportunities = new();
    private readonly NavigationManager _navigation;

    public PipelinePageTests()
    {
        Services.AddSingleton<IOpportunityService>(_opportunities);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Price", true), new LookupOption(2, "Competitor", true)));
        Services.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero)));
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();

        _opportunities.OnBoard = _ => Board();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static OpportunityListItem Card(
        int id, string name, StageOption stage, decimal amount = 1000m, DateOnly? close = null, string owner = "Olga Owner") =>
        new(id, name, 3, "Acme", stage.Id, stage.Name, stage.IsWon, stage.IsLost, amount, 20m,
            close ?? new DateOnly(2030, 1, 1), "owner-2", owner, true, [(byte)id]);

    private static IReadOnlyList<BoardColumn> Board() =>
    [
        new(FakeOpportunityService.Prospecting, 2, 3000m,
            [Card(1, "Alpha deal", FakeOpportunityService.Prospecting, 1000m, new DateOnly(2026, 10, 1)),
             Card(2, "Beta deal", FakeOpportunityService.Prospecting, 2000m)]),
        new(FakeOpportunityService.Qualification, 0, 0m, []),
        new(FakeOpportunityService.Won, 1, 500m, [Card(3, "Old win", FakeOpportunityService.Won, 500m)]),
        new(FakeOpportunityService.Lost, 0, 0m, []),
    ];

    private IRenderedComponent<PipelinePage> RenderBoard(string url = "/opportunities/board")
    {
        _navigation.NavigateTo(url);
        return Render<PipelinePage>();
    }

    private static void WaitForBoard(IRenderedComponent<PipelinePage> cut) =>
        cut.WaitForAssertion(() => cut.FindAll("[data-board-column]").Count.ShouldBeGreaterThan(0));

    private static IElement CardItem(IRenderedComponent<PipelinePage> cut, int id) =>
        cut.Find($"[data-board-card='{id}']").Closest(".mud-drop-item")!;

    private static IElement Zone(IRenderedComponent<PipelinePage> cut, StageOption stage) =>
        cut.Find($".mud-drop-zone[identifier='{stage.Id}']");

    private static void Drag(IRenderedComponent<PipelinePage> cut, int cardId, StageOption to)
    {
        var item = CardItem(cut, cardId);
        item.DragStart();
        var zone = Zone(cut, to);
        zone.DragEnter();
        zone.Drop();
    }

    private static string Summary(IRenderedComponent<PipelinePage> cut, StageOption stage) =>
        cut.Find($"[data-column-summary='{stage.Name}']").TextContent.Trim();

    private static bool InZone(IRenderedComponent<PipelinePage> cut, int cardId, StageOption stage) =>
        Zone(cut, stage).QuerySelector($"[data-board-card='{cardId}']") is not null;

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    // ---- Layout ----

    [Fact]
    public void Each_open_stage_is_a_column_with_count_and_value_and_Won_and_Lost_are_collapsed_at_the_end()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        cut.FindAll("[data-board-column]").Select(c => c.GetAttribute("data-board-column"))
            .ShouldBe(["Prospecting", "Qualification", "Won", "Lost"]);
        Summary(cut, FakeOpportunityService.Prospecting).ShouldBe("2 · €3,000.00");
        Summary(cut, FakeOpportunityService.Qualification).ShouldBe("0 · €0.00");
        Summary(cut, FakeOpportunityService.Won).ShouldBe("1 · €500.00");

        // Collapsed: the Won card is not listed until the column is opened.
        cut.FindAll("[data-board-card='3']").ShouldBeEmpty();
        cut.Find("button[aria-label='Show Won cards']").Click();
        cut.WaitForAssertion(() => InZone(cut, 3, FakeOpportunityService.Won).ShouldBeTrue());
    }

    [Fact]
    public void Cards_show_name_account_amount_close_date_and_owner_initials_and_only_overdue_ones_have_the_red_edge()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        var late = cut.Find("[data-board-card='1']");
        late.TextContent.ShouldContain("Alpha deal");
        late.TextContent.ShouldContain("Acme");
        late.TextContent.ShouldContain("€1,000.00");
        late.TextContent.ShouldContain("1 Oct 2026");
        late.QuerySelector("[data-card-owner]")!.TextContent.Trim().ShouldBe("OO");
        late.GetAttribute("data-overdue").ShouldBe("true");
        late.GetAttribute("style")!.ShouldContain("border-left");
        cut.Find("[data-board-card='2']").HasAttribute("data-overdue").ShouldBeFalse();
    }

    [Fact]
    public void A_column_with_more_cards_than_were_loaded_links_to_the_list_for_that_stage()
    {
        _opportunities.OnBoard = _ =>
        [
            new BoardColumn(FakeOpportunityService.Prospecting, 130, 130000m, [Card(1, "Alpha deal", FakeOpportunityService.Prospecting)]),
            new BoardColumn(FakeOpportunityService.Qualification, 0, 0m, []),
            new BoardColumn(FakeOpportunityService.Won, 0, 0m, []),
            new BoardColumn(FakeOpportunityService.Lost, 0, 0m, []),
        ];

        var cut = RenderBoard("/opportunities/board?scope=all&owner=owner-2");
        WaitForBoard(cut);

        var link = cut.Find("[data-show-more='1']");
        link.TextContent.Trim().ShouldBe("Show 129 more in the list");
        link.GetAttribute("href").ShouldBe("opportunities?scope=all&owner=owner-2&stage=1");
    }

    [Fact]
    public void The_url_becomes_the_board_query_and_the_List_button_keeps_the_filters()
    {
        var cut = RenderBoard("/opportunities/board?scope=all&q=deal&owner=owner-2&from=2030-01-15&to=2030-03-31");

        cut.WaitForAssertion(() => _opportunities.BoardCalls.Count.ShouldBe(1));
        _opportunities.BoardCalls[0].ShouldBe(new BoardQuery(
            ListScope.All, "deal", "owner-2", new DateOnly(2030, 1, 15), new DateOnly(2030, 3, 31)));
        cut.WaitForAssertion(() =>
            cut.Find("[data-switch-to='list']").GetAttribute("href")
                .ShouldBe("opportunities?scope=all&q=deal&owner=owner-2&from=2030-01-15&to=2030-03-31"));
    }

    // ---- Drag and drop ----

    [Fact]
    public async Task Dropping_a_card_moves_it_at_once_and_keeps_it_there_when_the_save_succeeds()
    {
        var answer = new TaskCompletionSource<MoveStageResult>();
        _opportunities.OnMove = _ => answer.Task;
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Qualification);

        // Optimistic: moved, and the column totals follow, before the server has answered.
        cut.WaitForAssertion(() => InZone(cut, 1, FakeOpportunityService.Qualification).ShouldBeTrue());
        Summary(cut, FakeOpportunityService.Prospecting).ShouldBe("1 · €2,000.00");
        Summary(cut, FakeOpportunityService.Qualification).ShouldBe("1 · €1,000.00");
        var move = _opportunities.Moves.Single();
        move.OpportunityId.ShouldBe(1);
        move.StageId.ShouldBe(FakeOpportunityService.Qualification.Id);
        move.RowVersion.ShouldBe([1]);

        await cut.InvokeAsync(() => answer.SetResult(new MoveStageResult(
            MoveStageStatus.Moved, new MovedOpportunity(2, 20m, null, new DateOnly(2026, 10, 1), null, [77]))));

        cut.WaitForAssertion(() => InZone(cut, 1, FakeOpportunityService.Qualification).ShouldBeTrue());
        Summary(cut, FakeOpportunityService.Qualification).ShouldBe("1 · €1,000.00");
        _opportunities.BoardCalls.Count.ShouldBe(1); // no reload on success
    }

    [Fact]
    public void A_failing_save_snaps_the_card_back_and_says_so()
    {
        _opportunities.OnMove = _ => throw new InvalidOperationException("database is down");
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Qualification);

        // The save was attempted and failed; afterwards the card is back in its column with the old totals.
        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        cut.WaitForAssertion(() => InZone(cut, 1, FakeOpportunityService.Prospecting).ShouldBeTrue());
        InZone(cut, 1, FakeOpportunityService.Qualification).ShouldBeFalse();
        Summary(cut, FakeOpportunityService.Prospecting).ShouldBe("2 · €3,000.00");
        Summary(cut, FakeOpportunityService.Qualification).ShouldBe("0 · €0.00");
        cut.Markup.ShouldNotContain("database is down");
    }

    [Fact]
    public void A_row_version_conflict_snaps_the_card_back_and_reloads_the_board()
    {
        _opportunities.OnMove = _ => Task.FromResult(new MoveStageResult(
            MoveStageStatus.Conflict, Message: "Someone changed this opportunity. Reload to see the latest."));
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Qualification);

        cut.WaitForAssertion(() => _opportunities.BoardCalls.Count.ShouldBe(2));
        cut.WaitForAssertion(() => InZone(cut, 1, FakeOpportunityService.Prospecting).ShouldBeTrue());
        Summary(cut, FakeOpportunityService.Qualification).ShouldBe("0 · €0.00");
    }

    [Fact]
    public void A_refused_move_snaps_back_without_reloading()
    {
        _opportunities.OnMove = _ => Task.FromResult(new MoveStageResult(MoveStageStatus.Invalid, Message: "Choose a valid stage."));
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 2, FakeOpportunityService.Qualification);

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        cut.WaitForAssertion(() => InZone(cut, 2, FakeOpportunityService.Prospecting).ShouldBeTrue());
        _opportunities.BoardCalls.Count.ShouldBe(1);
    }

    [Fact]
    public void Dropping_on_Won_opens_the_dialog_and_sends_the_date()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Won);
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Mark as won"));
        _opportunities.Moves.ShouldBeEmpty(); // nothing is sent before the dialog is confirmed
        PressInDialog("Mark as won");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        var move = _opportunities.Moves.Single();
        move.StageId.ShouldBe(FakeOpportunityService.Won.Id);
        move.ClosedOn.ShouldBe(new DateOnly(2026, 10, 10));
        Summary(cut, FakeOpportunityService.Won).ShouldBe("2 · €1,500.00");
    }

    [Fact]
    public void Cancelling_the_Won_dialog_snaps_the_card_back_and_sends_nothing()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Won);
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Mark as won"));
        PressInDialog("Cancel");

        cut.WaitForAssertion(() => InZone(cut, 1, FakeOpportunityService.Prospecting).ShouldBeTrue());
        _opportunities.Moves.ShouldBeEmpty();
        Summary(cut, FakeOpportunityService.Won).ShouldBe("1 · €500.00");
    }

    [Fact]
    public void Dropping_on_Lost_needs_a_reason()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Lost);
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Mark as lost"));
        PressInDialog("Mark as lost");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("A lost reason is required."));
        _opportunities.Moves.ShouldBeEmpty();
    }

    [Fact]
    public void Dragging_a_won_card_back_to_an_open_stage_asks_to_reopen_first()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);
        cut.Find("button[aria-label='Show Won cards']").Click();
        cut.WaitForAssertion(() => InZone(cut, 3, FakeOpportunityService.Won).ShouldBeTrue());

        Drag(cut, 3, FakeOpportunityService.Qualification);
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Reopen opportunity?"));
        _opportunities.Moves.ShouldBeEmpty();
        PressInDialog("Reopen");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        _opportunities.Moves.Single().ConfirmReopen.ShouldBeTrue();
        cut.WaitForAssertion(() => InZone(cut, 3, FakeOpportunityService.Qualification).ShouldBeTrue());
    }

    [Fact]
    public void Dropping_a_card_on_its_own_column_does_nothing()
    {
        var cut = RenderBoard();
        WaitForBoard(cut);

        Drag(cut, 1, FakeOpportunityService.Prospecting);

        _opportunities.Moves.ShouldBeEmpty();
    }

    // ---- Phone ----

    [Fact]
    public void On_a_phone_the_stages_are_tabs_with_a_Stage_dropdown_per_card_and_no_drag_and_drop()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var cut = RenderBoard();

        cut.WaitForAssertion(() => cut.Find("[data-board-tabs]"));
        cut.FindAll(".mud-tab").Select(t => t.TextContent.Trim())
            .ShouldBe(["Prospecting (2)", "Qualification (0)", "Won (1)", "Lost (0)"]);
        cut.FindAll(".mud-drop-zone").ShouldBeEmpty();
        cut.FindAll("[data-card-stage-select]").Select(s => s.GetAttribute("data-card-stage-select")).ShouldBe(["1", "2"]);
    }

    [Fact]
    public void Choosing_a_stage_in_the_card_dropdown_moves_the_card()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var cut = RenderBoard();
        cut.WaitForAssertion(() => cut.Find("[data-card-stage-select='2']"));

        cut.Find("[data-card-stage-select='2']").MouseDown();
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-list-item").Count.ShouldBe(4));
        PopoverProvider.FindAll(".mud-list-item").First(i => i.TextContent.Trim() == "Qualification").Click();

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        var move = _opportunities.Moves.Single();
        move.OpportunityId.ShouldBe(2);
        move.StageId.ShouldBe(FakeOpportunityService.Qualification.Id);
        // The card has left the Prospecting tab at once.
        cut.WaitForAssertion(() => cut.FindAll("[data-board-card='2']").ShouldBeEmpty());
        cut.FindAll(".mud-tab").First().TextContent.Trim().ShouldBe("Prospecting (1)");
    }

    [Fact]
    public void A_failing_phone_move_puts_the_card_back()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        _opportunities.OnMove = _ => throw new InvalidOperationException("database is down");
        var cut = RenderBoard();
        cut.WaitForAssertion(() => cut.Find("[data-card-stage-select='2']"));

        cut.Find("[data-card-stage-select='2']").MouseDown();
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-list-item").Count.ShouldBe(4));
        PopoverProvider.FindAll(".mud-list-item").First(i => i.TextContent.Trim() == "Qualification").Click();

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        cut.WaitForAssertion(() => cut.Find("[data-board-card='2']"));
        cut.FindAll(".mud-tab").First().TextContent.Trim().ShouldBe("Prospecting (2)");
    }
}
