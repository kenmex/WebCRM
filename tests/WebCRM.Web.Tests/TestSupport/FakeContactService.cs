using WebCRM.Core.Contacts;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>An in-memory IContactService: tests choose what Save returns and read back what was saved.</summary>
public sealed class FakeContactService : IContactService
{
    public Dictionary<int, ContactDetail> Contacts { get; } = [];

    /// <summary>The owner of each account, as the service would report it.</summary>
    public Dictionary<int, string> AccountOwners { get; } = [];

    public List<(ContactEditModel Model, SaveOptions Options)> Saves { get; } = [];

    public List<int?> NewCalls { get; } = [];

    public List<int> Deleted { get; } = [];

    public Func<ContactEditModel, SaveResult> OnSave { get; set; } = _ => new SaveResult(SaveStatus.Saved, Id: 42);

    public Func<ContactQuery, PagedResult<ContactListItem>> OnSearch { get; set; } = _ => PagedResult<ContactListItem>.Empty;

    public List<ContactQuery> Searches { get; } = [];

    public Task<PagedResult<ContactListItem>> SearchAsync(
        ContactQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        Searches.Add(query);
        return Task.FromResult(OnSearch(query));
    }

    public Task<ContactDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contacts.GetValueOrDefault(id));

    public Task<ContactEditModel> NewAsync(UserContext user, int? accountId = null, CancellationToken cancellationToken = default)
    {
        NewCalls.Add(accountId);
        var owner = accountId is { } id && AccountOwners.TryGetValue(id, out var o) ? o : user.UserId;
        return Task.FromResult(new ContactEditModel { AccountId = accountId, OwnerId = owner });
    }

    public Task<string?> GetDefaultOwnerAsync(int accountId, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(AccountOwners.GetValueOrDefault(accountId));

    public Task<SaveResult> SaveAsync(
        ContactEditModel model, UserContext user, SaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        Saves.Add((model.Clone(), options ?? new SaveOptions()));
        return Task.FromResult(OnSave(model));
    }

    public Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        return Task.FromResult(true);
    }
}
