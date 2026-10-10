using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Services;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

/// <summary>The Ctrl+K palette and the component that listens for the keys.</summary>
public class CommandPaletteTests : MudTestContext
{
    private const string BoxSelector = "input[aria-label='Command palette']";

    private readonly FakeSearchService _search = new();
    private readonly FakeFavouriteService _favourites = new();
    private readonly FakeRecentViewService _recents = new();
    private readonly NavigationManager _navigation;

    public CommandPaletteTests()
    {
        Services.AddSingleton<ISearchService>(_search);
        Services.AddSingleton<IFavouriteService>(_favourites);
        Services.AddSingleton<IRecentViewService>(_recents);
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/");
    }

    private string Uri => new Uri(_navigation.Uri).PathAndQuery;

    private IDialogService Dialogs => Services.GetRequiredService<IDialogService>();

    private async Task OpenPaletteAsync()
    {
        await Dialogs.ShowAsync<CommandPalette>(string.Empty, new DialogOptions { NoHeader = true });
        DialogProvider.WaitForElement(BoxSelector);
    }

    private AngleSharp.Dom.IElement Box => DialogProvider.Find(BoxSelector);

    private IReadOnlyList<string> Items() =>
        [.. DialogProvider.FindAll("[data-palette-item]").Select(e => e.GetAttribute("data-palette-item")!)];

    private static SearchHit Hit(SearchEntity type, int id, string title) => new(type, id, title, null);

    private static KeyboardEventArgs Press(string key) => new() { Key = key };

    // ---- Content

