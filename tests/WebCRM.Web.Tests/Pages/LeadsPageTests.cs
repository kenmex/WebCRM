using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Lookups;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Leads;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>The Leads list page (P12): the URL becomes the query, and the default is "open leads only".</summary>
public class LeadsPageTests : MudTestContext
{
    private readonly FakeLeadService _leads = new();
    private readonly FakeSavedViewService _savedViews = new();
    private readonly NavigationManager _navigation;

    public LeadsPageTests()
    {
        Services.AddSingleton<ILeadService>(_leads);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "New", true, LeadStatus.New),
            new LookupOption(2, "Contacted", true),
            new LookupOption(4, "Disqualified", true, LeadStatus.Disqualified),
            new LookupOption(5, "Converted", true, LeadStatus.Converted)));
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<ISavedViewService>(_savedViews);
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private static LeadListItem Row(int id, string name, string status = "New", string? code = LeadStatus.New, string? email = null) =>
        new(id, name, "Acme", email, "Website", status, code, "owner-2", "Olga Owner", true, new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void With_no_filter_the_list_asks_for_open_leads_newest_first()
    {
        _navigation.NavigateTo("/leads");

        Render<LeadsPage>().WaitForAssertion(() => _leads.Searches.Count.ShouldBe(1));

        var query = _leads.Searches[0];
        query.OpenOnly.ShouldBeTrue();
        query.StatusId.ShouldBeNull();
        query.Sort.ShouldBe(LeadSort.Created);
        query.Descending.ShouldBeTrue();
    }

    [Fact]
    public void The_url_becomes_the_query_and_the_filters_show_their_values()
    {
        _navigation.NavigateTo("/leads?scope=all&q=maria&status=all&source=3&owner=owner-2&sort=company&page=2");

        var cut = Render<LeadsPage>();

        cut.WaitForAssertion(() => _leads.Searches.Count.ShouldBe(1));
        _leads.Searches[0].ShouldBe(new LeadQuery(
            ListScope.All, Search: "maria", StatusId: null, OpenOnly: false, SourceId: 3, OwnerId: "owner-2",
            Sort: LeadSort.Company, Descending: false, Page: 2));
        cut.WaitForAssertion(() => cut.InputByLabel("Status").GetAttribute("value").ShouldBe("All statuses"));
        cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner");
    }

    [Fact]
    public void A_status_in_the_url_asks_for_that_status_only()
    {
        _navigation.NavigateTo("/leads?scope=all&status=5");

        var cut = Render<LeadsPage>();

        cut.WaitForAssertion(() => _leads.Searches.Single().StatusId.ShouldBe(5));
        cut.WaitForAssertion(() => cut.InputByLabel("Status").GetAttribute("value").ShouldBe("Converted"));
    }

    [Fact]
    public void Rows_show_the_status_and_open_the_lead()
    {
        _leads.OnSearch = _ => new PagedResult<LeadListItem>(
            [Row(1, "Maria Papadopoulou", email: "maria@acme.gr"), Row(2, "Done Deal", "Converted", LeadStatus.Converted)], 2);
        _navigation.NavigateTo("/leads?scope=all");

        var cut = Render<LeadsPage>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Count.ShouldBe(2));
        cut.Find("a[href='mailto:maria@acme.gr']").TextContent.ShouldBe("maria@acme.gr");
        cut.Find("[data-lead-status='CONVERTED']").TextContent.ShouldBe("Converted");
        cut.Find("[data-lead-status='NEW']").TextContent.ShouldBe("New");
    }

    [Fact]
    public void The_New_button_opens_the_new_lead_form()
    {
        _navigation.NavigateTo("/leads");

        var cut = Render<LeadsPage>();

        cut.WaitForAssertion(() => cut.FindAll("a[href='leads/new']").ShouldNotBeEmpty());
    }

    [Fact]
    public void The_Views_menu_belongs_to_the_leads_list()
    {
        _navigation.NavigateTo("/leads?scope=all&status=all");

        var cut = Render<LeadsPage>();

        cut.WaitForAssertion(() => cut.Find("[data-saved-views]").GetAttribute("data-saved-views").ShouldBe("leads"));
    }
}
