namespace WebCRM.Core.Entities;

/// <summary>A record starred by a user.</summary>
public class Favourite
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
