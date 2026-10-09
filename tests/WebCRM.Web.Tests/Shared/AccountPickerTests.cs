using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Users;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class AccountPickerTests : MudTestContext
{
    private readonly FakeAccountService _accounts = new();

    public AccountPickerTests()
    {
        _accounts.Picker.AddRange(
        [
            new AccountPickerItem(1, "Acme Hellas"),
            new AccountPickerItem(2, "Acme Retail"),
            new AccountPickerItem(3, "Beta Ltd"),
        ]);
        Services.AddSingleton<IAccountService>(_accounts);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
    }

    private static string InputText(IRenderedComponent<AccountPickerHost> cut) => cut.Find("input").GetAttribute("value") ?? string.Empty;

    [Fact]
    public void It_shows_the_name_of_the_account_it_was_given()
    {
        var cut = Render<AccountPickerHost>(p => p.Add(x => x.InitialId, 2));

        cut.WaitForAssertion(() => InputText(cut).ShouldBe("Acme Retail"));
    }

    [Fact]
    public void It_is_empty_without_a_value()
    {
        var cut = Render<AccountPickerHost>();

        InputText(cut).ShouldBeEmpty();
    }

    [Fact]
    public void Typing_searches_after_the_debounce_and_lists_the_matching_accounts()
    {
        var cut = Render<AccountPickerHost>();

        cut.Find("input").Input("acme");

        cut.WaitForAssertion(() =>
        {
            _accounts.PickerSearches.ShouldContain("acme");
            DialogOrPopoverMarkup(cut).ShouldContain("Acme Hellas");
        });
        DialogOrPopoverMarkup(cut).ShouldContain("Acme Retail");
        DialogOrPopoverMarkup(cut).ShouldNotContain("Beta Ltd");
    }

    [Fact]
    public void Choosing_a_result_sets_the_value_and_marks_the_form_edited()
    {
        var cut = Render<AccountPickerHost>();
        cut.Find("input").Input("beta");
        cut.WaitForAssertion(() => Items().ShouldContain(i => i.TextContent.Trim() == "Beta Ltd"));

        Items().First(i => i.TextContent.Trim() == "Beta Ltd").Click();

        cut.WaitForAssertion(() => cut.Instance.Model.AccountId.ShouldBe(3));
        cut.Instance.Context.IsModified().ShouldBeTrue();
        cut.WaitForAssertion(() => InputText(cut).ShouldBe("Beta Ltd"));
    }

    [Fact]
    public void A_required_picker_shows_the_forms_validation_message()
    {
        var cut = Render<AccountPickerHost>();

        cut.InvokeAsync(() => cut.Instance.Context.Validate());

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Choose an account."));
    }

    [Fact]
    public void A_disabled_picker_cannot_be_typed_into()
    {
        var cut = Render<AccountPickerHost>(p => p.Add(x => x.InitialId, 1).Add(x => x.Disabled, true));

        cut.Find("input").HasAttribute("disabled").ShouldBeTrue();
    }

    private IReadOnlyList<AngleSharp.Dom.IElement> Items() => [.. PopoverProvider.FindAll(".mud-list-item")];

    private string DialogOrPopoverMarkup(IRenderedComponent<AccountPickerHost> cut) =>
        cut.Markup + PopoverProvider.Markup;
}
