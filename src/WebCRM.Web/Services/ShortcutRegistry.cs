using Microsoft.AspNetCore.Components.Web;

namespace WebCRM.Web.Services;

public enum Shortcut
{
    /// <summary>Ctrl+K (Cmd+K): the command palette.</summary>
    Palette,

    /// <summary>/ : jump to the search box.</summary>
    FocusSearch,

    /// <summary>Ctrl+S (Cmd+S): save the form being edited.</summary>
    Save,

    /// <summary>E : edit the record on the page.</summary>
    Edit,

    /// <summary>N : new record of the kind the list page shows.</summary>
    New,
}

/// <summary>
/// Who answers which keyboard shortcut right now (docs/page-spec.md, "Keyboard first"). The layout feeds key presses in;
/// components that can act on a shortcut (the record header, the list page, the search box) register while they can,
/// and the one registered last wins. A shortcut nobody has registered does nothing.
/// One per circuit (scoped), like every other service that holds UI state.
/// </summary>
public sealed class ShortcutRegistry(IBrowserFocus focus)
{
    /// <summary>
    /// How long Ctrl+S waits after leaving the field being typed in, so the field's own "changed" event reaches the
    /// server before the form is saved. Without it the save could miss the last edit.
    /// </summary>
    internal static readonly TimeSpan CommitDelay = TimeSpan.FromMilliseconds(75);

    private readonly Lock _gate = new();
    private readonly List<Registration> _handlers = [];

    public IDisposable Register(Shortcut shortcut, Func<Task> handler)
    {
        var registration = new Registration(shortcut, handler, this);
        lock (_gate)
        {
            _handlers.Add(registration);
        }

        return registration;
    }

    public bool HasHandler(Shortcut shortcut)
    {
        lock (_gate)
        {
            return _handlers.Any(h => h.Shortcut == shortcut);
        }
    }

    /// <summary>
    /// Runs the handler for a key press, if the key is a shortcut and someone is registered for it. Returns whether one ran.
    /// Letter shortcuts (/, E, N) are ignored while the user is typing in a field or has a dialog or menu open;
    /// Ctrl/Cmd combinations work everywhere, as in every other app.
    /// </summary>
    public async Task<bool> HandleKeyAsync(KeyboardEventArgs e)
    {
        if (Match(e) is not { } shortcut || Find(shortcut) is not { } handler)
        {
            return false;
        }

        if (shortcut is Shortcut.FocusSearch or Shortcut.Edit or Shortcut.New && await focus.IsTypingOrInDialogAsync())
        {
            return false;
        }

        if (shortcut == Shortcut.Save)
        {
            await focus.BlurActiveElementAsync();
            await Task.Delay(CommitDelay);
        }

        await handler();
        return true;
    }

    /// <summary>
    /// Which shortcut a key press is. Matches the physical key (Code) first, so the shortcuts work on a Greek keyboard
    /// layout too, and falls back to the typed character.
    /// </summary>
    internal static Shortcut? Match(KeyboardEventArgs e)
    {
        if (e.AltKey)
        {
            return null;
        }

        var letter = e.Code switch
        {
            "KeyK" => 'k',
            "KeyS" => 's',
            "KeyE" => 'e',
            "KeyN" => 'n',
            "Slash" => '/',
            _ => (char?)null,
        } ?? (e.Key is { Length: 1 } key ? char.ToLowerInvariant(key[0]) : (char?)null);

        var command = e.CtrlKey || e.MetaKey;
        return (command, letter) switch
        {
            (true, 'k') => Shortcut.Palette,
            (true, 's') => Shortcut.Save,
            (false, '/') => Shortcut.FocusSearch,
            (false, 'e') => Shortcut.Edit,
            (false, 'n') => Shortcut.New,
            _ => null,
        };
    }

    private Func<Task>? Find(Shortcut shortcut)
    {
        lock (_gate)
        {
            return _handlers.LastOrDefault(h => h.Shortcut == shortcut)?.Handler;
        }
    }

    private void Remove(Registration registration)
    {
        lock (_gate)
        {
            _handlers.Remove(registration);
        }
    }

    private sealed class Registration(Shortcut shortcut, Func<Task> handler, ShortcutRegistry owner) : IDisposable
    {
        public Shortcut Shortcut { get; } = shortcut;

        public Func<Task> Handler { get; } = handler;

        public void Dispose() => owner.Remove(this);
    }
}
