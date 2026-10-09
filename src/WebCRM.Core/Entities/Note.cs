namespace WebCRM.Core.Entities;

/// <summary>Plain-text note. Linked to exactly one of Account, Contact, Opportunity, Lead (D1).</summary>
public class Note : BaseEntity
{
    public string Body { get; set; } = string.Empty;

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
}
