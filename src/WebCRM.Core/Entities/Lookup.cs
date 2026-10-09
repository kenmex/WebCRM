namespace WebCRM.Core.Entities;

/// <summary>
/// Admin-editable lookup table (Industry, LeadSource, LeadStatus, ActivityType,
/// AccountStatus, LostReason). Stage has its own class.
/// </summary>
public abstract class Lookup
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Dropdown order.</summary>
    public int SortOrder { get; set; }

    /// <summary>Inactive values stay on old records but are hidden from dropdowns.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set on values the code relies on (e.g. LeadStatus NEW); Admin can rename but not deactivate them.</summary>
    public string? SystemCode { get; set; }
}
