using WebCRM.Core.Opportunities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>An in-memory IOpportunityService: tests choose what each call returns and read back what was asked.</summary>
public sealed class FakeOpportunityService : IOpportunityService
{
    public static readonly StageOption Prospecting = new(1, "Prospecting", 10, 10m, false, false);
    public static readonly StageOption Qualification = new(2, "Qualification", 20, 20m, false, false);
    public static readonly StageOption Won = new(5, "Won", 90, 100m, true, false);
    public static readonly StageOption Lost = new(6, "Lost", 100, 0m, false, true);

    public IReadOnlyList<StageOption> Stages { get; set; } = [Prospecting, Qualification, Won, Lost];

    public Dictionary<int, OpportunityDetail> Opportunities { get; } = [];

    public List<OpportunityQuery> Searches { get; } = [];

    public List<OpportunityQuery> TotalsCalls { get; } = [];

    public List<BoardQuery> BoardCalls { get; } = [];

    public List<(OpportunityEditModel Model, SaveOptions Options)> Saves { get; } = [];

    public List<MoveStageRequest> Moves { get; } = [];

    public List<int> Deleted { get; } = [];

    public List<int> ContactOptionCalls { get; } = [];

    public List<ContactOption> Contacts { get; } = [];

    public IReadOnlyList<OpportunityListItem> AccountRows { get; set; } = [];

    public Func<OpportunityEditModel, SaveResult> OnSave { get; set; } = _ => new SaveResult(SaveStatus.Saved, Id: 42);

    public Func<OpportunityQuery, PagedResult<OpportunityListItem>> OnSearch { get; set; } = _ => PagedResult<OpportunityListItem>.Empty;

    public Func<OpportunityQuery, OpportunityTotals> OnTotals { get; set; } = _ => new OpportunityTotals(0m, 0m);

    public Func<BoardQuery, IReadOnlyList<BoardColumn>> OnBoard { get; set; } = _ => [];

    public Func<MoveStageRequest, Task<MoveStageResult>> OnMove { get; set; } = request => Task.FromResult(
        new MoveStageResult(MoveStageStatus.Moved, new MovedOpportunity(request.StageId, 50m, null, new DateOnly(2030, 1, 1), null, [9])));

    public Task<IReadOnlyList<StageOption>> GetStagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Stages);

    public Task<PagedResult<OpportunityListItem>> SearchAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        Searches.Add(query);
        return Task.FromResult(OnSearch(query));
    }

    public Task<OpportunityTotals> GetTotalsAsync(
        OpportunityQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        TotalsCalls.Add(query);
        return Task.FromResult(OnTotals(query));
    }

    public Task<IReadOnlyList<BoardColumn>> GetBoardAsync(
        BoardQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        BoardCalls.Add(query);
        return Task.FromResult(OnBoard(query));
    }

    public Task<IReadOnlyList<OpportunityListItem>> ListForAccountAsync(
        int accountId, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(AccountRows);

    public Task<IReadOnlyList<ContactOption>> GetContactOptionsAsync(
        int accountId, int? currentContactId = null, CancellationToken cancellationToken = default)
    {
        ContactOptionCalls.Add(accountId);
        return Task.FromResult<IReadOnlyList<ContactOption>>(Contacts);
    }

    public Task<OpportunityDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Opportunities.GetValueOrDefault(id));

    public Task<OpportunityEditModel> NewAsync(
        UserContext user, int? accountId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OpportunityEditModel
        {
            AccountId = accountId,
            StageId = Prospecting.Id,
            Probability = Prospecting.DefaultProbability,
            CloseDate = new DateOnly(2030, 1, 1),
            OwnerId = user.UserId,
        });

    public Task<SaveResult> SaveAsync(
        OpportunityEditModel model, UserContext user, SaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        Saves.Add((model.Clone(), options ?? new SaveOptions()));
        return Task.FromResult(OnSave(model));
    }

    public Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        return Task.FromResult(true);
    }

    public Task<MoveStageResult> MoveStageAsync(
        MoveStageRequest request, UserContext user, CancellationToken cancellationToken = default)
    {
        Moves.Add(request);
        return OnMove(request);
    }
}
