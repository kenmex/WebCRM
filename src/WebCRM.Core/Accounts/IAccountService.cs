using WebCRM.Core.Querying;
using WebCRM.Core.Users;

namespace WebCRM.Core.Accounts;

public enum AccountSaveStatus
{
    Saved,

    /// <summary>Field errors in <see cref="AccountSaveResult.FieldErrors"/>; nothing was saved.</summary>
    Invalid,

    /// <summary>Similar names exist. Nothing was saved; repeat with AcceptSimilarNames to go ahead.</summary>
    SimilarNames,

    /// <summary>Someone saved the record since it was loaded. Nothing was saved.</summary>
    Conflict,

    /// <summary>Missing, deleted or not visible to this user.</summary>
    NotFound,
}

/// <param name="ChangedBy">Display name of the user who saved the record since it was opened.</param>
/// <param name="ChangedAtUtc">When they saved it.</param>
public sealed record ConcurrencyConflict(string? ChangedBy, DateTime? ChangedAtUtc);

public sealed record AccountSaveResult(
    AccountSaveStatus Status,
    int Id = 0,
    IReadOnlyDictionary<string, string>? FieldErrors = null,
    IReadOnlyList<string>? SimilarNames = null,
    ConcurrencyConflict? Conflict = null);

public sealed record AccountSaveOptions
{
    /// <summary>The user saw the similar-names warning and wants to save anyway.</summary>
    public bool AcceptSimilarNames { get; init; }

    /// <summary>Save over a concurrent change. Honoured for Admin only.</summary>
    public bool Overwrite { get; init; }
}

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

    Task<AccountSaveResult> SaveAsync(
        AccountEditModel model, UserContext user, AccountSaveOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AccountDeleteImpact?> GetDeleteImpactAsync(
        int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Soft delete. Blocked while the account has open opportunities; its contacts go with it.</summary>
    Task<AccountDeleteResult> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default);
}
