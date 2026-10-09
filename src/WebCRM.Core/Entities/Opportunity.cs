namespace WebCRM.Core.Entities;

public class Opportunity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    /// <summary>Must belong to the same account (service rule).</summary>
    public int? PrimaryContactId { get; set; }
    public Contact? PrimaryContact { get; set; }

    /// <summary>Defaults to the first open stage (set by the service).</summary>
    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;

    public decimal Amount { get; set; }

    /// <summary>EUR only in MVP (D9).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>0 to 100; defaults to the stage's default probability (set by the service).</summary>
    public decimal Probability { get; set; }

    /// <summary>Set when the user edits Probability; stage changes then keep it.</summary>
    public bool ProbabilityOverridden { get; set; }

    /// <summary>Expected close; defaults to today + 30 in Europe/Athens (set by the service).</summary>
    public DateOnly CloseDate { get; set; }

    /// <summary>UTC. Stamped on Won or Lost, cleared on reopen.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Required when the stage is Lost (service rule).</summary>
    public int? LostReasonId { get; set; }
    public LostReason? LostReason { get; set; }

    /// <summary>User id; defaults to the creator (set by the service).</summary>
    public string OwnerId { get; set; } = string.Empty;
}
