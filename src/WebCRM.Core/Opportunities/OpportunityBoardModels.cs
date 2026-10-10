using WebCRM.Core.Querying;

namespace WebCRM.Core.Opportunities;

/// <summary>Filters of the pipeline board (P15); the same fields as the P14 filters that make sense on a board.</summary>
public sealed record BoardQuery(
    ListScope Scope = ListScope.Mine,
    string? Search = null,
    string? OwnerId = null,
    DateOnly? CloseFrom = null,
    DateOnly? CloseTo = null)
{
    /// <summary>Cards shown per column; the rest is reached through the list.</summary>
    public const int CardsPerColumn = 100;
}

/// <param name="Count">All matching opportunities in the stage, not only the cards loaded.</param>
/// <param name="Total">Sum of Amount over all of them.</param>
/// <param name="Cards">At most <see cref="BoardQuery.CardsPerColumn"/>, earliest close date first.</param>
public sealed record BoardColumn(StageOption Stage, int Count, decimal Total, IReadOnlyList<OpportunityListItem> Cards)
{
    public bool HasMore => Count > Cards.Count;
}
