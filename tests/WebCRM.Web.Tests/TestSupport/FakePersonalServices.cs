using WebCRM.Core.Personal;
using WebCRM.Core.Records;
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

/// <summary>A scriptable ISavedViewService: tests set the views the user sees and record what the UI asks for.</summary>
public sealed class FakeSavedViewService : ISavedViewService
{
    private int _nextId = 100;

    public List<SavedViewItem> Mine { get; } = [];

    public List<SavedViewItem> Shared { get; } = [];

    public List<(string ListKey, string Name, string QueryString, bool Replace)> SaveCalls { get; } = [];

    public List<(int Id, string Name)> RenameCalls { get; } = [];

    public List<(int Id, bool IsPublic)> PublishCalls { get; } = [];

    public List<int> DeleteCalls { get; } = [];

    /// <summary>Set to make SaveAsync refuse a name as a duplicate.</summary>
    public HashSet<string> TakenNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Exception? FailWith { get; set; }

    public Task<SavedViewList> ListAsync(UserContext user, string listKey, CancellationToken cancellationToken = default) =>
        FailWith is null ? Task.FromResult(new SavedViewList([.. Mine], [.. Shared])) : Task.FromException<SavedViewList>(FailWith);

    public Task<SaveResult> SaveAsync(
        UserContext user, string listKey, string name, string queryString, bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        SaveCalls.Add((listKey, name, queryString, replaceExisting));
        if (TakenNames.Contains(name) && !replaceExisting)
        {
            return Task.FromResult(new SaveResult(
                SaveStatus.Invalid, FieldErrors: new Dictionary<string, string> { ["Name"] = PersonalRules.DuplicateViewNameMessage }));
        }

        var id = _nextId++;
        Mine.Add(new SavedViewItem(id, name, queryString, false, user.UserId, "Me", true));
        return Task.FromResult(new SaveResult(SaveStatus.Saved, id));
    }

    public Task<SaveResult> RenameAsync(UserContext user, int id, string name, CancellationToken cancellationToken = default)
    {
        RenameCalls.Add((id, name));
        var index = Mine.FindIndex(v => v.Id == id);
        if (index < 0)
        {
            return Task.FromResult(new SaveResult(SaveStatus.NotFound));
        }

        Mine[index] = Mine[index] with { Name = name };
        return Task.FromResult(new SaveResult(SaveStatus.Saved, id));
    }

    public Task<bool> DeleteAsync(UserContext user, int id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);
        return Task.FromResult(Mine.RemoveAll(v => v.Id == id) + Shared.RemoveAll(v => v.Id == id) > 0);
    }

    public Task<bool> SetPublicAsync(UserContext user, int id, bool isPublic, CancellationToken cancellationToken = default)
    {
        PublishCalls.Add((id, isPublic));
        var index = Mine.FindIndex(v => v.Id == id);
        if (index < 0 || !user.IsAdmin)
        {
            return Task.FromResult(false);
        }

        Mine[index] = Mine[index] with { IsPublic = isPublic };
        return Task.FromResult(true);
    }
}
