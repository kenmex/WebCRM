using MudBlazor;
using WebCRM.Core.Opportunities;
using WebCRM.Core.Users;
using WebCRM.Web.Components.Pages.Opportunities;

namespace WebCRM.Web.Components.Shared;

/// <summary>What the Won/Lost dialog collected: the lost reason (Lost only) and the actual close date.</summary>
public sealed record CloseOpportunityResult(int? LostReasonId, DateOnly ClosedOn);

/// <param name="Cancelled">The user backed out of a dialog; nothing was sent.</param>
/// <param name="Result">The service's answer; null when cancelled.</param>
public sealed record StageMoveOutcome(bool Cancelled, MoveStageResult? Result)
{
    public bool Moved => Result is { Status: MoveStageStatus.Moved };
}

/// <summary>
/// The stage-change flow shared by the board (drag, phone dropdown) and the detail page (stepper): the Won/Lost
/// dialog, the reopen confirmation, the service call and the message when it did not work. The rules themselves
/// live in <see cref="IOpportunityService.MoveStageAsync"/>; this only asks the user what the rules need.
/// </summary>
public sealed class StageMover(IOpportunityService opportunities, IDialogService dialogs, ISnackbar snackbar)
{
    public async Task<StageMoveOutcome> MoveAsync(
        int opportunityId, byte[] rowVersion, bool currentIsClosed, StageOption target, UserContext user,
        CancellationToken cancellationToken = default)
    {
        int? lostReasonId = null;
        DateOnly? closedOn = null;
        var confirmReopen = false;

        if (target.IsWon || target.IsLost)
        {
            var closed = await AskCloseAsync(target);
            if (closed is null)
            {
                return new StageMoveOutcome(true, null);
            }

            lostReasonId = closed.LostReasonId;
            closedOn = closed.ClosedOn;
        }
        else if (currentIsClosed)
        {
            if (!await ConfirmReopenAsync())
            {
                return new StageMoveOutcome(true, null);
            }

            confirmReopen = true;
        }

        var request = new MoveStageRequest(opportunityId, target.Id, rowVersion, lostReasonId, closedOn, confirmReopen);
        var result = await opportunities.MoveStageAsync(request, user, cancellationToken);

        // The service insists on confirmation (the caller did not know the opportunity was closed): ask, then retry.
        if (result.Status == MoveStageStatus.NeedsReopenConfirm)
        {
            if (!await ConfirmReopenAsync())
            {
                return new StageMoveOutcome(true, null);
            }

            result = await opportunities.MoveStageAsync(request with { ConfirmReopen = true }, user, cancellationToken);
        }

        Report(result);
        return new StageMoveOutcome(false, result);
    }

    private async Task<CloseOpportunityResult?> AskCloseAsync(StageOption target)
    {
        var parameters = new DialogParameters<CloseOpportunityDialog>
        {
            { x => x.StageName, target.Name },
            { x => x.IsLost, target.IsLost },
        };
        var dialog = await dialogs.ShowAsync<CloseOpportunityDialog>(
            target.IsLost ? "Mark as lost" : "Mark as won",
            parameters,
            new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.ExtraSmall, CloseOnEscapeKey = true });
        var result = await dialog.Result;
        return result is { Canceled: false, Data: CloseOpportunityResult closed } ? closed : null;
    }

    private Task<bool> ConfirmReopenAsync() => dialogs.ConfirmAsync(
        "Reopen opportunity?",
        "Moving it back to an open stage clears its close time and lost reason.",
        "Reopen");

    private void Report(MoveStageResult result)
    {
        switch (result.Status)
        {
            case MoveStageStatus.Moved:
                break;
            case MoveStageStatus.Conflict:
                snackbar.Add(result.Message ?? "Someone changed this opportunity. Reload to see the latest.", Severity.Warning);
                break;
            case MoveStageStatus.NotFound:
                snackbar.Add("This opportunity no longer exists.", Severity.Warning);
                break;
            default:
                snackbar.Add(result.Message ?? "The stage could not be changed.", Severity.Warning);
                break;
        }
    }
}
