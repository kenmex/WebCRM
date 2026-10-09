namespace WebCRM.Core.Users;

public sealed record OwnerOption(string Id, string DisplayName, bool IsActive);

public interface IOwnerService
{
    /// <summary>
    /// Users the current user may assign records to: Admin all active users, Manager their own team,
    /// Sales only themselves. <paramref name="currentOwnerId"/> is always included so an existing
    /// (possibly inactive) owner still displays.
    /// </summary>
    Task<IReadOnlyList<OwnerOption>> GetAssignableAsync(
        UserContext user, string? currentOwnerId = null, CancellationToken cancellationToken = default);

    /// <summary>Everyone who can own a record, for list filters (inactive users included, marked).</summary>
    Task<IReadOnlyList<OwnerOption>> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
