using WebCRM.Core.Personal;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

public sealed class FakeFavouriteService : IFavouriteService
{
    public HashSet<(SearchEntity Type, int Id)> Starred { get; } = [];

    /// <summary>What ListAsync returns.</summary>
    public List<SearchHit> Listed { get; } = [];

    public List<(SearchEntity Type, int Id, bool Favourite)> SetCalls { get; } = [];

    /// <summary>Set to make the next SetAsync calls fail.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>Set to false to simulate a record that has gone.</summary>
    public bool SetResult { get; set; } = true;

    public Task<bool> IsFavouriteAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Starred.Contains((type, id)));

    public Task<bool> SetAsync(UserContext user, SearchEntity type, int id, bool favourite, CancellationToken cancellationToken = default)
    {
        SetCalls.Add((type, id, favourite));
        if (FailWith is not null)
        {
            return Task.FromException<bool>(FailWith);
        }

        if (SetResult)
        {
            _ = favourite ? Starred.Add((type, id)) : Starred.Remove((type, id));
        }

        return Task.FromResult(SetResult);
    }

    public Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default) =>
        FailWith is null ? Task.FromResult<IReadOnlyList<SearchHit>>(Listed) : Task.FromException<IReadOnlyList<SearchHit>>(FailWith);
}

public sealed class FakeRecentViewService : IRecentViewService
{
    public List<(SearchEntity Type, int Id)> Recorded { get; } = [];

    /// <summary>What ListAsync returns.</summary>
    public List<SearchHit> Listed { get; } = [];

    public Exception? FailWith { get; set; }

    public Task RecordAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default)
    {
        Recorded.Add((type, id));
        return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
    }

    public Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SearchHit>>(Listed);
}
