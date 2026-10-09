using Microsoft.AspNetCore.Components.Forms;
using WebCRM.Core.Records;

namespace WebCRM.Web.Components.Shared;

/// <summary>A save that came back with a warning: the headline and the records behind it.</summary>
public sealed record SaveWarning(string Message, IReadOnlyList<string> Items);

/// <summary>
/// The edit-mode plumbing every detail page needs: the model and its EditContext, server-side field errors,
/// the saving flag, the concurrency conflict and the "save anyway" warning. A page creates one, calls
/// <see cref="Begin"/> to enter edit mode and <see cref="SaveAsync"/> to save; it only supplies what is specific
/// to its record (the service call and what to do after a save).
/// </summary>
public sealed class RecordEditor<TModel>
    where TModel : class
{
    private ValidationMessageStore? _messages;

    public TModel? Model { get; private set; }

    public EditContext? Context { get; private set; }

    public bool IsEditing { get; private set; }

    public bool IsSaving { get; private set; }

    public ConcurrencyConflict? Conflict { get; private set; }

    public SaveWarning? Warning { get; private set; }

    /// <summary>Enters edit mode on a model (a copy of the record, or a blank one for create).</summary>
    public void Begin(TModel model)
    {
        Model = model;
        Conflict = null;
        Warning = null;
        Context = new EditContext(model);
        _messages = new ValidationMessageStore(Context);

        // A server-side error on a field goes away as soon as the user edits that field.
        Context.OnFieldChanged += (_, e) => _messages.Clear(e.FieldIdentifier);
        IsEditing = true;
    }

    /// <summary>Leaves edit mode and drops the edit model.</summary>
    public void End()
    {
        IsEditing = false;
        Model = null;
        Context = null;
        _messages = null;
        Conflict = null;
        Warning = null;
    }

    /// <summary>
    /// Marks the form clean. Call it before navigating away after a save, so the unsaved-changes guard
    /// does not fire on the page's own navigation.
    /// </summary>
    public void MarkClean() => Context?.MarkAsUnmodified();

    public void DismissWarning() => Warning = null;

    /// <summary>Shows server-side errors on their fields. An empty key is an error on the whole form.</summary>
    public void ShowFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        if (Context is null || _messages is null || Model is null)
        {
            return;
        }

        _messages.Clear();
        foreach (var (field, message) in errors)
        {
            _messages.Add(field.Length == 0 ? new FieldIdentifier(Model, string.Empty) : Context.Field(field), message);
        }

        Context.NotifyValidationStateChanged();
    }

    /// <summary>
    /// Runs a save and routes its outcome: field errors, warning and conflict are kept here for the page to show;
    /// <paramref name="onSaved"/> and <paramref name="onNotFound"/> are the page's own follow-ups.
    /// </summary>
    public async Task SaveAsync(
        Func<SaveOptions, Task<SaveResult>> save,
        SaveOptions options,
        Func<SaveResult, Task> onSaved,
        Func<Task> onNotFound,
        Action<Exception> onError)
    {
        IsSaving = true;
        Conflict = null;
        Warning = null;
        try
        {
            var result = await save(options);
            switch (result.Status)
            {
                case SaveStatus.Saved:
                    await onSaved(result);
                    break;

                case SaveStatus.Invalid:
                    ShowFieldErrors(result.FieldErrors ?? new Dictionary<string, string>());
                    break;

                case SaveStatus.Warning:
                    Warning = new SaveWarning(result.WarningMessage ?? "Please check before saving", result.Warnings ?? []);
                    break;

                case SaveStatus.Conflict:
                    Conflict = result.Conflict;
                    break;

                case SaveStatus.NotFound:
                    MarkClean();
                    await onNotFound();
                    break;
            }
        }
        catch (Exception ex)
        {
            onError(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
