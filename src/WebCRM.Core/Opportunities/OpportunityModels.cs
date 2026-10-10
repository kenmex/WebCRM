using System.ComponentModel.DataAnnotations;
using WebCRM.Core.Querying;

namespace WebCRM.Core.Opportunities;

public enum OpportunityOutcome
{
    Open,
    Won,
    Lost,
    Any,
}

public enum OpportunitySort
{
    Name,
    Account,
    Stage,
    Amount,
    Probability,
    Weighted,
    CloseDate,
    Owner,
}

/// <param name="StageId">One stage; wins over <paramref name="Outcome"/>.</param>
/// <param name="Outcome">Open (the default of P14), Won, Lost or Any.</param>
/// <param name="CloseFrom">Inclusive.</param>
/// <param name="CloseTo">Inclusive.</param>
/// <param name="Page">1-based.</param>
public sealed record OpportunityQuery(
    ListScope Scope = ListScope.Mine,
    string? Search = null,
    int? StageId = null,
    OpportunityOutcome Outcome = OpportunityOutcome.Open,
    string? OwnerId = null,
    DateOnly? CloseFrom = null,
    DateOnly? CloseTo = null,
    OpportunitySort Sort = OpportunitySort.CloseDate,
    bool Descending = false,
    int Page = 1,
    int PageSize = OpportunityQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
}

/// <summary>Footer of P14: sums over the whole filtered set, not only the visible page.</summary>
public sealed record OpportunityTotals(decimal Amount, decimal Weighted);

/// <summary>One row of the list (P14), a board card (P15) or a row of the account's Opportunities tab (P9).</summary>
public sealed record OpportunityListItem(
    int Id,
    string Name,
    int AccountId,
    string AccountName,
    int StageId,
    string StageName,
    bool IsWon,
    bool IsLost,
    decimal Amount,
    decimal Probability,
    DateOnly CloseDate,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    byte[] RowVersion)
{
    public decimal Weighted => OpportunityRules.Weighted(Amount, Probability);

    public bool IsOpen => !IsWon && !IsLost;

    /// <summary>Open and past its expected close date.</summary>
    public bool IsOverdue(DateOnly today) => IsOpen && CloseDate < today;
}

/// <summary>An opportunity as shown in the header card (P16).</summary>
public sealed record OpportunityDetail(
    int Id,
    string Name,
    int AccountId,
    string AccountName,
    int? PrimaryContactId,
    string? PrimaryContactName,
    int StageId,
    string StageName,
    bool IsWon,
    bool IsLost,
    decimal Amount,
    string Currency,
    decimal Probability,
    bool ProbabilityOverridden,
    DateOnly CloseDate,
    DateTime? ClosedAt,
    int? LostReasonId,
    string? LostReasonName,
    string OwnerId,
    string? OwnerName,
    bool OwnerIsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion)
{
    public bool IsOpen => !IsWon && !IsLost;

    public OpportunityEditModel ToEditModel() => new()
    {
        Id = Id,
        Name = Name,
        AccountId = AccountId,
        PrimaryContactId = PrimaryContactId,
        StageId = StageId,
        Amount = Amount,
        Probability = Probability,
        CloseDate = CloseDate,
        LostReasonId = LostReasonId,
        OwnerId = OwnerId,
        RowVersion = RowVersion,
    };
}

/// <summary>
/// The opportunity form (P16 header card). Id = 0 means new. On an existing record the stage is read-only here:
/// it changes only through <see cref="IOpportunityService.MoveStageAsync"/> (the stepper), so the form and the
/// stepper cannot disagree. The service validates everything again on save.
/// </summary>
public sealed class OpportunityEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account is required.")]
    public int? AccountId { get; set; }

    public int? PrimaryContactId { get; set; }

    /// <summary>Chosen on a new record (open stages only); on an existing one it must stay as loaded.</summary>
    [Required(ErrorMessage = "Stage is required.")]
    public int? StageId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Amount cannot be negative.")]
    public decimal Amount { get; set; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Probability must be between 0 and 100.")]
    public decimal Probability { get; set; }

    [Required(ErrorMessage = "Close date is required.")]
    public DateOnly? CloseDate { get; set; }

    /// <summary>Only used (and required) while the opportunity is in the Lost stage.</summary>
    public int? LostReasonId { get; set; }

    [Required(ErrorMessage = "Owner is required.")]
    public string? OwnerId { get; set; }

    /// <summary>The RowVersion loaded with the record; sent back on save for the concurrency check.</summary>
    public byte[] RowVersion { get; set; } = [];

    public OpportunityEditModel Clone() => (OpportunityEditModel)MemberwiseClone();
}

/// <summary>A stage as the stepper, the board and the stage dropdowns need it.</summary>
public sealed record StageOption(int Id, string Name, int SortOrder, decimal DefaultProbability, bool IsWon, bool IsLost)
{
    public bool IsOpen => !IsWon && !IsLost;
}

public sealed record ContactOption(int Id, string Name);
