namespace WebCRM.Core.Entities;

/// <summary>In-app notification for one user.</summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>Recipient user id.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Assigned, TaskDue, RecordChanged, JobDone.</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Relative URL of the record.</summary>
    public string? Link { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC. Null = unread.</summary>
    public DateTime? ReadAt { get; set; }
}
