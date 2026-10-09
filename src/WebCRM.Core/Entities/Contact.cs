namespace WebCRM.Core.Entities;

public class Contact : BaseEntity
{
    public string? FirstName { get; set; }

    public string LastName { get; set; } = string.Empty;

    /// <summary>Computed by SQL Server: CONCAT_WS(' ', FirstName, LastName).</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Required in MVP (D5).</summary>
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    /// <summary>Optional: Mr, Ms, Dr ... Not part of FullName.</summary>
    public int? SalutationId { get; set; }
    public Salutation? Salutation { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    /// <summary>The person has asked not to be contacted. A warning for people; it blocks nothing.</summary>
    public bool DoNotContact { get; set; }

    /// <summary>UTC. Set by the service when DoNotContact is switched on, cleared when it is switched off.</summary>
    public DateTime? DoNotContactSince { get; set; }

    /// <summary>User id; defaults to the account owner (set by the service).</summary>
    public string OwnerId { get; set; } = string.Empty;

    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
}
