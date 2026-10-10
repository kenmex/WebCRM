using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>The Opportunities tab on the account page (P9): open first, then won and lost, and + New pre-filled.</summary>
public class AccountOpportunitiesTabTests : MudTestContext
{
    private readonly FakeOpportunityService _opportunities = new();
    private readonly NavigationManager _navigation;

    public AccountOpportunitiesTabTests()
    {
        Services.AddSingleton<IOpportunityService>(_opportunities);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero)));
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static OpportunityListItem Row(int id, string name, StageOption stage, DateOnly close) =>
        new(id, name, 5, "Acme Hellas", stage.Id, stage.Name, stage.IsWon, stage.IsLost, 1000m, 50m, close,
            "owner-2", "Olga Owner", true, [1]);

    private IRenderedComponent<AccountOpportunitiesTab> RenderTab() =>
        Render<AccountOpportunitiesTab>(p => p.Add(x => x.AccountId, 5));

    [Fact]
    public void Rows_come_in_the_order_the_service_gives_with_stage_and_amount_and_open_ones_overdue_are_marked()
    {
        _opportunities.AccountRows =
        [
            Row(1, "Late open deal", FakeOpportunityService.Prospecting, new DateOnly(2026, 9, 1)),
            Row(2, "Future open deal", FakeOpportunityService.Qualification, new DateOnly(2027, 1, 1)),
            Row(3, "Old won deal", FakeOpportunityService.Won, new DateOnly(2026, 1, 1)),
        ];

        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(3));
        cut.FindAll("tbody tr").Select(r => r.QuerySelector("strong")!.TextContent)
            .ShouldBe(["Late open deal", "Future open deal", "Old won deal"]);
        cut.Find("[data-stage='Won']").TextContent.ShouldBe("Won");
        cut.FindAll("tbody tr")[0].TextContent.ShouldContain("€1,000.00");
        cut.FindAll("[data-overdue='true']").Count.ShouldBe(1); // the won deal is not overdue
    }

    [Fact]
    public void An_account_without_opportunities_says_so()
    {
        var cut = RenderTab();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("This account has no opportunities yet."));
    }

    [Fact]
    public void New_opportunity_opens_the_form_with_this_account_filled_in()
    {
        var cut = RenderTab();
        cut.WaitForAssertion(() => cut.HasButton("New opportunity").ShouldBeTrue());

        cut.ButtonByText("New opportunity").Click();

        new Uri(_navigation.Uri).PathAndQuery.ShouldBe("/opportunities/new?accountId=5");
    }
}
