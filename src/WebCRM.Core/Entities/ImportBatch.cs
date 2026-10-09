namespace WebCRM.Core.Entities;

/// <summary>One row per Excel import, for progress and rollback.</summary>
public class ImportBatch : ICreationAudited
{
    public int Id { get; set; }

    /// <summary>Account or Contact.</summary>
    public string Entity { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public ImportStatus Status { get; set; } = ImportStatus.Queued;

    public DuplicateRule DuplicateRule { get; set; } = DuplicateRule.Skip;

    public int TotalRows { get; set; }

    public int OkRows { get; set; }

    public int WarningRows { get; set; }

    public int ErrorRows { get; set; }

    /// <summary>Stored file name of the error report (same rules as attachments).</summary>
    public string? ErrorReportName { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}
