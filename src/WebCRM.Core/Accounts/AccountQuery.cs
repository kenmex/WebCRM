using WebCRM.Core.Querying;

namespace WebCRM.Core.Accounts;

public enum AccountSort
{
    Name,
    Industry,
    City,
    Status,
    Owner,
    OpenOpportunities,
    LastActivity,
}

/// <param name="Page">1-based.</param>
public sealed record AccountQuery(
    ListScope Scope = ListScope.Mine,
    string? Search = null,
    int? StatusId = null,
    int? IndustryId = null,
    string? OwnerId = null,
    string? City = null,
    AccountSort Sort = AccountSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = AccountQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
}
