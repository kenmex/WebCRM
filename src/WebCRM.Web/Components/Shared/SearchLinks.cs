using WebCRM.Core.Search;

namespace WebCRM.Web.Components.Shared;

/// <summary>Where search results lead. Relative to the base address, like every other link in the app.</summary>
public static class SearchLinks
{
    public static string ResultsPage(string query) => $"search?q={Uri.EscapeDataString(query)}";

    public static string Record(SearchHit hit) => hit.Type switch
    {
        SearchEntity.Account => $"accounts/{hit.Id}",
        SearchEntity.Contact => $"contacts/{hit.Id}",
        _ => throw new ArgumentOutOfRangeException(nameof(hit)),
    };

    /// <summary>The full list with the quick filter set to the query, across everything the user can see.</summary>
    public static string List(SearchEntity type, string query) => type switch
    {
        SearchEntity.Account => $"accounts?scope=all&q={Uri.EscapeDataString(query)}",
        SearchEntity.Contact => $"contacts?scope=all&q={Uri.EscapeDataString(query)}",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Label(SearchEntity type, bool plural = true) => (type, plural) switch
    {
        (SearchEntity.Account, true) => "Accounts",
        (SearchEntity.Account, false) => "Account",
        (SearchEntity.Contact, true) => "Contacts",
        (SearchEntity.Contact, false) => "Contact",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Icon(SearchEntity type) => type switch
    {
        SearchEntity.Account => MudBlazor.Icons.Material.Filled.Business,
        SearchEntity.Contact => MudBlazor.Icons.Material.Filled.Person,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
