using MudBlazor;

namespace WebCRM.Web.Components.Shared;

public static class DialogServiceExtensions
{
    /// <summary>
    /// Consistent confirm dialog for destructive or lossy actions (delete, discard changes).
    /// Returns true only when the user chose the confirm button.
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogs, string title, string message, string confirmText, string cancelText = "Cancel")
    {
        var result = await dialogs.ShowMessageBoxAsync(
            title, message, yesText: confirmText, cancelText: cancelText,
            options: new DialogOptions { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true });
        return result == true;
    }
}
