using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Shouldly;
using WebCRM.Core.Personal;
using WebCRM.Core.Querying;
using WebCRM.Core.Search;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Services;
using WebCRM.Web.Tests.TestSupport;
using static WebCRM.Web.Tests.Shared.CrmListPageHost;

namespace WebCRM.Web.Tests.Shared;

/// <summary>
/// The pages and components that answer the keyboard shortcuts: E and Ctrl+S on a record, N on a list, / for the search box.
/// The keys go through the ShortcutRegistry, as the layout's key listener would send them.
/// </summary>
public class ShortcutWiringTests : MudTestContext
{
    private sealed class Form
    {
        [Required]
        public string? Name { get; set; }
    }

    private readonly ShortcutRegistry _shortcuts;
    private readonly NavigationManager _navigation;
    private readonly Form _model = new() { Name = "Acme" };
    private int _edits;
    private int _saves;

    public ShortcutWiringTests()
    {
        Services.AddSingleton<ISearchService>(new FakeSearchService());
        Services.AddSingleton<IFavouriteService>(new FakeFavouriteService());
        Services.AddSingleton<IRecentViewService>(new FakeRecentViewService());
        Services.AddSingleton<IUserContextProvider>(new FakeUserContextProvider());
        StartProviders();
        _shortcuts = Services.GetRequiredService<ShortcutRegistry>();
        _navigation = Services.GetRequiredService<NavigationManager>();
        _navigation.NavigateTo("/accounts");
    }

    private Task PressAsync(string key, bool ctrl = false) => _shortcuts.HandleKeyAsync(new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

    private IRenderedComponent<RecordHeader> RenderHeader(bool editing, bool canEdit = true) =>
        Render<RecordHeader>(p => p
            .Add(x => x.Title, "Acme")
            .Add(x => x.IsEditing, editing)
            .Add(x => x.CanEdit, canEdit)
            .Add(x => x.EditContext, editing ? new EditContext(_model) : null)
            .Add(x => x.OnEdit, () => _edits++)
            .Add(x => x.OnSave, () => _saves++));

    // ---- Record header: E and Ctrl+S

    [Fact]
    public async Task E_starts_editing_in_read_mode()
    {
        RenderHeader(editing: false);

        await PressAsync("e");

        _edits.ShouldBe(1);
    }

    [Fact]
    public async Task E_does_nothing_when_the_user_cannot_edit_or_is_already_editing()
    {
        RenderHeader(editing: false, canEdit: false);
        RenderHeader(editing: true);

        await PressAsync("e");

        _edits.ShouldBe(0);
    }

    [Fact]
    public async Task E_does_nothing_while_typing_in_a_field()
    {
        RenderHeader(editing: false);
        BrowserFocusFake.Typing = true;

        await PressAsync("e");

        _edits.ShouldBe(0);
    }

    [Fact]
    public async Task Ctrl_S_saves_a_valid_form_after_leaving_the_field()
    {
        RenderHeader(editing: true);
        BrowserFocusFake.Typing = true; // typing in a field does not stop Ctrl+S

        await PressAsync("s", ctrl: true);

        _saves.ShouldBe(1);
        BrowserFocusFake.Blurs.ShouldBe(1);
    }

    [Fact]
    public async Task Ctrl_S_does_not_save_an_invalid_form()
    {
        _model.Name = null;
        RenderHeader(editing: true);

        await PressAsync("s", ctrl: true);

        _saves.ShouldBe(0);
    }

    [Fact]
    public async Task Ctrl_S_does_nothing_in_read_mode()
    {
        RenderHeader(editing: false);

        await PressAsync("s", ctrl: true);

        _saves.ShouldBe(0);
    }

    [Fact]
    public async Task Switching_between_reading_and_editing_moves_the_shortcuts_with_it()
    {
        var cut = RenderHeader(editing: false);
        _shortcuts.HasHandler(Shortcut.Edit).ShouldBeTrue();
        _shortcuts.HasHandler(Shortcut.Save).ShouldBeFalse();

        cut.Render(p => p.Add(x => x.IsEditing, true).Add(x => x.EditContext, new EditContext(_model)));

        _shortcuts.HasHandler(Shortcut.Edit).ShouldBeFalse();
        _shortcuts.HasHandler(Shortcut.Save).ShouldBeTrue();

        await DisposeComponentsAsync();
        _shortcuts.HasHandler(Shortcut.Save).ShouldBeFalse();
    }

    // ---- List page: N

    [Fact]
    public async Task N_on_a_list_opens_the_New_form()
    {
        Render<CrmListPageHost>(p => p.Add(x => x.Load, (_, _) => Task.FromResult(PagedResult<Row>.Empty)));

        await PressAsync("n");

        new Uri(_navigation.Uri).AbsolutePath.ShouldBe("/accounts/new");
    }

    // ---- Search box: /

    [Fact]
    public async Task Slash_focuses_the_search_box()
    {
        var cut = Render<GlobalSearch>();
        cut.WaitForAssertion(() => cut.FindAll("input").Count.ShouldBe(1));
        _shortcuts.HasHandler(Shortcut.FocusSearch).ShouldBeTrue();

        await PressAsync("/");

        // MudBlazor focuses its field through its own JS call.
        JSInterop.Invocations.ShouldContain(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Slash_on_a_phone_opens_the_search_bar()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var cut = Render<GlobalSearch>();
        cut.WaitForAssertion(() => cut.FindAll("button[aria-label=Search]").Count.ShouldBe(1));
        cut.FindAll("input").ShouldBeEmpty();

        await PressAsync("/");

        cut.WaitForAssertion(() => cut.FindAll("input").Count.ShouldBe(1));
    }

    [Fact]
    public async Task The_search_box_gives_up_the_shortcut_when_it_goes_away()
    {
        Render<GlobalSearch>();
        _shortcuts.HasHandler(Shortcut.FocusSearch).ShouldBeTrue();

        await DisposeComponentsAsync();

        _shortcuts.HasHandler(Shortcut.FocusSearch).ShouldBeFalse();
    }
}
