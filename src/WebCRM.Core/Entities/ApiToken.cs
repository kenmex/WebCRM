namespace WebCRM.Core.Entities;

/// <summary>API token for integrations. Only a SHA-256 hash is stored; the token itself is shown once.</summary>
public class ApiToken : ICreationAudited
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The user the token acts as; it inherits their record visibility.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 of the token (32 bytes).</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>First 8 characters, shown to identify the token.</summary>
    public string TokenPrefix { get; set; } = string.Empty;

    public ApiTokenScope Scope { get; set; } = ApiTokenScope.Read;

    /// <summary>UTC; defaults to +90 days (set by the service).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC; updated at most once a minute.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}
