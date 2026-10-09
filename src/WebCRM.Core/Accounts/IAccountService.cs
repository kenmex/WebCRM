using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Core.Accounts;

public enum AccountDeleteStatus
{
    Deleted,

    /// <summary>The account still has open opportunities.</summary>
    Blocked,

    NotFound,
}

public sealed record AccountDeleteResult(AccountDeleteStatus Status, string? Message = null);

/// <summary>What deleting an account affects, for the confirm dialog.</summary>
public sealed record AccountDeleteImpact(int Contacts, int OpenOpportunities);

public interface IAccountService
{
    Task<PagedResult<AccountListItem>> SearchAsync(
        AccountQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Null when the account does not exist or the user cannot see it (the same 404 for both).</summary>
    Task<AccountDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>A blank form with defaults: owner = current user, status = first status.</summary>
    Task<AccountEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accounts for the picker: names containing the text (accent-insensitive), names that start with it first,
    /// at most <paramref name="take"/>. Blank text gives the first accounts by name.
    /// </summary>
    Task<IReadOnlyList<AccountPickerItem>> SearchPickerAsync(
        string? text, UserContext user, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>The picker item for one account, to show the current value. Null if missing or not visible.</summary>
    Task<AccountPickerItem?> GetPickerItemAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    Task<SaveResult> SaveAsync(
        AccountEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AccountDeleteImpact?> GetDeleteImpactAsync(
        int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Soft delete. Blocked while the account has open opportunities; its contacts go with it.</summary>
    Task<AccountDeleteResult> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default);
}
