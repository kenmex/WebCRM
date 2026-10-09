namespace WebCRM.Core.Entities;

public class Lead : BaseEntity
{
    /// <summary>Person's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Pre-fills the account on Convert.</summary>
    public string? Company { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int? LeadSourceId { get; set; }
    public LeadSource? LeadSource { get; set; }

    /// <summary>Defaults to New (set by the service); Converted only via Convert.</summary>
    public int LeadStatusId { get; set; }
    public LeadStatus LeadStatus { get; set; } = null!;

    /// <summary>User id; defaults to the creator (set by the service).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>UTC. Set by Convert; the lead is read-only once set.</summary>
    public DateTime? ConvertedAt { get; set; }

    public int? ConvertedAccountId { get; set; }
    public Account? ConvertedAccount { get; set; }

    public int? ConvertedContactId { get; set; }
    public Contact? ConvertedContact { get; set; }

    public int? ConvertedOpportunityId { get; set; }
    public Opportunity? ConvertedOpportunity { get; set; }
}
