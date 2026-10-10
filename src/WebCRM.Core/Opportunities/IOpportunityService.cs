using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Core.Opportunities;

public static class OpportunityAccess
{
    /// <summary>
    /// The one place that decides which opportunities a user may see (lists, board, search, export, dashboard, API).
    /// Stub until Phase 5: every user sees every record.
    /// </summary>
    public static IQueryable<Opportunity> VisibleTo(this IQueryable<Opportunity> opportunities, UserContext user) =>
        opportunities;
}

public interface IOpportunityService
{
    /// <summary>Open stages by SortOrder, then Won and Lost. Inactive stages are left out.</summary>
    Task<IReadOnlyList<StageOption>> GetStagesAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<OpportunityListItem>> SearchAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Amount and Weighted over the whole filtered set (paging and sort ignored).</summary>
    Task<OpportunityTotals> GetTotalsAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>One column per open stage plus Won and Lost, each with count, value and up to 100 cards.</summary>
    Task<IReadOnlyList<BoardColumn>> GetBoardAsync(
        BoardQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>The account's opportunities: open first (earliest close date), then won and lost (latest first).</summary>
    Task<IReadOnlyList<OpportunityListItem>> ListForAccountAsync(
        int accountId, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Contacts of the account, for the Primary contact picker.</summary>
    Task<IReadOnlyList<ContactOption>> GetContactOptionsAsync(
        int accountId, int? currentContactId = null, CancellationToken cancellationToken = default);

    /// <summary>Null when the opportunity does not exist or the user cannot see it (the same 404 for both).</summary>
    Task<OpportunityDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>A blank form: first open stage and its probability, close date today + 30, owner the current user.</summary>
    Task<OpportunityEditModel> NewAsync(
        UserContext user, int? accountId = null, CancellationToken cancellationToken = default);

    Task<SaveResult> SaveAsync(
        OpportunityEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete. False when the opportunity does not exist or is not visible.</summary>
    Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>The stage rules of P15/P16 in one place. See <see cref="OpportunityRules.ApplyMove"/>.</summary>
    Task<MoveStageResult> MoveStageAsync(
        MoveStageRequest request, UserContext user, CancellationToken cancellationToken = default);
}
