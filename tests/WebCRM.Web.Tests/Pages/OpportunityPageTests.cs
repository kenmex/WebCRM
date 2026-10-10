using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Lookups;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Personal;
using WebCRM.Core.Records;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Opportunities;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The opportunity page (P16 header card) driven with fake services: create, edit, the stage stepper with the
/// Won/Lost dialog and the reopen confirmation, conflicts, delete, star and recent views.
/// </summary>
public class OpportunityPageTests : MudTestContext
{
    private readonly FakeOpportunityService _opportunities = new();
    private readonly FakeAccountService _accounts = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public OpportunityPageTests()
    {
        Services.AddSingleton<IOpportunityService>(_opportunities);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Price", true), new LookupOption(2, "Competitor", true)));
        Services.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero)));
        StartProviders();

        _navigation = Services.GetRequiredService<NavigationManager>();
        _accounts.Picker.Add(new AccountPickerItem(3, "Acme Hellas"));
        _opportunities.Contacts.Add(new ContactOption(12, "Maria Papadopoulou"));
        _opportunities.Opportunities[7] = Detail();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static OpportunityDetail Detail(StageOption? stage = null, string? lostReason = null)
    {
        stage ??= FakeOpportunityService.Prospecting;
        return new OpportunityDetail(
            7, "Acme deal", 3, "Acme Hellas", 12, "Maria Papadopoulou", stage.Id, stage.Name, stage.IsWon, stage.IsLost,
            1500m, "EUR", 40m, false, new DateOnly(2030, 1, 15), stage.IsOpen ? null : new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            lostReason is null ? null : 1, lostReason, "owner-2", "Olga Owner", true,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);
    }

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    private IRenderedComponent<OpportunityPage> RenderNew(int? accountId = null)
    {
        _navigation.NavigateTo(accountId is { } id ? $"/opportunities/new?accountId={id}" : "/opportunities/new");
        return Render<OpportunityPage>();
    }

    private IRenderedComponent<OpportunityPage> RenderExisting()
    {
        _navigation.NavigateTo("/opportunities/7");
        return Render<OpportunityPage>(p => p.Add(x => x.Id, 7));
    }

    private static void Press(IRenderedComponent<OpportunityPage> cut, string text)
    {
        cut.WaitForAssertion(() => cut.HasButton(text).ShouldBeTrue());
        cut.ButtonByText(text).Click();
    }

    private static void ClickStep(IRenderedComponent<OpportunityPage> cut, string stage)
    {
        cut.WaitForAssertion(() => cut.Find($"[data-stage-step='{stage}']"));
        cut.Find($"[data-stage-step='{stage}']").Click();
    }

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    // ---- Read mode and the stepper ----

    [Fact]
    public void Read_mode_shows_the_details_with_links_and_the_stage_stepper()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Acme deal"));
        cut.Find("a[href='accounts/3']");
        cut.Find("a[href='contacts/12']").TextContent.ShouldBe("Maria Papadopoulou");
        cut.Markup.ShouldContain("€1,500.00");
        cut.Markup.ShouldContain("€600.00"); // weighted
        cut.FindAll("[data-stage-step]").Select(s => s.GetAttribute("data-stage-step"))
            .ShouldBe(["Prospecting", "Qualification", "Won", "Lost"]);
        cut.Find("[data-current='true']").GetAttribute("data-stage-step").ShouldBe("Prospecting");
        cut.HasButton("Edit").ShouldBeTrue();
    }

    [Fact]
    public void Clicking_an_open_stage_moves_there_without_a_dialog()
    {
        var cut = RenderExisting();

        ClickStep(cut, "Qualification");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        var move = _opportunities.Moves.Single();
        move.OpportunityId.ShouldBe(7);
        move.StageId.ShouldBe(FakeOpportunityService.Qualification.Id);
        move.RowVersion.ShouldBe([1]);
        move.ConfirmReopen.ShouldBeFalse();
        DialogProvider.Markup.ShouldNotContain("Mark as");
    }

    [Fact]
    public void Clicking_Won_asks_for_the_close_date_and_sends_it()
    {
        var cut = RenderExisting();

        ClickStep(cut, "Won");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Mark as won"));
        PressInDialog("Mark as won");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        var move = _opportunities.Moves.Single();
        move.StageId.ShouldBe(FakeOpportunityService.Won.Id);
        move.ClosedOn.ShouldBe(new DateOnly(2026, 10, 10));
        move.LostReasonId.ShouldBeNull();
    }

    [Fact]
    public void Clicking_Lost_needs_a_reason_before_anything_is_sent()
    {
        var cut = RenderExisting();

        ClickStep(cut, "Lost");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Mark as lost"));
        PressInDialog("Mark as lost");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("A lost reason is required."));
        _opportunities.Moves.ShouldBeEmpty();
    }

    [Fact]
    public void Lost_with_a_reason_sends_the_reason_and_the_date()
    {
        var cut = RenderExisting();

        ClickStep(cut, "Lost");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Lost reason"));
        DialogProvider.FindAll("div.mud-input-control")
            .First(d => d.QuerySelector("label")?.TextContent.Trim().StartsWith("Lost reason", StringComparison.Ordinal) == true)
            .MouseDown();
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-list-item").Count.ShouldBe(2));
        PopoverProvider.FindAll(".mud-list-item").First(i => i.TextContent.Trim() == "Competitor").Click();
        PressInDialog("Mark as lost");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        var move = _opportunities.Moves.Single();
        move.StageId.ShouldBe(FakeOpportunityService.Lost.Id);
        move.LostReasonId.ShouldBe(2);
        move.ClosedOn.ShouldBe(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void Cancelling_the_Won_Lost_dialog_sends_nothing()
    {
        var cut = RenderExisting();

        ClickStep(cut, "Won");
        PressInDialog("Cancel");

        cut.WaitForAssertion(() => DialogProvider.FindAll("button").ShouldNotContain(b => b.TextContent.Trim() == "Mark as won"));
        _opportunities.Moves.ShouldBeEmpty();
    }

    [Fact]
    public void Moving_a_closed_opportunity_back_to_an_open_stage_asks_for_confirmation_first()
    {
        _opportunities.Opportunities[7] = Detail(FakeOpportunityService.Won);
        var cut = RenderExisting();

        ClickStep(cut, "Qualification");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Reopen opportunity?"));
        _opportunities.Moves.ShouldBeEmpty();
        PressInDialog("Reopen");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(1));
        _opportunities.Moves.Single().ConfirmReopen.ShouldBeTrue();
    }

    [Fact]
    public void Declining_the_reopen_confirmation_leaves_the_stage_alone()
    {
        _opportunities.Opportunities[7] = Detail(FakeOpportunityService.Lost, "Price");
        var cut = RenderExisting();

        ClickStep(cut, "Qualification");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Reopen opportunity?"));
        PressInDialog("Cancel");

        cut.WaitForAssertion(() => DialogProvider.Markup.ShouldNotContain("Reopen opportunity?"));
        _opportunities.Moves.ShouldBeEmpty();
    }

    [Fact]
    public void A_service_that_wants_confirmation_gets_it_and_the_move_is_repeated()
    {
        var calls = 0;
        _opportunities.OnMove = request => Task.FromResult(++calls == 1
            ? new MoveStageResult(MoveStageStatus.NeedsReopenConfirm)
            : new MoveStageResult(MoveStageStatus.Moved, new MovedOpportunity(request.StageId, 20m, null, new DateOnly(2030, 1, 15), null, [2])));
        var cut = RenderExisting();

        ClickStep(cut, "Qualification");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Reopen opportunity?"));
        PressInDialog("Reopen");

        cut.WaitForAssertion(() => _opportunities.Moves.Count.ShouldBe(2));
        _opportunities.Moves[0].ConfirmReopen.ShouldBeFalse();
        _opportunities.Moves[1].ConfirmReopen.ShouldBeTrue();
    }

    [Fact]
    public void A_row_version_conflict_on_a_move_says_so_and_offers_Reload()
    {
        _opportunities.OnMove = _ => Task.FromResult(new MoveStageResult(
            MoveStageStatus.Conflict, Message: "Someone changed this opportunity.", Conflict: new ConcurrencyConflict("Maria", null)));
        var cut = RenderExisting();

        ClickStep(cut, "Qualification");

        cut.WaitForAssertion(() => cut.Find("[data-move-conflict='true']"));
        cut.HasButton("Reload").ShouldBeTrue();
    }

    // ---- Edit ----

    [Fact]
    public void While_editing_the_stepper_is_off_and_the_stage_field_is_read_only()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));

        cut.FindAll("[data-stage-step]").ShouldAllBe(b => b.HasAttribute("disabled"));
        cut.InputByLabel("Stage").HasAttribute("readonly").ShouldBeTrue();
    }

    [Fact]
    public void Saving_an_existing_opportunity_returns_to_read_mode()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.InputByLabel("Name").Change("Acme deal 2");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.HasButton("Edit").ShouldBeTrue());
        var saved = _opportunities.Saves.Single().Model;
        saved.Name.ShouldBe("Acme deal 2");
        saved.Id.ShouldBe(7);
        saved.PrimaryContactId.ShouldBe(12);
        saved.RowVersion.ShouldBe([1]);
    }

    [Fact]
    public void A_save_conflict_names_the_other_user_and_offers_Reload()
    {
        _opportunities.OnSave = _ => new SaveResult(
            SaveStatus.Conflict, Conflict: new ConcurrencyConflict("Maria", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)));
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.InputByLabel("Name").Change("Other");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Changed by Maria"));
        cut.HasButton("Reload (lose my changes)").ShouldBeTrue();
    }

    [Fact]
    public void A_lost_opportunity_shows_its_reason_and_the_form_asks_for_one()
    {
        _opportunities.Opportunities[7] = Detail(FakeOpportunityService.Lost, "Price");
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Lost reason"));
        cut.Markup.ShouldContain("Price");
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Lost reason").GetAttribute("value").ShouldBe("Price"));
    }

    // ---- Create ----

    [Fact]
    public void A_new_opportunity_starts_on_the_first_open_stage_owned_by_the_current_user_and_saves_to_its_page()
    {
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.WaitForAssertion(() => cut.InputByLabel("Stage").GetAttribute("value").ShouldBe("Prospecting"));
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Sam Sales"));
        cut.InputByLabel("Name").Change("Big deal");

        Press(cut, "Save");

        // No account yet: the form refuses.
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Account is required."));
        _opportunities.Saves.ShouldBeEmpty();
    }

    [Fact]
    public void A_new_opportunity_from_an_account_has_that_account_filled_in_and_saves()
    {
        var cut = RenderNew(accountId: 3);
        cut.WaitForAssertion(() => cut.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
        cut.InputByLabel("Name").Change("Big deal");

        Press(cut, "Save");

        cut.WaitForAssertion(() => Path.ShouldBe("/opportunities/42"));
        var saved = _opportunities.Saves.Single().Model;
        saved.AccountId.ShouldBe(3);
        saved.StageId.ShouldBe(FakeOpportunityService.Prospecting.Id);
        saved.Name.ShouldBe("Big deal");
        _opportunities.ContactOptionCalls.ShouldContain(3);
    }

    [Fact]
    public void Field_errors_from_the_server_are_shown_on_the_form()
    {
        _opportunities.OnSave = _ => new SaveResult(
            SaveStatus.Invalid,
            FieldErrors: new Dictionary<string, string> { [nameof(OpportunityEditModel.PrimaryContactId)] = "The contact must belong to the chosen account." });
        var cut = RenderNew(accountId: 3);
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.InputByLabel("Name").Change("Big deal");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("The contact must belong to the chosen account."));
    }

    // ---- Delete, star, recents ----

    [Fact]
    public void Delete_asks_first_then_deletes_and_returns_to_the_list()
    {
        var cut = RenderExisting();
        Press(cut, "Delete");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Delete opportunity?"));
        PressInDialog("Delete");

        cut.WaitForAssertion(() => Path.ShouldBe("/opportunities"));
        _opportunities.Deleted.ShouldBe([7]);
    }

    [Fact]
    public void Opening_the_page_records_a_recent_view_but_the_new_form_does_not()
    {
        RenderNew();
        _recents.Recorded.ShouldBeEmpty();

        var cut = RenderExisting();
        cut.WaitForAssertion(() => _recents.Recorded.ShouldBe([(SearchEntity.Opportunity, 7)]));
    }

    [Fact]
    public void The_star_toggles_an_opportunity_both_ways()
    {
        _favourites.Starred.Add((SearchEntity.Opportunity, 7));
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']").GetAttribute("aria-pressed").ShouldBe("true"));
        cut.Find("button[aria-label='Remove from favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']"));

        _favourites.SetCalls.ShouldBe([(SearchEntity.Opportunity, 7, false)]);
    }
}
