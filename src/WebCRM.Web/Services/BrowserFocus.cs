using Microsoft.JSInterop;

namespace WebCRM.Web.Services;

/// <summary>What the keyboard shortcuts need to know about the browser: where the focus is.</summary>
public interface IBrowserFocus
{
    /// <summary>
    /// True when the focus is in a text field (so a letter key is meant as text), or a dialog or menu is open
    /// (so the page behind it must not react). When it cannot tell, true: doing nothing is the safe answer.
    /// </summary>
    Task<bool> IsTypingOrInDialogAsync();

    /// <summary>Leaves the focused field, which makes the browser report its last edit. Does nothing when it cannot.</summary>
    Task BlurActiveElementAsync();
}

/// <summary>
/// Reads the focus through the browser's own <c>document.querySelector</c> over the existing Blazor JS interop.
/// No script of our own: MudBlazor's key interceptor reports the key but not where it was pressed.
/// </summary>
public sealed class BrowserFocus(IJSRuntime js, ILogger<BrowserFocus> logger) : IBrowserFocus
{
    private const string TextOrDialog = "input:focus, textarea:focus, select:focus, [contenteditable]:focus, .mud-dialog, .mud-popover-open";

    public async Task<bool> IsTypingOrInDialogAsync()
    {
        try
        {
            await using var element = await js.InvokeAsync<IJSObjectReference?>("document.querySelector", TextOrDialog);
            return element is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not read the focus; ignoring the letter shortcut.");
            return true;
        }
    }

    public async Task BlurActiveElementAsync()
    {
        try
        {
            await using var element = await js.InvokeAsync<IJSObjectReference?>("document.querySelector", ":focus");
            if (element is not null)
            {
                await element.InvokeVoidAsync("blur");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not leave the focused field.");
        }
    }
}
