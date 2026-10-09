namespace WebCRM.Core.Querying;

/// <summary>One page of a server-side query plus the total across all pages.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount)
{
    public static PagedResult<T> Empty { get; } = new([], 0);
}
