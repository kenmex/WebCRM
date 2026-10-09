namespace WebCRM.Core.Accounts;

/// <summary>An account as shown in the read-mode header card (P9).</summary>
public sealed record AccountDetail(
    int Id,
    string Name,
    string? VatNumber,
    int? IndustryId,
    string? IndustryName,
    int AccountStatusId,
    string StatusName,
    string? Phone,
    string? Website,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion)
{
    public AccountEditModel ToEditModel() => new()
    {
        Id = Id,
        Name = Name,
        VatNumber = VatNumber,
        IndustryId = IndustryId,
        AccountStatusId = AccountStatusId,
        Phone = Phone,
        Website = Website,
        OwnerId = OwnerId,
        RowVersion = RowVersion,
    };
}
