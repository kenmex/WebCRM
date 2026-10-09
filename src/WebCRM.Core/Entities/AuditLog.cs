namespace WebCRM.Core.Entities;

/// <summary>Append-only audit trail (no base columns).</summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>User id; null for background jobs.</summary>
    public string? UserId { get; set; }

    /// <summary>Create, Update, Delete, Restore, Convert, Erase, SignIn, SignOut, SignInFailed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Null for sign-in events.</summary>
    public string? EntityName { get; set; }

    public int? EntityId { get; set; }

    /// <summary>JSON of field old and new values; scrubbed on Erase.</summary>
    public string? Changes { get; set; }

    /// <summary>UI, API token name, Import, Job.</summary>
    public string Source { get; set; } = "UI";

    /// <summary>UTC.</summary>
    public DateTime ChangedAt { get; set; }
}
