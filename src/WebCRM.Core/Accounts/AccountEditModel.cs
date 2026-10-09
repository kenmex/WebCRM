using System.ComponentModel.DataAnnotations;

namespace WebCRM.Core.Accounts;

/// <summary>
/// The account form (P9 header card), used for create and edit. Id = 0 means new.
/// Annotated for the form; the service validates it again on save.
/// </summary>
public sealed class AccountEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? VatNumber { get; set; }

    public int? IndustryId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public int? AccountStatusId { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(300)]
    [AbsoluteUrl]
    public string? Website { get; set; }

    [Required(ErrorMessage = "Owner is required.")]
    public string? OwnerId { get; set; }

    /// <summary>The RowVersion loaded with the record; sent back on save for the concurrency check.</summary>
    public byte[] RowVersion { get; set; } = [];

    public AccountEditModel Clone() => (AccountEditModel)MemberwiseClone();
}
