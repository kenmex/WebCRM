using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Leads;
using WebCRM.Core.Lookups;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Leads;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>The Convert action on the lead page (P13): what the dialog starts with, what it sends, and what it shows.</summary>
public class ConvertLeadDialogTests : MudTestContext
{
    private readonly FakeLeadService _leads = new();
    private readonly FakeContactService _contacts = new();
    private readonly FakeAccountService _accounts = new();
    private readonly NavigationManager _navigation;

    public ConvertLeadDialogTests()
    {
        _accounts.Picker.Add(new AccountPickerItem(5, "Acme Hellas"));
        _accounts.Picker.Add(new AccountPickerItem(6, "Other Co"));
        Services.AddSingleton<ILeadService>(_leads);
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(new OwnerOption("sales-1", "Sam Sales", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(new FakeFavouriteService());
        Services.AddSingleton<IRecentViewService>(new FakeRecentViewService());
        Services.AddSingleton<ILookupService>(new FakeLookupService(new LookupOption(1, "New", true, LeadStatus.New)));
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();

        _leads.Leads[7] = Detail();
        _leads.Prefills[7] = new ConvertPrefill(
            7, "Acme", "Maria", "Papadopoulou", "maria@acme.gr", "210 123 4567", "Acme deal",
            [new AccountMatch(5, "Acme Hellas", AccountMatchReason.CompanyName | AccountMatchReason.EmailDomain)]);
    }

    private static LeadDetail Detail(string status = "New", string? code = LeadStatus.New) =>
        new(7, "Maria Papadopoulou", "Acme", "maria@acme.gr", null, null, null, 1, status, code,
            "sales-1", "Sam Sales", true, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);

    private IRenderedComponent<LeadPage> RenderLead()
    {
        _navigation.NavigateTo("/leads/7");
        return Render<LeadPage>(p => p.Add(x => x.Id, 7));
    }

    private IRenderedComponent<LeadPage> OpenDialog()
    {
        var cut = RenderLead();
        cut.WaitForAssertion(() => cut.HasButton("Convert").ShouldBeTrue());
        cut.ButtonByText("Convert").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("[data-convert-section='account']").Count.ShouldBe(1));
        return cut;
    }

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    private void ChooseRadio(string text) =>
        DialogProvider.FindAll("label.mud-radio").First(l => l.TextContent.Trim() == text).QuerySelector("input")!.Click();

    private static ConvertResult Converted => new(ConvertStatus.Converted, AccountId: 1, ContactId: 2, OpportunityId: 3);

    // ---- The button ----

    [Theory]
    [InlineData("New", LeadStatus.New, true)]
    [InlineData("Disqualified", LeadStatus.Disqualified, false)]
    [InlineData("Converted", LeadStatus.Converted, false)]
    public void The_Convert_button_shows_only_for_a_lead_that_is_neither_disqualified_nor_converted(
        string status, string code, bool shown)
    {
        _leads.Leads[7] = Detail(status, code) with
        {
            ConvertedAt = code == LeadStatus.Converted ? DateTime.UtcNow : null,
            ConvertedAccountId = code == LeadStatus.Converted ? 5 : null,
            ConvertedContactId = code == LeadStatus.Converted ? 6 : null,
        };

        var cut = RenderLead();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Maria Papadopoulou"));
        cut.HasButton("Convert").ShouldBe(shown);
    }

    // ---- What the dialog starts with ----

    [Fact]
    public void The_dialog_is_prefilled_from_the_lead_and_suggests_matching_accounts()
    {
        OpenDialog();

        DialogProvider.InputByLabel("Account name").GetAttribute("value").ShouldBe("Acme");
        DialogProvider.InputByLabel("First name").GetAttribute("value").ShouldBe("Maria");
        DialogProvider.InputByLabel("Last name").GetAttribute("value").ShouldBe("Papadopoulou");
        DialogProvider.InputByLabel("Email").GetAttribute("value").ShouldBe("maria@acme.gr");
        DialogProvider.InputByLabel("Phone").GetAttribute("value").ShouldBe("210 123 4567");
        var match = DialogProvider.Find("[data-convert-match='5']");
        match.TextContent.Trim().ShouldBe("Acme Hellas");
        match.GetAttribute("title").ShouldBe("Same company name and email domain");
        DialogProvider.FindAll("[data-convert-section='opportunity'] input[type=text]").ShouldBeEmpty(); // unchecked
    }

    [Fact]
    public void A_lead_that_can_no_longer_be_converted_says_so_instead_of_showing_the_form()
    {
        _leads.Prefills.Clear();

        var cut = RenderLead();
        cut.WaitForAssertion(() => cut.HasButton("Convert").ShouldBeTrue());
        cut.ButtonByText("Convert").Click();

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("can no longer be converted"));
        DialogProvider.FindAll("[data-convert-section]").ShouldBeEmpty();
    }

