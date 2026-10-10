using System.ComponentModel.DataAnnotations;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.Core.Querying;

namespace WebCRM.Core.Leads;

public enum LeadSort
{
    Name,
    Company,
    Email,
    Source,
    Status,
    Owner,
    Created,
}

/// <param name="StatusId">One status; wins over <paramref name="OpenOnly"/>.</param>
/// <param name="OpenOnly">
/// True (the default of P12): hide Converted and Disqualified leads. False: every status.
/// </param>
/// <param name="Page">1-based.</param>
public sealed record LeadQuery(
    ListScope Scope = ListScope.Mine,
    string? Search = null,
    int? StatusId = null,
    bool OpenOnly = true,
    int? SourceId = null,
    string? OwnerId = null,
    LeadSort Sort = LeadSort.Created,
    bool Descending = true,
    int Page = 1,
    int PageSize = LeadQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
}

/// <summary>One row of the Leads list (P12).</summary>
public sealed record LeadListItem(
    int Id,
    string Name,
    string? Company,
    string? Email,
    string? SourceName,
    string StatusName,
    string? StatusCode,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime CreatedAt);

/// <summary>A lead as shown in the header card (P13).</summary>
public sealed record LeadDetail(
    int Id,
    string Name,
    string? Company,
    string? Email,
    string? Phone,
    int? LeadSourceId,
    string? SourceName,
    int LeadStatusId,
    string StatusName,
    string? StatusCode,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion,
    DateTime? ConvertedAt = null,
    int? ConvertedAccountId = null,
    string? ConvertedAccountName = null,
    int? ConvertedContactId = null,
    string? ConvertedContactName = null,
    int? ConvertedOpportunityId = null,
    string? ConvertedOpportunityName = null)
{
    /// <summary>Converted leads are read-only (P13).</summary>
    public bool IsConverted => ConvertedAt is not null;

    public bool IsDisqualified => StatusCode == LeadStatus.Disqualified;

    /// <summary>The Convert button shows only for leads that are neither Converted nor Disqualified.</summary>
    public bool CanConvert => !IsConverted && !IsDisqualified;

    public LeadEditModel ToEditModel() => new()
    {
        Id = Id,
        Name = Name,
        Company = Company,
        Email = Email,
        Phone = Phone,
        LeadSourceId = LeadSourceId,
        LeadStatusId = LeadStatusId,
        OwnerId = OwnerId,
        RowVersion = RowVersion,
    };
}

/// <summary>The lead form (P13 header card). Id = 0 means new. The service validates it again on save.</summary>
public sealed class LeadEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Company { get; set; }

    [StringLength(ContactRules.MaxEmailLength)]
    [ContactEmail]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public int? LeadSourceId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public int? LeadStatusId { get; set; }

    [Required(ErrorMessage = "Owner is required.")]
    public string? OwnerId { get; set; }

    /// <summary>The RowVersion loaded with the record; sent back on save for the concurrency check.</summary>
    public byte[] RowVersion { get; set; } = [];

    public LeadEditModel Clone() => (LeadEditModel)MemberwiseClone();
}
