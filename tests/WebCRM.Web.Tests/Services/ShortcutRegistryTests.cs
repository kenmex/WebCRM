using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using WebCRM.Web.Services;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Services;

public class ShortcutRegistryTests
{
    private readonly FakeBrowserFocus _focus = new();
    private readonly ShortcutRegistry _registry;
    private readonly List<Shortcut> _ran = [];

    public ShortcutRegistryTests() => _registry = new ShortcutRegistry(_focus);

    private IDisposable On(Shortcut shortcut) => _registry.Register(shortcut, () =>
    {
        _ran.Add(shortcut);
        return Task.CompletedTask;
    });

    private static KeyboardEventArgs Key(string key, string? code = null, bool ctrl = false, bool meta = false, bool alt = false, bool shift = false) =>
        new() { Key = key, Code = code ?? string.Empty, CtrlKey = ctrl, MetaKey = meta, AltKey = alt, ShiftKey = shift };

    [Theory]
    [InlineData("k", true, false, Shortcut.Palette)]
    [InlineData("K", true, false, Shortcut.Palette)]
    [InlineData("k", false, true, Shortcut.Palette)]
    [InlineData("s", true, false, Shortcut.Save)]
    [InlineData("s", false, true, Shortcut.Save)]
    [InlineData("/", false, false, Shortcut.FocusSearch)]
    [InlineData("e", false, false, Shortcut.Edit)]
    [InlineData("E", false, false, Shortcut.Edit)]
    [InlineData("n", false, false, Shortcut.New)]
    public async Task Each_shortcut_runs_the_handler_registered_for_it(string key, bool ctrl, bool meta, Shortcut expected)
    {
        foreach (var shortcut in Enum.GetValues<Shortcut>())
        {
            On(shortcut);
        }

        (await _registry.HandleKeyAsync(Key(key, ctrl: ctrl, meta: meta))).ShouldBeTrue();

        _ran.ShouldBe([expected]);
    }

    [Theory]
    [InlineData("ε", "KeyE", Shortcut.Edit)]
    [InlineData("ν", "KeyN", Shortcut.New)]
    [InlineData("κ", "KeyK", Shortcut.Palette)]
    [InlineData("σ", "KeyS", Shortcut.Save)]
    public async Task The_shortcuts_work_on_a_Greek_keyboard_layout(string key, string code, Shortcut expected)
    {
        On(expected);
        var ctrl = expected is Shortcut.Palette or Shortcut.Save;

        (await _registry.HandleKeyAsync(Key(key, code, ctrl: ctrl))).ShouldBeTrue();

        _ran.ShouldBe([expected]);
    }

    [Theory]
    [InlineData("a", false, false)]
    [InlineData("e", true, false)] // Ctrl+E is not Edit
    [InlineData("n", false, true)] // Cmd+N is the browser's
    [InlineData("k", false, false)] // K alone is just a letter
    [InlineData("s", false, false)]
    [InlineData("/", true, false)]
    public async Task Other_keys_do_nothing(string key, bool ctrl, bool meta)
    {
        foreach (var shortcut in Enum.GetValues<Shortcut>())
        {
            On(shortcut);
        }

        (await _registry.HandleKeyAsync(Key(key, ctrl: ctrl, meta: meta))).ShouldBeFalse();

        _ran.ShouldBeEmpty();
    }

    [Fact]
    public async Task Alt_combinations_are_left_to_the_browser()
    {
        On(Shortcut.Palette);

        (await _registry.HandleKeyAsync(Key("k", ctrl: true, alt: true))).ShouldBeFalse();
    }

    [Fact]
    public async Task A_shortcut_nobody_registered_does_nothing()
    {
        (await _registry.HandleKeyAsync(Key("e"))).ShouldBeFalse();
        _focus.Blurs.ShouldBe(0);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("e")]
    [InlineData("n")]
    public async Task Letter_shortcuts_are_ignored_while_the_user_types_in_a_field_or_has_a_dialog_open(string key)
    {
        On(Shortcut.FocusSearch);
        On(Shortcut.Edit);
        On(Shortcut.New);
        _focus.Typing = true;

        (await _registry.HandleKeyAsync(Key(key))).ShouldBeFalse();

        _ran.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ctrl_combinations_work_even_while_typing()
    {
        On(Shortcut.Palette);
        On(Shortcut.Save);
        _focus.Typing = true;

        (await _registry.HandleKeyAsync(Key("k", ctrl: true))).ShouldBeTrue();
        (await _registry.HandleKeyAsync(Key("s", ctrl: true))).ShouldBeTrue();

        _ran.ShouldBe([Shortcut.Palette, Shortcut.Save]);
    }

    [Fact]
    public async Task Saving_first_leaves_the_field_so_its_last_edit_is_committed()
    {
        var order = new List<string>();
        _registry.Register(Shortcut.Save, () =>
        {
            order.Add($"save after {_focus.Blurs} blur");
            return Task.CompletedTask;
        });

        await _registry.HandleKeyAsync(Key("s", ctrl: true));

        order.ShouldBe(["save after 1 blur"]);
    }

    [Fact]
    public async Task The_handler_registered_last_wins_and_the_one_before_returns_when_it_is_disposed()
    {
        var first = new List<string>();
        _registry.Register(Shortcut.Edit, () => { first.Add("first"); return Task.CompletedTask; });
        var second = _registry.Register(Shortcut.Edit, () => { first.Add("second"); return Task.CompletedTask; });

        await _registry.HandleKeyAsync(Key("e"));
        second.Dispose();
        await _registry.HandleKeyAsync(Key("e"));

        first.ShouldBe(["second", "first"]);
    }

    [Fact]
    public async Task A_disposed_registration_no_longer_runs()
    {
        var registration = On(Shortcut.New);
        registration.Dispose();

        (await _registry.HandleKeyAsync(Key("n"))).ShouldBeFalse();
        _registry.HasHandler(Shortcut.New).ShouldBeFalse();
    }
}
