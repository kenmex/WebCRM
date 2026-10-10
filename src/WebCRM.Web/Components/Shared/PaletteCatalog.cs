using MudBlazor;

namespace WebCRM.Web.Components.Shared;

/// <summary>One line in the command palette: where it leads and how it looks.</summary>
public sealed record PaletteItem(string Group, string Title, string? Subtitle, string Icon, string Href);

/// <summary>The fixed part of the command palette: pages to jump to and things to create (docs/page-spec.md, App shell).</summary>
public static class PaletteCatalog
{
    public const string ActionsGroup = "Actions";

    public static readonly IReadOnlyList<PaletteItem> Actions =
    [
        new(ActionsGroup, "New account", null, Icons.Material.Filled.Add, "accounts/new"),
        new(ActionsGroup, "New contact", null, Icons.Material.Filled.Add, "contacts/new"),
        new(ActionsGroup, "New lead", null, Icons.Material.Filled.Add, "leads/new"),
        new(ActionsGroup, "Go to Accounts", null, Icons.Material.Filled.Business, "accounts"),
        new(ActionsGroup, "Go to Contacts", null, Icons.Material.Filled.People, "contacts"),
        new(ActionsGroup, "Go to Leads", null, Icons.Material.Filled.TrackChanges, "leads"),
        new(ActionsGroup, "Go to Home", null, Icons.Material.Filled.Home, string.Empty),
    ];

    /// <summary>The actions whose title contains every word typed (any case); all of them for an empty text.</summary>
    public static IReadOnlyList<PaletteItem> Match(string? text)
    {
        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. Actions.Where(a => words.All(w => a.Title.Contains(w, StringComparison.OrdinalIgnoreCase)))];
    }
}
