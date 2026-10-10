using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Records;
using WebCRM.Core.Lookups;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Contacts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The contact detail page (P11) driven with fake services: create, edit, the phone layout with its Call and Email
/// buttons, the duplicate-email warning, and the "Leave without saving?" guard, which must not fire after a save.
/// </summary>
public class ContactPageTests : MudTestContext
{
    private const string LeaveDialog = "Leave without saving?";

    private readonly FakeContactService _contacts = new();
    private readonly FakeAccountService _accounts = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public ContactPageTests()
    {
        _accounts.Picker.Add(new AccountPickerItem(5, "Acme Hellas"));
        _contacts.AccountOwners[5] = "owner-2";
        Services.AddSingleton<IContactService>(_contacts);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(
            new OwnerOption("sales-1", "Sam Sales", true), new OwnerOption("owner-2", "Olga Owner", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Mr", true), new LookupOption(2, "Ms", true), new LookupOption(3, "Dr", true)));
        StartProviders();

        _navigation = Services.GetRequiredService<NavigationManager>();
        _contacts.Contacts[7] = Detail(phone: "+30 210 123 4567", mobile: "694 000 1111", email: "anna@example.com");
    }

    private static ContactDetail Detail(string? phone = null, string? mobile = null, string? email = null) =>
        new(7, "Anna", "Smith", "Anna Smith", 5, "Acme Hellas", "Buyer", email, phone, mobile, "owner-2", "Olga Owner", true,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    private IRenderedComponent<ContactPage> RenderNew(int? accountId = null)
    {
        // The page reads ?account= from the URL, so the test navigates there first.
        _navigation.NavigateTo(accountId is { } id ? $"/contacts/new?account={id}" : "/contacts/new");
        return Render<ContactPage>();
    }

    private IRenderedComponent<ContactPage> RenderExisting()
    {
        _navigation.NavigateTo("/contacts/7");
        return Render<ContactPage>(p => p.Add(x => x.Id, 7));
    }

    private static void Press(IRenderedComponent<ContactPage> cut, string text)
    {
        cut.WaitForAssertion(() => cut.HasButton(text).ShouldBeTrue());
        cut.ButtonByText(text).Click();
    }

    private void PressInDialog(string text)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == text));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == text).Click();
    }

    // ---- Create: save on /new must not trigger the NavigationLock ----

    [Fact]
    public void Saving_a_new_contact_goes_to_its_page_without_the_leave_prompt()
    {
        var cut = RenderNew(accountId: 5);
        cut.WaitForAssertion(() => cut.InputByLabel("Last name"));
        cut.InputByLabel("Last name").Change("Smith");

        Press(cut, "Save");

        cut.WaitForAssertion(() => Path.ShouldBe("/contacts/42"));
        _contacts.Saves.Single().Model.LastName.ShouldBe("Smith");
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Control_leaving_a_dirty_new_form_does_ask()
    {
        var cut = RenderNew(accountId: 5);
        cut.WaitForAssertion(() => cut.InputByLabel("Last name"));
        cut.InputByLabel("Last name").Change("Smith");

        cut.InvokeAsync(() => _navigation.NavigateTo("/contacts"));

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
        PressInDialog("Stay");
        Path.ShouldBe("/contacts/new");
    }

    [Fact]
    public void Cancelling_a_dirty_new_form_asks_once_then_leaves_without_a_second_prompt()
    {
        var cut = RenderNew(accountId: 5);
        cut.WaitForAssertion(() => cut.InputByLabel("Last name"));
        cut.InputByLabel("Last name").Change("Smith");

        Press(cut, "Cancel");
        PressInDialog("Discard");

        cut.WaitForAssertion(() => Path.ShouldBe("/contacts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_new_contact_started_from_an_account_is_prefilled_with_the_account_and_its_owner()
    {
        var cut = RenderNew(accountId: 5);

        cut.WaitForAssertion(() => cut.InputByLabel("Account").GetAttribute("value").ShouldBe("Acme Hellas"));
        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Olga Owner"));
        _contacts.NewCalls.ShouldBe([5]);
    }

    [Fact]
    public void A_new_contact_without_an_account_shows_an_empty_required_picker_and_will_not_save_without_one()
    {
        var cut = RenderNew();
        cut.WaitForAssertion(() => cut.InputByLabel("Last name"));

        cut.InputByLabel("Account").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.InputByLabel("Account").HasAttribute("required").ShouldBeTrue();

        cut.InputByLabel("Last name").Change("Smith");
        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Choose an account."));
        _contacts.Saves.ShouldBeEmpty();
    }

    [Fact]
    public void A_new_contact_without_an_account_starts_with_the_current_user_as_owner()
    {
        var cut = RenderNew();

        cut.WaitForAssertion(() => cut.InputByLabel("Owner").GetAttribute("value").ShouldBe("Sam Sales"));
        _contacts.NewCalls.ShouldBe([null]);
    }

    // ---- Edit, warnings, conflicts ----

    [Fact]
    public void Saving_an_existing_contact_returns_to_read_mode_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Job title"));
        cut.InputByLabel("Job title").Change("Head of purchasing");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.HasButton("Edit").ShouldBeTrue());
        _contacts.Saves.Single().Model.JobTitle.ShouldBe("Head of purchasing");

        cut.InvokeAsync(() => _navigation.NavigateTo("/contacts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/contacts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_duplicate_email_warning_offers_Save_anyway_which_saves_with_the_warning_accepted()
    {
        var first = true;
        _contacts.OnSave = _ =>
        {
            var warn = first;
            first = false;
            return warn
                ? new SaveResult(SaveStatus.Warning, Warnings: ["Anna Twin (Bob Co)"], WarningMessage: "A contact with this email already exists")
                : new SaveResult(SaveStatus.Saved, Id: 7);
        };
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Email"));
        cut.InputByLabel("Email").Change("twin@example.com");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("A contact with this email already exists: Anna Twin (Bob Co)."));
        _contacts.Saves.Single().Options.AcceptWarnings.ShouldBeFalse();

        Press(cut, "Save anyway");

        cut.WaitForAssertion(() => _contacts.Saves.Count.ShouldBe(2));
        _contacts.Saves[1].Options.AcceptWarnings.ShouldBeTrue();
        cut.WaitForAssertion(() => cut.HasButton("Edit").ShouldBeTrue());
    }

    [Fact]
    public void A_conflict_names_the_other_user_and_offers_Reload()
    {
        _contacts.OnSave = _ => new SaveResult(
            SaveStatus.Conflict, Conflict: new ConcurrencyConflict("Maria", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)));
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Job title"));
        cut.InputByLabel("Job title").Change("Boss");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Changed by Maria"));
        cut.HasButton("Reload (lose my changes)").ShouldBeTrue();
        cut.HasButton("Overwrite with my changes").ShouldBeFalse(); // not an Admin
    }

    // ---- Read mode: links and the phone layout ----

    [Fact]
    public void Read_mode_shows_the_account_link_and_tel_and_mailto_links()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Anna Smith"));
        cut.Find("a[href='accounts/5']").TextContent.ShouldBe("Acme Hellas");
        cut.Find("a.mud-link[href='mailto:anna@example.com']").TextContent.ShouldBe("anna@example.com");
        cut.Find("a.mud-link[href='tel:+302101234567']").TextContent.ShouldBe("+30 210 123 4567");
        cut.Find("a.mud-link[href='tel:6940001111']").TextContent.ShouldBe("694 000 1111");
    }

    [Fact]
    public void The_phone_layout_has_large_full_width_Call_and_Email_buttons_above_the_details()
    {
        var cut = RenderExisting();
        cut.WaitForAssertion(() => cut.FindAll("[data-phone-actions]").Count.ShouldBe(1));

        var block = cut.Find("[data-phone-actions]");
        block.ClassList.ShouldContain("d-md-none"); // shown below md only

        var call = block.QuerySelector("a[href='tel:+302101234567']")!;
        var email = block.QuerySelector("a[href='mailto:anna@example.com']")!;
        call.TextContent.Trim().ShouldBe("Call");
        email.TextContent.Trim().ShouldBe("Email");
        call.ClassList.ShouldContain("mud-button-filled-size-large");
        call.ClassList.ShouldContain("mud-width-full");

        // The block comes before the details in the page.
        var markup = cut.Markup;
        markup.IndexOf("data-phone-actions", StringComparison.Ordinal)
            .ShouldBeLessThan(markup.IndexOf("Job title", StringComparison.Ordinal));
    }

    [Fact]
    public void On_desktop_the_same_actions_are_small_buttons_in_the_header()
    {
        var cut = RenderExisting();
        cut.WaitForAssertion(() => cut.HasButton("Edit").ShouldBeTrue());

        var desktop = cut.FindAll("a.d-none.d-md-inline-flex");
        desktop.Select(a => a.TextContent.Trim()).ShouldBe(["Call", "Email"]);
    }

    [Fact]
    public void Call_uses_the_mobile_number_when_there_is_no_phone()
    {
        _contacts.Contacts[7] = Detail(phone: null, mobile: "694 000 1111", email: "anna@example.com");
        var cut = RenderExisting();
        cut.WaitForAssertion(() => cut.FindAll("[data-phone-actions]").Count.ShouldBe(1));

        cut.Find("[data-phone-actions]").QuerySelector("a[href='tel:6940001111']")!.TextContent.Trim().ShouldBe("Call");
    }

    [Fact]
    public void Without_a_number_or_an_email_there_are_no_Call_or_Email_buttons()
    {
        _contacts.Contacts[7] = Detail();
        var cut = RenderExisting();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Anna Smith"));

        cut.FindAll("[data-phone-actions]").ShouldBeEmpty();
        cut.HasButton("Call").ShouldBeFalse();
        cut.HasButton("Email").ShouldBeFalse();
    }

    // ---- Delete ----

    [Fact]
    public void Delete_asks_first_then_deletes_and_returns_to_the_list()
    {
        var cut = RenderExisting();
        Press(cut, "Delete");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Delete contact?"));
        PressInDialog("Delete");

        cut.WaitForAssertion(() => Path.ShouldBe("/contacts"));
        _contacts.Deleted.ShouldBe([7]);
    }
    // ---- Salutation, department, Do not contact ----

    [Fact]
    public void A_do_not_contact_contact_shows_a_warning_chip_with_the_date_in_the_header()
    {
        _contacts.Contacts[7] = Detail(email: "anna@example.com") with
        {
            DoNotContact = true,
            DoNotContactSince = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
        };

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.FindAll("[data-do-not-contact]").Count.ShouldBe(1));
        var chip = cut.Find("[data-do-not-contact]");
        chip.TextContent.Trim().ShouldBe("Do not contact");
        chip.ClassList.ShouldContain("mud-chip-color-warning");
        chip.GetAttribute("title").ShouldBe("Since 9 Oct 2026 15:00"); // shown in Athens time
    }

    [Fact]
    public void A_contact_who_can_be_contacted_has_no_warning_chip()
    {
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Anna Smith"));
        cut.FindAll("[data-do-not-contact]").ShouldBeEmpty();
    }

    [Fact]
    public void Read_mode_shows_the_salutation_and_the_department()
    {
        _contacts.Contacts[7] = Detail() with { SalutationName = "Dr", Department = "Purchasing" };

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Purchasing"));
        cut.Markup.ShouldContain("Salutation");
        cut.Markup.ShouldContain("Department");
        cut.FindAll(".mud-typography-body1").Select(e => e.TextContent.Trim()).ShouldContain("Dr");
    }

    [Fact]
    public void The_form_has_a_salutation_a_department_and_a_Do_not_contact_switch_that_are_saved()
    {
        _contacts.Contacts[7] = Detail() with { SalutationId = 3, SalutationName = "Dr", Department = "Sales" };
        var cut = RenderExisting();
        Press(cut, "Edit");
        cut.WaitForAssertion(() => cut.InputByLabel("Department").GetAttribute("value").ShouldBe("Sales"));
        cut.WaitForAssertion(() => cut.InputByLabel("Salutation").GetAttribute("value").ShouldBe("Dr"));

        cut.InputByLabel("Department").Change("Purchasing");
        cut.Find("input[type=checkbox]").Change(true);
        Press(cut, "Save");

        cut.WaitForAssertion(() => _contacts.Saves.Count.ShouldBe(1));
        var saved = _contacts.Saves.Single().Model;
        saved.Department.ShouldBe("Purchasing");
        saved.SalutationId.ShouldBe(3);
        saved.DoNotContact.ShouldBeTrue();
    }

    [Fact]
    public void A_Do_not_contact_contact_keeps_the_switch_on_when_edited()
    {
        _contacts.Contacts[7] = Detail() with { DoNotContact = true, DoNotContactSince = DateTime.UtcNow };
        var cut = RenderExisting();
        Press(cut, "Edit");

        cut.WaitForAssertion(() => cut.Find("input[type=checkbox]").HasAttribute("checked").ShouldBeTrue());
    }

    [Fact]
    public void Opening_the_page_records_a_recent_view_but_the_new_form_does_not()
    {
        RenderNew();
        _recents.Recorded.ShouldBeEmpty();

        var cut = RenderExisting();
        cut.WaitForAssertion(() => _recents.Recorded.ShouldBe([(SearchEntity.Contact, 7)]));
    }

    [Fact]
    public void A_failing_recent_view_write_does_not_stop_the_page_from_showing()
    {
        _recents.FailWith = new InvalidOperationException("db down");

        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldNotBeNullOrWhiteSpace());
    }

    [Fact]
    public void The_star_shows_the_saved_state_and_toggles_both_ways()
    {
        _favourites.Starred.Add((SearchEntity.Contact, 7));
        var cut = RenderExisting();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']").GetAttribute("aria-pressed").ShouldBe("true"));

        cut.Find("button[aria-label='Remove from favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']"));
        cut.Find("button[aria-label='Add to favourites']").Click();
        cut.WaitForAssertion(() => cut.Find("button[aria-label='Remove from favourites']"));

        _favourites.SetCalls.ShouldBe([(SearchEntity.Contact, 7, false), (SearchEntity.Contact, 7, true)]);
    }

    [Fact]
    public void The_star_goes_back_when_saving_it_fails()
    {
        var cut = RenderExisting();
        cut.WaitForElement("button[aria-label='Add to favourites']");
        _favourites.FailWith = new InvalidOperationException("db down");

        cut.Find("button[aria-label='Add to favourites']").Click();

        cut.WaitForAssertion(() => cut.Find("button[aria-label='Add to favourites']").GetAttribute("aria-pressed").ShouldBe("false"));
    }

    [Fact]
    public void There_is_no_star_on_the_new_form()
    {
        var cut = RenderNew();

        cut.WaitForElement("h1");
        cut.FindAll("button[aria-label$='favourites']").ShouldBeEmpty();
    }
}
