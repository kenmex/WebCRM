namespace WebCRM.Core.Entities;

/// <summary>
/// Base columns for every business table (marked "base" in docs/data-dictionary.md).
/// Created* and Updated* are filled by the SaveChanges interceptor.
/// </summary>
public abstract class BaseEntity : ICreationAudited, IModificationAudited
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Soft delete: false hides the row from lists and search.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>SQL Server rowversion, for optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = [];
}
