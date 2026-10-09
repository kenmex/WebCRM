namespace WebCRM.Core.Entities;

/// <summary>A record recently opened by a user; the last 50 per user are kept, upserted on view.</summary>
public class RecentView
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime ViewedAt { get; set; }
}
