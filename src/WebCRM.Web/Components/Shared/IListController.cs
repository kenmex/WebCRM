namespace WebCRM.Web.Components.Shared;

/// <summary>What a list page offers to the parts inside it (sort headers, filter controls).</summary>
public interface IListController
{
    ListState State { get; }

    /// <summary>Sets one typed filter, goes back to page 1 and updates the URL.</summary>
    Task SetFilterAsync(string key, string? value);

    /// <summary>Sorts by a column key; sorting the same column again flips the direction.</summary>
    Task SortByAsync(string key);
}
