using WebCRM.Core.Records;
using WebCRM.Core.Search;
using WebCRM.Core.Users;

namespace WebCRM.Core.Personal;

/// <summary>Starred records (the Star button on a record's header).</summary>
public interface IFavouriteService
{
    Task<bool> IsFavouriteAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stars or un-stars a record; asking for the state it already has changes nothing. False when the record does not exist
    /// or the user cannot see it.
    /// </summary>
    Task<bool> SetAsync(UserContext user, SearchEntity type, int id, bool favourite, CancellationToken cancellationToken = default);

    /// <summary>Newest first. Records that were deleted or are no longer visible are left out (and their rows removed).</summary>
    Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default);
}

/// <summary>The records the user opened last.</summary>
public interface IRecentViewService
{
    /// <summary>
    /// Notes that the user opened a record. Opening the same record again within a minute writes nothing. Only the last
    /// <see cref="PersonalRules.MaxRecentViews"/> are kept.
    /// </summary>
    Task RecordAsync(UserContext user, SearchEntity type, int id, CancellationToken cancellationToken = default);

    /// <summary>Newest first. Records that were deleted or are no longer visible are left out (and their rows removed).</summary>
    Task<IReadOnlyList<SearchHit>> ListAsync(UserContext user, int take = 10, CancellationToken cancellationToken = default);
}

/// <param name="OwnerName">Who made it; shown on published views.</param>
/// <param name="IsMine">True for the user's own views; a published view of someone else is read-only.</param>
public sealed record SavedViewItem(int Id, string Name, string QueryString, bool IsPublic, string OwnerId, string? OwnerName, bool IsMine);

/// <param name="Mine">The user's own views, by name.</param>
/// <param name="Shared">Views published by other users, by name.</param>
public sealed record SavedViewList(IReadOnlyList<SavedViewItem> Mine, IReadOnlyList<SavedViewItem> Shared)
{
    public static SavedViewList Empty { get; } = new([], []);
}

/// <summary>Named filters for a list (the Views menu), stored as the list's query string.</summary>
public interface ISavedViewService
{
    Task<SavedViewList> ListAsync(UserContext user, string listKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the user's view under a name. A name the user already uses for this list replaces that view when
    /// <paramref name="replaceExisting"/> is true (Update), otherwise it is an error on the Name field. Results: Saved
    /// (<see cref="SaveResult.Id"/> is the view), Invalid (field errors: Name, QueryString, or the 30-view limit).
    /// </summary>
    Task<SaveResult> SaveAsync(
        UserContext user, string listKey, string name, string queryString, bool replaceExisting = false,
        CancellationToken cancellationToken = default);

    /// <summary>Renames one of the user's own views. NotFound for anyone else's.</summary>
    Task<SaveResult> RenameAsync(UserContext user, int id, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's own view; an Admin may also delete a published one. False when there is nothing the user may delete.</summary>
    Task<bool> DeleteAsync(UserContext user, int id, CancellationToken cancellationToken = default);

    /// <summary>Publishes or withdraws one of the user's own views. Admin only; false for anyone else or another user's view.</summary>
    Task<bool> SetPublicAsync(UserContext user, int id, bool isPublic, CancellationToken cancellationToken = default);
}