    // ---- What it sends ----

    [Fact]
    public void Converting_with_the_defaults_creates_a_new_account_and_a_new_contact_and_shows_the_lead_converted()
    {
        _leads.OnConvert = _ =>
        {
            _leads.Leads[7] = Detail("Converted", LeadStatus.Converted) with
            {
                ConvertedAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
                ConvertedAccountId = 1,
                ConvertedAccountName = "Acme",
                ConvertedContactId = 2,
                ConvertedContactName = "Maria Papadopoulou",
            };
            return Converted;
        };
        var cut = OpenDialog();

        PressInDialog("Convert");

        cut.WaitForAssertion(() => cut.FindAll("[data-lead-converted]").Count.ShouldBe(1));
        var request = _leads.Conversions.Single();
        request.LeadId.ShouldBe(7);
        request.NewAccountName.ShouldBe("Acme");
        request.ExistingAccountId.ShouldBeNull();
        request.ExistingContactId.ShouldBeNull();
        request.ContactFirstName.ShouldBe("Maria");
        request.ContactLastName.ShouldBe("Papadopoulou");
        request.ContactEmail.ShouldBe("maria@acme.gr");
        request.CreateOpportunity.ShouldBeFalse();
        cut.HasButton("Convert").ShouldBeFalse(); // read-only now
        DialogProvider.FindAll("[data-convert-section]").ShouldBeEmpty(); // the dialog is gone
    }

    [Fact]
    public void The_opportunity_checkbox_adds_a_named_opportunity_to_the_request()
    {
        OpenDialog();

        DialogProvider.Find("[data-convert-section='opportunity'] input[type=checkbox]").Change(true);
        DialogProvider.WaitForAssertion(() => DialogProvider.InputByLabel("Opportunity name").GetAttribute("value").ShouldBe("Acme deal"));
        DialogProvider.InputByLabel("Opportunity name").Change("Acme rollout");
        PressInDialog("Convert");

        _leads.Conversions.Single().CreateOpportunity.ShouldBeTrue();
        _leads.Conversions.Single().OpportunityName.ShouldBe("Acme rollout");
    }

