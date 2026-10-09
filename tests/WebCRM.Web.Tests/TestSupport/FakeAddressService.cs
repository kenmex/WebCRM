using WebCRM.Core.Accounts;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>An in-memory IAccountAddressService: a saved model is what the next Get returns.</summary>
public sealed class FakeAddressService : IAccountAddressService
{
    public Dictionary<int, AccountAddressesEditModel> Addresses { get; } = [];

    public List<(int AccountId, AccountAddressesEditModel Model)> Saves { get; } = [];

    public int Gets { get; private set; }

    /// <summary>Make Get throw, to test the error state.</summary>
    public bool FailGet { get; set; }

    public Func<AccountAddressesEditModel, SaveResult> OnSave { get; set; } = _ => new SaveResult(SaveStatus.Saved);

    public Task<AccountAddressesEditModel?> GetAsync(int accountId, UserContext user, CancellationToken cancellationToken = default)
    {
        Gets++;
        if (FailGet)
        {
            throw new InvalidOperationException("db down");
        }

        return Task.FromResult(Addresses.TryGetValue(accountId, out var model) ? model.Clone() : null);
    }

    public Task<SaveResult> SaveAsync(
        int accountId, AccountAddressesEditModel model, UserContext user, CancellationToken cancellationToken = default)
    {
        Saves.Add((accountId, model.Clone()));
        var result = OnSave(model);
        if (result.Status == SaveStatus.Saved)
        {
            Addresses[accountId] = model.Clone();
        }

        return Task.FromResult(result);
    }
}
