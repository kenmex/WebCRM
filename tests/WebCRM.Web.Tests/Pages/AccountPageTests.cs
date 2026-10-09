using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Lookups;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Accounts;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Pages;

/// <summary>
/// The account detail page driven end to end with a fake service: edit, save, cancel and the
/// "Leave without saving?" guard, which must only fire for changes the user has really not saved.
/// </summary>
public class AccountPageTests : MudTestContext
{
    private const string LeaveDialog = "Leave without saving?";

    private readonly FakeAccountService _accounts = new();
    private readonly NavigationManager _navigation;

    public AccountPageTests()
    {
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<ILookupService>(new FakeLookupService(
            new LookupOption(1, "Prospect", true), new LookupOption(2, "Active", true)));
        Services.AddSingleton<IOwnerService>(new FakeOwnerService(new OwnerOption("sales-1", "Sam Sales", true)));
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();

        _navigation = Services.GetRequiredService<NavigationManager>();
        _accounts.Accounts[7] = new AccountDetail(
            7, "Acme", null, null, null, 1, "Prospect", null, null, "sales-1", "Sam Sales", true,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, [1]);
    }

    private string Path => new Uri(_navigation.Uri).AbsolutePath;

    private IRenderedComponent<AccountPage> RenderNew()
    {
        _navigation.NavigateTo("/accounts/new");
        return Render<AccountPage>();
    }

    private IRenderedComponent<AccountPage> RenderExisting()
    {
        _navigation.NavigateTo("/accounts/7");
        return Render<AccountPage>(p => p.Add(x => x.Id, 7));
    }

    private static void Type(IRenderedComponent<AccountPage> cut, string selector, string text) =>
        cut.WaitForElement(selector).Change(text);

    private static void Press(IRenderedComponent<AccountPage> cut, string buttonText)
    {
        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == buttonText));
        cut.FindAll("button").First(b => b.TextContent.Trim() == buttonText).Click();
    }

    private void PressInDialog(string buttonText)
    {
        DialogProvider.WaitForAssertion(() =>
            DialogProvider.FindAll("button").ShouldContain(b => b.TextContent.Trim() == buttonText));
        DialogProvider.FindAll("button").First(b => b.TextContent.Trim() == buttonText).Click();
    }

    // ---- The reported bug: after saving a new account the guard fired and the page was stuck ----

    [Fact]
    public void Saving_a_new_account_goes_to_its_page_without_the_leave_prompt()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        Press(cut, "Save");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts/42"));
        _accounts.Saves.Single().Name.ShouldBe("Acme Hellas");
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Control_leaving_a_dirty_new_form_does_ask_and_staying_keeps_the_page()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
        PressInDialog("Stay");
        Path.ShouldBe("/accounts/new");
    }

    [Fact]
    public void Cancelling_a_dirty_new_form_asks_once_then_leaves_without_a_second_prompt()
    {
        var cut = RenderNew();
        Type(cut, "input", "Acme Hellas");

        Press(cut, "Cancel");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Discard changes?"));
        PressInDialog("Discard");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Cancelling_a_clean_new_form_leaves_without_any_prompt()
    {
        var cut = RenderNew();

        Press(cut, "Cancel");

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
        DialogProvider.Markup.ShouldNotContain("Discard changes?");
    }

    [Fact]
    public void Saving_an_existing_account_returns_to_read_mode_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "210 123 4567");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Edit"));
        _accounts.Saves.Single().Phone.ShouldBe("210 123 4567");

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void Cancelling_an_edit_of_an_existing_account_discards_it_and_leaving_does_not_prompt()
    {
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "999");

        Press(cut, "Cancel");
        PressInDialog("Discard");

        cut.WaitForAssertion(() => cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Edit"));
        _accounts.Saves.ShouldBeEmpty();

        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));

        cut.WaitForAssertion(() => Path.ShouldBe("/accounts"));
        DialogProvider.Markup.ShouldNotContain(LeaveDialog);
    }

    [Fact]
    public void A_save_the_server_rejects_keeps_the_form_open_and_dirty_and_shows_the_message()
    {
        _accounts.OnSave = _ => new AccountSaveResult(
            AccountSaveStatus.Invalid,
            FieldErrors: new Dictionary<string, string> { [nameof(AccountEditModel.VatNumber)] = VatNumberRules.InvalidMessage });
        var cut = RenderExisting();
        Press(cut, "Edit");
        Type(cut, "input[type=tel]", "210");

        Press(cut, "Save");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain(VatNumberRules.InvalidMessage));
        cut.FindAll("button").ShouldContain(b => b.TextContent.Trim() == "Save");

        // Still dirty, so leaving still asks.
        cut.InvokeAsync(() => _navigation.NavigateTo("/accounts"));
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain(LeaveDialog));
    }
}
