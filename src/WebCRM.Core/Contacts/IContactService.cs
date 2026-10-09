using WebCRM.Core.Entities;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Core.Contacts;

public static class ContactAccess
{
    /// <summary>
    /// The one place that decides which contacts a user may see (lists, search, export, dashboard, API).
    /// Stub until Phase 5: every user sees every record.
    /// </summary>
    public static IQueryable<Contact> VisibleTo(this IQueryable<Contact> contacts, UserContext user) => contacts;
}

public interface IContactService
{
    Task<PagedResult<ContactListItem>> SearchAsync(
        ContactQuery query, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>Null when the contact does not exist or the user cannot see it (the same 404 for both).</summary>
    Task<ContactDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default);

    /// <summary>
    /// A blank form. With an account (e.g. Add contact on the account page) the account is filled in and the owner
    /// defaults to the account's owner; without one the owner is the current user.
    /// </summary>
    Task<ContactEditModel> NewAsync(UserContext user, int? accountId = null, CancellationToken cancellationToken = default);

    /// <summary>The owner a new contact of this account starts with: the account's owner. Null if the account is not visible.</summary>
    Task<string?> GetDefaultOwnerAsync(int accountId, UserContext user, CancellationToken cancellationToken = default);

    Task<SaveResult> SaveAsync(
        ContactEditModel model, UserContext user, SaveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Soft delete. False when the contact does not exist or is not visible.</summary>
    Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default);
}
