using WebCRM.Core.Entities;

namespace WebCRM.Core.Opportunities;

public static class OpportunityRules
{
    public static decimal Weighted(decimal amount, decimal probability) => Math.Round(amount * probability / 100m, 2);

    /// <summary>
    /// The stage rules of P15/P16, shared by the board, the stepper and the service:
    /// probability resets to the stage default unless it was overridden; Won and Lost stamp ClosedAt (and set
    /// CloseDate to the actual close date); Lost needs a reason; moving a closed opportunity back to an open stage
    /// needs confirmation and clears ClosedAt and the reason. Changes the entity only when it returns Moved.
    /// </summary>
    public static MoveStageStatus ApplyMove(
        Opportunity opportunity, Stage from, Stage to, MoveStageRequest request, bool lostReasonExists,
        DateTimeOffset now)
    {
        var wasClosed = from.IsWon || from.IsLost;
        var toClosed = to.IsWon || to.IsLost;
        var today = DateOnly.FromDateTime(TimeDisplay.ToLocal(now.UtcDateTime));

        if (to.IsLost && request.LostReasonId is null)
        {
            return MoveStageStatus.NeedsLostReason;
        }

        if (to.IsLost && !lostReasonExists)
        {
            return MoveStageStatus.Invalid;
        }

        if (wasClosed && !toClosed && !request.ConfirmReopen)
        {
            return MoveStageStatus.NeedsReopenConfirm;
        }

        var closedOn = request.ClosedOn ?? today;
        if (toClosed && closedOn > today)
        {
            return MoveStageStatus.Invalid;
        }

        opportunity.StageId = to.Id;
        if (!opportunity.ProbabilityOverridden)
        {
            opportunity.Probability = to.DefaultProbability;
        }

        if (toClosed)
        {
            opportunity.ClosedAt = closedOn == today ? now.UtcDateTime : MiddayUtc(closedOn);
            opportunity.CloseDate = closedOn;
            opportunity.LostReasonId = to.IsLost ? request.LostReasonId : null;
        }
        else
        {
            opportunity.ClosedAt = null;
            opportunity.LostReasonId = null;
        }

        return MoveStageStatus.Moved;
    }

    /// <summary>Midday in Europe/Athens on that date, as UTC (midday keeps the date right whatever the offset).</summary>
    private static DateTime MiddayUtc(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeDisplay.DefaultTimeZoneId);
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(12, 0)), zone);
    }
}
