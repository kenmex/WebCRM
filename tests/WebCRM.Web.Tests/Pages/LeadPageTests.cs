using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Lookups;
using WebCRM.Core.Personal;
using WebCRM.Core.Records;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Leads;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The lead page (P13 header card) driven with fake services: create, edit, the read-only converted lead with its
/// links, the status choices, delete, star and recent views.
/// </summary>
public class LeadPageTests : MudTestContext
{
    private const string LeaveDialog = "Leave without saving?";

    private readonly FakeLeadService _leads = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public LeadPageTests()
    {
        Services.AddSingleton<ILeadService>(_leads);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "New", true, LeadStatus.New),
            new LookupOption(2, "Contacted", true),
            new LookupOption(4, "Disqualified", true, LeadStatus.Disqualified),
            new LookupOption(5, "Converted", true, LeadStatus.Converted)));
        StartProviders();

        _navigation = Services.GetRequiredService<NavigationManager>();
        _leads.Leads[7] = Detail();
    }

    private static LeadDetail Detail(int statusId = 1, string status = "New", string? code = LeadStatus.New) =>
        new(7, "Maria Papadopoulou", "Acme", "maria@acme.gr", "+30 210 123 4567", 1, "Website", statusId, status, code,
            "owner-2", "Olga Owner", true, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);

    private static LeadDetail ConvertedDetail() =>
        Detail(5, "Converted", LeadStatus.Converted) with
        {
            ConvertedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
            ConvertedAccountId = 11,
            ConvertedAccountName = "Acme Hellas",
            ConvertedContactId = 12,
            ConvertedContactName = "Maria Papadopoulou",
            ConvertedOpportunityId = 13,
            ConvertedOpportunityName = "Acme deal",
        };

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    private IRenderedComponent<LeadPage> RenderNew()
    {
        _navigation.NavigateTo("/leads/new");
        return Render<LeadPage>();
    }

    private IRenderedComponent<LeadPage> RenderExisting()
    {
        _navigation.NavigateTo("/leads/7");
        return Render<LeadPage>(p => p.Add(x => x.Id, 7));
    }

    private static void Press(IRenderedComponent<LeadPage> cut, string text)
    {
        cut.WaitForAssertion(() => cut.HasButton(text).ShouldBeTrue());
        cut.ButtonByText(text).Click();
    }

    private static void OpenSelect(IRenderedComponent<LeadPage> cut, string label) =>
        cut.FindAll("div.mud-input-control")
            .First(d => d.QuerySelector("label")?.TextContent.Trim().StartsWith(label, StringComparison.Ordinal) == true)
            .MouseDown();

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    // ---- Create ----

    [Fact]
    public void A_new_lead_starts_as_New_owned_by_the_current_user_and_saves_to_its_page()
    {
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.WaitForAssertion(() => cut.InputByLabel("Status").GetAttribute("value").ShouldBe("New"));
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Sam Sales"));
        cut.InputByLabel("Name").Change("Maria Papadopoulou");

        Press(cut, "Save");

        cut.WaitForAssertion(() => Path.ShouldBe("/leads/42"));
        _leads.Saves.Single().Model.Name.ShouldBe("Maria Papadopoulou");
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_new_lead_without_a_name_will_not_save()
    {
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Name is required."));
        _leads.Saves.ShouldBeEmpty();
    }

    [Fact]
    public void Cancelling_a_dirty_new_form_asks_once_then_leaves_without_a_second_prompt()
    {
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.InputByLabel("Name").Change("Maria");

        Press(cut, "Cancel");
        PressInDialog("Discard");

        cut.WaitForAssertion(() => Path.ShouldBe("/leads"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Field_errors_from_the_server_are_shown_on_the_form()
    {
        _leads.OnSave = _ => new SaveResult(
            SaveStatus.Invalid,
            FieldErrors: new Dictionary<string, string> { [nameof(LeadEditModel.Email)] = "Enter a valid email address, e.g. name@example.com." });
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Name"));
        cut.InputByLabel("Name").Change("Maria");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Enter a valid email address"));
    }

    // ---- Edit and conflicts ----

    [Fact]
    public void Saving_an_existing_lead_returns_to_read_mode_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Company"));
        cut.InputByLabel("Company").Change("Acme Hellas SA");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.HasButton("Edit").ShouldBeTrue());
        _leads.Saves.Single().Model.Company.ShouldBe("Acme Hellas SA");

        cut.InvokeAsync(() => _navigation.NavigateTo("/leads"));

        cut.WaitForAssertion(() => Path.ShouldBe("/leads"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_conflict_names_the_other_user_and_offers_Reload()
    {
        _leads.OnSave = _ => new SaveResult(
            SaveStatus.Conflict, Conflict: new ConcurrencyConflict("Maria", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)));
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Company"));
        cut.InputByLabel("Company").Change("Other");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Changed by Maria"));
        cut.HasButton("Reload (lose my changes)").ShouldBeTrue();
        cut.HasButton("Overwrite with my changes").ShouldBeFalse(); // not an Admin
    }

    [Fact]
    public void The_status_choices_leave_out_Converted_because_only_Convert_sets_it()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Status").GetAttribute("value").ShouldBe("New"));

        OpenSelect(cut, "Status");

        PopoverProvider.WaitForAssertion(() =>
            PopoverProvider.FindAll(".mud-list-item").Select(i => i.TextContent.Trim()).ShouldBe(["New", "Contacted", "Disqualified"]));
    }

    [Fact]
    public void A_disqualified_lead_can_be_reopened_by_changing_its_status()
    {
        _leads.Leads[7] = Detail(4, "Disqualified", LeadStatus.Disqualified);
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Status").GetAttribute("value").ShouldBe("Disqualified"));

        OpenSelect(cut, "Status");
        PopoverProvider.WaitForAssertion(() => PopoverProvider.FindAll(".mud-list-item").Count.ShouldBe(3));
        PopoverProvider.FindAll(".mud-list-item").First(i => i.TextContent.Trim() == "Contacted").Click();
        Press(cut, "Save");

        cut.WaitForAssertion(() => _leads.Saves.Count.ShouldBe(1));
        _leads.Saves.Single().Model.LeadStatusId.ShouldBe(2);
    }

    // ---- Read mode ----

    [Fact]
    public void Read_mode_shows_the_details_with_mailto_and_tel_links_and_the_status_chip()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Maria Papadopoulou"));
        cut.Find("a.mud-link[href='mailto:maria@acme.gr']").TextContent.ShouldBe("maria@acme.gr");
        cut.Find("a.mud-link[href='tel:+302101234567']").TextContent.ShouldBe("+30 210 123 4567");
        cut.Find("[data-lead-status='NEW']").TextContent.ShouldBe("New");
        cut.Markup.ShouldContain("Website");
        cut.FindAll("[data-lead-converted]").ShouldBeEmpty();
        cut.HasButton("Edit").ShouldBeTrue();
        cut.HasButton("Delete").ShouldBeTrue();
    }

    [Fact]
    public void A_converted_lead_is_read_only_and_links_to_what_it_became()
    {
        _leads.Leads[7] = ConvertedDetail();

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.FindAll("[data-lead-converted]").Count.ShouldBe(1));
        cut.HasButton("Edit").ShouldBeFalse();
        cut.HasButton("Delete").ShouldBeFalse();
        cut.Find("a[href='accounts/11']").TextContent.ShouldContain("Acme Hellas");
        cut.Find("a[href='contacts/12']").TextContent.ShouldContain("Maria Papadopoulou");
        cut.Find("a[data-converted-opportunity]").TextContent.ShouldContain("Acme deal");
        cut.Find("a[data-converted-opportunity]").GetAttribute("href").ShouldBe("opportunities/13");
        cut.Find("[data-lead-status='CONVERTED']").TextContent.ShouldBe("Converted");
        cut.Find("[data-lead-converted]").TextContent.ShouldContain("5 Oct 2026 12:00");
    }

    // ---- Delete ----

    [Fact]
    public void Delete_asks_first_then_deletes_and_returns_to_the_list()
    {
        var cut = RenderExisting();
        Press(cut, "Delete");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Delete lead?"));
        PressInDialog("Delete");

        cut.WaitForAssertion(() => Path.ShouldBe("/leads"));
        _leads.Deleted.ShouldBe([7]);
    }

    // ---- Star and recent views ----

    [Fact]
    public void Opening_the_page_records_a_recent_view_but_the_new_form_does_not()
    {
        RenderNew();
        _recents.Recorded.ShouldBeEmpty();

        var cut = RenderExisting();
        cut.WaitForAssertion(() => _recents.Recorded.ShouldBe([(SearchEntity.Lead, 7)]));
    }

    [Fact]
    public void The_star_toggles_a_lead_both_ways()
    {
        _favourites.Starred.Add((SearchEntity.Lead, 7));
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']").GetAttribute("aria-pressed").ShouldBe("true"));
        cut.Find("button[aria-label='Remove from favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']"));

        _favourites.SetCalls.ShouldBe([(SearchEntity.Lead, 7, false)]);
    }

    [Fact]
    public void There_is_no_star_on_the_new_form()
    {
        var cut = RenderNew();

        cut.WaitForElement("h1");
        cut.FindAll("button[aria-label$='favourites']").ShouldBeEmpty();
    }
}