    [Fact]
    public void Choosing_a_suggested_account_links_it_instead_of_creating_one()
    {
        OpenDialog();

        DialogProvider.Find("[data-convert-match='5']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
        PressInDialog("Convert");

        var request = _leads.Conversions.Single();
        request.ExistingAccountId.ShouldBe(5);
        request.NewAccountName.ShouldBeNull();
        request.ExistingContactId.ShouldBeNull(); // the account has no contacts: a new one is created on it
        request.ContactLastName.ShouldBe("Papadopoulou");
    }

    [Fact]
    public void A_contact_of_the_account_with_the_leads_email_is_preselected_and_can_be_switched_to_a_new_one()
    {
        _contacts.OnSearch = q => q.AccountId == 5
            ? new PagedResult<ContactListItem>(
                [
                    new ContactListItem(21, "Other Person", 5, "Acme Hellas", "CFO", "cfo@acme.gr", null, null, "sales-1", "Sam Sales", true, null),
                    new ContactListItem(22, "Maria Papadopoulou", 5, "Acme Hellas", "Buyer", "Maria@Acme.gr", null, null, "sales-1", "Sam Sales", true, null),
                ],
                2)
            : PagedResult<ContactListItem>.Empty;
        OpenDialog();

        DialogProvider.Find("[data-convert-match='5']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("[data-convert-email-match]").Count.ShouldBe(1));
        PressInDialog("Convert");

        _contacts.Searches.Single().ShouldBe(new ContactQuery(ListScope.All, AccountId: 5, PageSize: 100));
        _leads.Conversions.Single().ExistingContactId.ShouldBe(22);
    }

    [Fact]
    public void Picking_a_new_contact_for_an_existing_account_sends_the_contact_fields()
    {
        _contacts.OnSearch = _ => new PagedResult<ContactListItem>(
            [new ContactListItem(21, "Other Person", 5, "Acme Hellas", "CFO", "cfo@acme.gr", null, null, "sales-1", "Sam Sales", true, null)], 1);
        OpenDialog();
        DialogProvider.Find("[data-convert-match='5']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("label.mud-radio").Count.ShouldBeGreaterThan(2));

        ChooseRadio("Create a new contact");
        DialogProvider.WaitForAssertion(() => DialogProvider.InputByLabel("Last name"));
        PressInDialog("Convert");

        var request = _leads.Conversions.Single();
        request.ExistingAccountId.ShouldBe(5);
        request.ExistingContactId.ShouldBeNull();
        request.ContactLastName.ShouldBe("Papadopoulou");
    }

    // ---- Mistakes and refusals ----

    [Fact]
    public void A_missing_account_name_or_last_name_is_caught_before_anything_is_sent()
    {
        OpenDialog();
        DialogProvider.InputByLabel("Account name").Change("");
        DialogProvider.InputByLabel("Last name").Change("");

        PressInDialog("Convert");

        DialogProvider.WaitForAssertion(() =>
        {
            DialogProvider.Markup.ShouldContain("Account name is required.");
            DialogProvider.Markup.ShouldContain("Last name is required.");
        });
        _leads.Conversions.ShouldBeEmpty();
    }

    [Fact]
    public void Using_an_existing_account_without_choosing_one_is_caught()
    {
        OpenDialog();
        ChooseRadio("Use an existing account");

        PressInDialog("Convert");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Choose an account."));
        _leads.Conversions.ShouldBeEmpty();
    }

    [Fact]
    public void Errors_from_the_service_are_shown_and_the_dialog_stays_open()
    {
        _leads.OnConvert = _ => new ConvertResult(
            ConvertStatus.Invalid,
            FieldErrors: new Dictionary<string, string>
            {
                [nameof(LeadConvertRequest.NewAccountName)] = "An account with this name already exists. Choose it as the existing account instead.",
            });
        OpenDialog();

        PressInDialog("Convert");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("An account with this name already exists."));
        DialogProvider.FindAll("[data-convert-section]").ShouldNotBeEmpty();
        _leads.Conversions.Count.ShouldBe(1);
    }

    [Fact]
    public void A_lead_converted_by_someone_else_a_moment_ago_closes_the_dialog_and_shows_it_converted()
    {
        _leads.OnConvert = _ =>
        {
            _leads.Leads[7] = Detail("Converted", LeadStatus.Converted) with
            {
                ConvertedAt = DateTime.UtcNow,
                ConvertedAccountId = 9,
                ConvertedAccountName = "Theirs",
                ConvertedContactId = 10,
                ConvertedContactName = "Someone",
            };
            return new ConvertResult(ConvertStatus.AlreadyConverted);
        };
        var cut = OpenDialog();

        PressInDialog("Convert");

        cut.WaitForAssertion(() => cut.FindAll("[data-lead-converted]").Count.ShouldBe(1));
        cut.Find("a[href='accounts/9']").TextContent.ShouldContain("Theirs");
    }

    [Fact]
    public void Cancel_closes_the_dialog_without_converting()
    {
        var cut = OpenDialog();

        PressInDialog("Cancel");

        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("[data-convert-section]").ShouldBeEmpty());
        _leads.Conversions.ShouldBeEmpty();
        cut.HasButton("Convert").ShouldBeTrue();
    }
}