    [Fact]
    public async Task Empty_it_offers_the_actions_favourites_and_recently_viewed()
    {
        _favourites.Listed.Add(Hit(SearchEntity.Account, 3, "Starred Co"));
        _recents.Listed.Add(Hit(SearchEntity.Contact, 9, "Recent Person"));

        await OpenPaletteAsync();

        DialogProvider.WaitForAssertion(() => Items().ShouldBe(
            ["accounts/new", "contacts/new", "leads/new", "accounts", "contacts", "leads", string.Empty, "accounts/3", "contacts/9"]));
        DialogProvider.Markup.ShouldContain("Actions");
        DialogProvider.Markup.ShouldContain("Favourites");
        DialogProvider.Markup.ShouldContain("Recently viewed");
        _search.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Typing_searches_records_first_then_the_matching_actions()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2, contacts: 1));
        await OpenPaletteAsync();

        Box.Input("account");

        DialogProvider.WaitForAssertion(() => Items().ShouldBe(
            ["accounts/1", "accounts/2", "contacts/101", "accounts/new", "accounts"]), TimeSpan.FromSeconds(3));
        _search.Calls.ShouldBe([("account", 5)]);
    }

    [Fact]
    public async Task One_character_filters_the_actions_without_searching()
    {
        await OpenPaletteAsync();

        Box.Input("n");

        DialogProvider.WaitForAssertion(() => Items().ShouldBe(["accounts/new", "contacts/new", "leads/new", "accounts", "contacts"]));
        Thread.Sleep(300);
        _search.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nothing_matching_says_so()
    {
        await OpenPaletteAsync();

        Box.Input("zzzzzz");

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Nothing found."), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_failing_search_keeps_the_actions_and_says_so()
    {
        _search.OnSearch = (_, _) => throw new InvalidOperationException("db down");
        await OpenPaletteAsync();

        Box.Input("account");

        DialogProvider.WaitForAssertion(
            () =>
            {
                DialogProvider.Markup.ShouldContain("Search failed");
                Items().ShouldContain("accounts/new");
            },
            TimeSpan.FromSeconds(3));
    }

    // ---- Choosing

    [Fact]
    public async Task Enter_opens_the_top_result()
    {
        _search.OnSearch = (text, _) => Task.FromResult(FakeSearchService.Results(text, accounts: 2));
        await OpenPaletteAsync();
        Box.Input("acme");
        DialogProvider.WaitForAssertion(() => Items().ShouldContain("accounts/1"), TimeSpan.FromSeconds(3));

        Box.KeyDown(Press("Enter"));

        DialogProvider.WaitForAssertion(() => Uri.ShouldBe("/accounts/1"));
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("input").ShouldBeEmpty());
    }

    [Fact]
    public async Task Arrow_keys_move_the_highlight_and_Enter_opens_that_entry()
    {
        await OpenPaletteAsync();
        DialogProvider.WaitForAssertion(() => Items().Count.ShouldBe(7));

        Box.KeyDown(Press("ArrowDown"));
        Box.KeyDown(Press("ArrowDown"));
        Box.KeyDown(Press("Enter"));

        Uri.ShouldBe("/leads/new");
    }

    [Fact]
    public async Task ArrowUp_from_the_top_wraps_to_the_last_entry()
    {
        await OpenPaletteAsync();
        DialogProvider.WaitForAssertion(() => Items().Count.ShouldBe(7));

        Box.KeyDown(Press("ArrowUp"));
        Box.KeyDown(Press("Enter"));

        Uri.ShouldBe("/");
    }

    [Fact]
    public async Task Clicking_an_entry_opens_it()
    {
        await OpenPaletteAsync();
        DialogProvider.WaitForAssertion(() => Items().ShouldContain("contacts/new"));

        DialogProvider.Find("[data-palette-item='contacts/new']").Click();

        Uri.ShouldBe("/contacts/new");
    }

    [Fact]
    public async Task Enter_with_nothing_listed_does_nothing()
    {
        await OpenPaletteAsync();
        Box.Input("zzzzzz");
        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Nothing found."), TimeSpan.FromSeconds(3));

        Box.KeyDown(Press("Enter"));

        Uri.ShouldBe("/");
    }

    // ---- Opening it from the keyboard

    [Fact]
    public async Task Ctrl_K_opens_the_palette_once_even_when_pressed_twice()
    {
        var registry = Services.GetRequiredService<ShortcutRegistry>();
        Render<KeyboardShortcuts>();
        registry.HasHandler(Shortcut.Palette).ShouldBeTrue();

        await registry.HandleKeyAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        await registry.HandleKeyAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });

        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll(BoxSelector).Count.ShouldBe(1));
    }

    [Fact]
    public async Task The_palette_can_be_opened_again_after_it_was_closed()
    {
        var registry = Services.GetRequiredService<ShortcutRegistry>();
        Render<KeyboardShortcuts>();
        await registry.HandleKeyAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });
        DialogProvider.WaitForElement(BoxSelector);
        DialogProvider.Find("[data-palette-item='accounts']").Click();
        DialogProvider.WaitForAssertion(() => DialogProvider.FindAll("input").ShouldBeEmpty());

        // The listener learns that the dialog closed a moment after the dialog is gone, so press again until it opens.
        DialogProvider.WaitForAssertion(() =>
        {
            registry.HandleKeyAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true }).GetAwaiter().GetResult();
            DialogProvider.FindAll(BoxSelector).Count.ShouldBe(1);
        });
    }


    [Fact]
    public void The_key_listener_is_attached_to_the_page_body_so_keys_with_nothing_focused_are_seen()
    {
        Render<KeyboardShortcuts>();

        // MudBlazor's interceptor connects to an element by id. Keys pressed with nothing focused are aimed at <body>,
        // so the id must be the body's (App.razor sets it from the same constant); an element inside the layout would miss them.
        JSInterop.Invocations
            .Where(i => i.Identifier.Contains("mudKeyInterceptor.connect", StringComparison.Ordinal))
            .ShouldContain(i => i.Arguments.Contains(KeyboardShortcuts.ShellElementId));
        KeyboardShortcuts.ShellElementId.ShouldBe("crm-body");
    }
    [Fact]
    public async Task The_shortcut_listener_removes_its_handler_when_it_goes_away()
    {
        var registry = Services.GetRequiredService<ShortcutRegistry>();
        Render<KeyboardShortcuts>();

        await DisposeComponentsAsync();

        registry.HasHandler(Shortcut.Palette).ShouldBeFalse();
    }
}

public class PaletteCatalogTests
{
    [Theory]
    [InlineData("", 7)]
    [InlineData("new", 3)]
    [InlineData("NEW ACC", 1)]
    [InlineData("go contacts", 1)]
    [InlineData("contacts", 1)]
    [InlineData("contact", 2)]
    [InlineData("zzz", 0)]
    public void Actions_match_when_every_typed_word_is_in_the_title(string text, int expected)
    {
        PaletteCatalog.Match(text).Count.ShouldBe(expected);
    }
}
