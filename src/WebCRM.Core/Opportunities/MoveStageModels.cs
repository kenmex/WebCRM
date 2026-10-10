using WebCRM.Core.Records;

namespace WebCRM.Core.Opportunities;

/// <param name="RowVersion">The version the caller saw; a changed row means <see cref="MoveStageStatus.Conflict"/>.</param>
/// <param name="LostReasonId">Required when the target stage is Lost.</param>
/// <param name="ClosedOn">
/// Won or Lost only: the actual close date (Europe/Athens). Defaults to today; sets both ClosedAt and CloseDate.
/// </param>
/// <param name="ConfirmReopen">The user confirmed moving a Won or Lost opportunity back to an open stage.</param>
public sealed record MoveStageRequest(
    int OpportunityId,
    int StageId,
    byte[] RowVersion,
    int? LostReasonId = null,
    DateOnly? ClosedOn = null,
    bool ConfirmReopen = false);

public enum MoveStageStatus
{
    Moved,

    /// <summary>The target stage is Lost and no reason was given. Nothing changed.</summary>
    NeedsLostReason,

    /// <summary>Moving a closed opportunity back to an open stage needs confirmation. Nothing changed.</summary>
    NeedsReopenConfirm,

    /// <summary>Unknown or inactive stage, unknown lost reason, or a close date in the future. Nothing changed.</summary>
    Invalid,

    /// <summary>Someone changed the record since it was loaded. Nothing changed.</summary>
    Conflict,

    /// <summary>Missing, deleted or not visible to this user.</summary>
    NotFound,
}

/// <summary>The values a successful move changed, so the caller can update its copy without reloading.</summary>
public sealed record MovedOpportunity(
    int StageId,
    decimal Probability,
    DateTime? ClosedAt,
    DateOnly CloseDate,
    int? LostReasonId,
    byte[] RowVersion);

public sealed record MoveStageResult(
    MoveStageStatus Status,
    MovedOpportunity? Moved = null,
    string? Message = null,
    ConcurrencyConflict? Conflict = null);
