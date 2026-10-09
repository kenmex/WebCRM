namespace WebCRM.Core.Accounts;

/// <summary>One row of the Accounts list (P8). Counts come from subqueries, not loaded child collections.</summary>
public sealed record AccountListItem(
    int Id,
    string Name,
    string? IndustryName,
    string? City,
    int AccountStatusId,
    string StatusName,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    int OpenOpportunities,
    DateTime? LastActivityAt);
