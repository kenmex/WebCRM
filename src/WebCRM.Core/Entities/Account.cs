namespace WebCRM.Core.Entities;

public class Account : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? VatNumber { get; set; }

    public int? IndustryId { get; set; }
    public Industry? Industry { get; set; }

    /// <summary>Defaults to the first status (set by the service).</summary>
    public int AccountStatusId { get; set; }
    public AccountStatus AccountStatus { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Website { get; set; }

    /// <summary>User id; defaults to the creator (set by the service).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Set when the account was created by an import, for rollback.</summary>
    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public ICollection<Address> Addresses { get; set; } = [];
    public ICollection<Contact> Contacts { get; set; } = [];
    public ICollection<Opportunity> Opportunities { get; set; } = [];
}
