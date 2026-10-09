namespace WebCRM.Core.Entities;

/// <summary>A named list view (filters, sort, columns, scope) saved by a user.</summary>
public class SavedView
{
    public int Id { get; set; }

    /// <summary>Owner user id.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>accounts, contacts, leads, opportunities, activities.</summary>
    public string ListKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string QueryString { get; set; } = string.Empty;

    /// <summary>Only Admin can set it.</summary>
    public bool IsPublic { get; set; }
}
