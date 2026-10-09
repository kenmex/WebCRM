using System.ComponentModel.DataAnnotations;
using WebCRM.Core.Querying;

namespace WebCRM.Core.Contacts;

public enum ContactSort
{
    Name,
    Account,
    JobTitle,
    Email,
    Owner,
    LastActivity,
}

/// <param name="HasEmail">True: only contacts with an email; false: only those without; null: all.</param>
/// <param name="Page">1-based.</param>
public sealed record ContactQuery(
    ListScope Scope = ListScope.Mine,
    string? Search = null,
    int? AccountId = null,
    string? OwnerId = null,
    bool? HasEmail = null,
    ContactSort Sort = ContactSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = ContactQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
}

/// <summary>One row of the Contacts list (P10).</summary>
public sealed record ContactListItem(
    int Id,
    string FullName,
    int AccountId,
    string AccountName,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? Mobile,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime? LastActivityAt);

/// <summary>A contact as shown in the read-mode header card (P11).</summary>
public sealed record ContactDetail(
    int Id,
    string? FirstName,
    string LastName,
    string FullName,
    int AccountId,
    string AccountName,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? Mobile,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion)
{
    public ContactEditModel ToEditModel() => new()
    {
        Id = Id,
        FirstName = FirstName,
        LastName = LastName,
        AccountId = AccountId,
        JobTitle = JobTitle,
        Email = Email,
        Phone = Phone,
        Mobile = Mobile,
        OwnerId = OwnerId,
        RowVersion = RowVersion,
    };
}

/// <summary>
/// The contact form (P11 header card and the Add contact dialog). Id = 0 means new.
/// Annotated for the form; the service validates it again on save.
/// </summary>
public sealed class ContactEditModel
{
    public int Id { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose an account.")]
    public int? AccountId { get; set; }

    [StringLength(100)]
    public string? JobTitle { get; set; }

    [StringLength(ContactRules.MaxEmailLength)]
    [ContactEmail]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(30)]
    public string? Mobile { get; set; }

    [Required(ErrorMessage = "Owner is required.")]
    public string? OwnerId { get; set; }

    /// <summary>The RowVersion loaded with the record; sent back on save for the concurrency check.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ContactEditModel Clone() => (ContactEditModel)MemberwiseClone();
}
