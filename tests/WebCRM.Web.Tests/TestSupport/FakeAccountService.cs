using WebCRM.Core.Records;
using WebCRM.Core.Accounts;
using WebCRM.Core.Querying;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>An in-memory IAccountService: tests choose what Save returns and read back what was saved.</summary>
public sealed class FakeAccountService : IAccountService
{
    public Dictionary<int, AccountDetail> Accounts { get; } = [];

    public List<AccountEditModel> Saves { get; } = [];

    public Func<AccountEditModel, SaveResult> OnSave { get; set; } =
        _ => new SaveResult(SaveStatus.Saved, Id: 42);

    public Task<PagedResult<AccountListItem>> SearchAsync(
        AccountQuery query, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(PagedResult<AccountListItem>.Empty);

    public Task<AccountDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));

    public Task<AccountEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccountEditModel { OwnerId = user.UserId, AccountStatusId = 1 });

    public Task<SaveResult> SaveAsync(
        AccountEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Saves.Add(model.Clone());
        return Task.FromResult(OnSave(model));
    }

    public Task<AccountDeleteImpact?> GetDeleteImpactAsync(
        int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult<AccountDeleteImpact?>(new AccountDeleteImpact(0, 0));

    public Task<AccountDeleteResult> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccountDeleteResult(AccountDeleteStatus.Deleted));
}
