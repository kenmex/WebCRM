using WebCRM.Core.Entities;
using WebCRM.Core.Lookups;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

public sealed class FakeUserContextProvider(UserContext? user = null) : IUserContextProvider
{
    public static UserContext Sales { get; } = new("sales-1", RoleNames.Sales, TeamId: 1);

    public ValueTask<UserContext?> GetAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<UserContext?>(user ?? Sales);
}

/// <summary>Returns the active values plus the current one, like the real service, and records the calls.</summary>
public sealed class FakeLookupService(params LookupOption[] all) : ILookupService
{
    public List<(LookupKind Kind, int? CurrentId)> Calls { get; } = [];

    public Task<IReadOnlyList<LookupOption>> GetOptionsAsync(
        LookupKind kind, int? currentId = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((kind, currentId));
        IReadOnlyList<LookupOption> result = [.. all.Where(o => o.IsActive || o.Id == currentId)];
        return Task.FromResult(result);
    }
}

public sealed class FakeOwnerService(params OwnerOption[] all) : IOwnerService
{
    public List<(UserContext User, string? CurrentOwnerId)> Calls { get; } = [];

    public Task<IReadOnlyList<OwnerOption>> GetAssignableAsync(
        UserContext user, string? currentOwnerId = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((user, currentOwnerId));
        IReadOnlyList<OwnerOption> result = [.. all.Where(o => o.IsActive || o.Id == currentOwnerId)];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<OwnerOption>> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OwnerOption>>(all);
}
