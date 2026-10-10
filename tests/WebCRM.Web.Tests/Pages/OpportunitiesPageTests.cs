using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Opportunities;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>The Opportunities list page (P14): the URL becomes the query, footer totals, overdue highlight, board toggle.</summary>
public class OpportunitiesPageTests : MudTestContext
{
    private readonly FakeOpportunityService _opportunities = new();
    private readonly NavigationManager _navigation;

    public OpportunitiesPageTests()
    {
        Services.AddSingleton<IOpportunityService>(_opportunities);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<ISavedViewService>(new FakeSavedViewService());
        Services.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero)));
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static OpportunityListItem Row(
        int id, string name, StageOption stage, decimal amount = 1000m, decimal probability = 50m, DateOnly? close = null) =>
        new(id, name, 3, "Acme", stage.Id, stage.Name, stage.IsWon, stage.IsLost, amount, probability,
            close ?? new DateOnly(2030, 1, 1), "owner-2", "Olga Owner", true, [1]);

    [Fact]
    public void With_no_filter_the_list_asks_for_open_opportunities_by_close_date()
    {
        _navigation.NavigateTo("/opportunities");

        Render<OpportunitiesPage>().WaitForAssertion(() => _opportunities.Searches.Count.ShouldBe(1));

        var query = _opportunities.Searches[0];
        query.Outcome.ShouldBe(OpportunityOutcome.Open);
        query.StageId.ShouldBeNull();
        query.Sort.ShouldBe(OpportunitySort.CloseDate);
        query.Descending.ShouldBeFalse();
    }

    [Fact]
    public void The_url_becomes_the_query_and_the_filters_show_their_values()
    {
        _navigation.NavigateTo("/opportunities?scope=all&q=deal&outcome=lost&stage=2&owner=owner-2&from=2030-01-15&to=2030-03-31&sort=amount&desc=1&page=2");

        var cut = Render<OpportunitiesPage>();

        cut.WaitForAssertion(() => _opportunities.Searches.Count.ShouldBe(1));
        _opportunities.Searches[0].ShouldBe(new OpportunityQuery(
            ListScope.All, Search: "deal", StageId: 2, Outcome: OpportunityOutcome.Lost, OwnerId: "owner-2",
            CloseFrom: new DateOnly(2030, 1, 15), CloseTo: new DateOnly(2030, 3, 31),
            Sort: OpportunitySort.Amount, Descending: true, Page: 2));
        cut.WaitForAssertion(() => cut.InputByLabel("Show").GetAttribute("value").ShouldBe("Lost"));
        cut.InputByLabel("Stage").GetAttribute("value").ShouldBe("Qualification");
        cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner");
    }

    [Fact]
    public void The_footer_shows_the_totals_of_the_whole_filtered_set_not_the_page()
    {
        _opportunities.OnSearch = _ => new PagedResult<OpportunityListItem>([Row(1, "Deal one", FakeOpportunityService.Prospecting)], 30);
        _opportunities.OnTotals = _ => new OpportunityTotals(123456.5m, 61728.25m);
        _navigation.NavigateTo("/opportunities?scope=all");

        var cut = Render<OpportunitiesPage>();

        cut.WaitForAssertion(() => cut.Find("[data-total='amount']").TextContent.ShouldBe("€123,456.50"));
        cut.Find("[data-total='weighted']").TextContent.ShouldBe("€61,728.25");
        _opportunities.TotalsCalls.Single().Scope.ShouldBe(ListScope.All);
    }

    [Fact]
    public void Rows_show_stage_amount_weighted_and_only_overdue_open_rows_are_highlighted()
    {
        _opportunities.OnSearch = _ => new PagedResult<OpportunityListItem>(
        [
            Row(1, "Late deal", FakeOpportunityService.Prospecting, close: new DateOnly(2026, 10, 1)),
            Row(2, "Future deal", FakeOpportunityService.Prospecting, close: new DateOnly(2026, 11, 1)),
            Row(3, "Old won deal", FakeOpportunityService.Won, close: new DateOnly(2026, 1, 1)),
        ], 3);
        _navigation.NavigateTo("/opportunities?scope=all&outcome=any");

        var cut = Render<OpportunitiesPage>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(3));
        var rows = cut.FindAll("tbody tr");
        rows[0].GetAttribute("style")!.ShouldContain("error-hover");
        rows[1].GetAttribute("style")!.ShouldNotContain("error-hover");
        rows[2].GetAttribute("style")!.ShouldNotContain("error-hover");
        cut.FindAll("[data-overdue='true']").Count.ShouldBe(1);
        rows[0].TextContent.ShouldContain("€1,000.00");
        rows[0].TextContent.ShouldContain("€500.00");
        cut.Find("[data-stage='Won']").TextContent.ShouldBe("Won");
    }

    [Fact]
    public void The_Board_button_keeps_scope_search_owner_and_dates_but_drops_stage_and_page()
    {
        _navigation.NavigateTo("/opportunities?scope=all&q=deal&stage=2&owner=owner-2&from=2030-01-15&page=3");

        var cut = Render<OpportunitiesPage>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-switch-to='board']").GetAttribute("href")
                .ShouldBe("opportunities/board?scope=all&q=deal&owner=owner-2&from=2030-01-15"));
    }

    [Fact]
    public void The_New_button_opens_the_new_opportunity_form_and_the_Views_menu_belongs_to_the_list()
    {
        _navigation.NavigateTo("/opportunities");

        var cut = Render<OpportunitiesPage>();

        cut.WaitForAssertion(() => cut.FindAll("a[href='opportunities/new']").ShouldNotBeEmpty());
        cut.Find("[data-saved-views]").GetAttribute("data-saved-views").ShouldBe("opportunities");
    }
}
