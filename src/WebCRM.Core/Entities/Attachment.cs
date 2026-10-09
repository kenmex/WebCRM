namespace WebCRM.Core.Entities;

/// <summary>Uploaded file. Linked to exactly one of Account, Contact, Opportunity (D1).</summary>
public class Attachment : BaseEntity
{
    public const long MaxSizeBytes = 10_485_760; // 10 MB

    /// <summary>Generated GUID + extension; never the uploaded name.</summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>Shown and used on download.</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>From the allowed-types list.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
}
