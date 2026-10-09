namespace WebCRM.Core.Entities;

/// <summary>Call, meeting or task. Linked to exactly one of Account, Contact, Opportunity, Lead (D1).</summary>
public class Activity : BaseEntity
{
    public int ActivityTypeId { get; set; }
    public ActivityType ActivityType { get; set; } = null!;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Outcome note.</summary>
    public string? Description { get; set; }

    /// <summary>UTC. Tasks need it; logged calls may not.</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>UTC. Null = open.</summary>
    public DateTime? DoneAt { get; set; }

    /// <summary>Meetings; greater than 0.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>User id; defaults to the creator (set by the service).</summary>
    public string OwnerId { get; set; } = string.Empty;

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>UTC. Stops duplicate reminder emails.</summary>
    public DateTime? ReminderSentAt { get; set; }
}
