using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using Shouldly;
using WebCRM.Core.Records;
using WebCRM.Web.Components.Shared;

namespace WebCRM.Web.Tests.Shared;

public class RecordEditorTests
{
    private sealed class Form
    {
        [Required]
        public string? Name { get; set; }

        public string? Phone { get; set; }
    }

    private readonly RecordEditor<Form> _editor = new();
    private int _saved;
    private int _notFound;
    private Exception? _error;

    private Task RunAsync(SaveResult result, SaveOptions? options = null) =>
        _editor.SaveAsync(
            _ => Task.FromResult(result),
            options ?? new SaveOptions(),
            _ =>
            {
                _saved++;
                return Task.CompletedTask;
            },
            () =>
            {
                _notFound++;
                return Task.CompletedTask;
            },
            ex => _error = ex);

    [Fact]
    public void Begin_enters_edit_mode_with_a_clean_context()
    {
        _editor.Begin(new Form());

        _editor.IsEditing.ShouldBeTrue();
        _editor.Context.ShouldNotBeNull();
        _editor.Context.IsModified().ShouldBeFalse();
    }

    [Fact]
    public void End_leaves_edit_mode_and_drops_everything()
    {
        _editor.Begin(new Form());

        _editor.End();

        _editor.IsEditing.ShouldBeFalse();
        _editor.Model.ShouldBeNull();
        _editor.Context.ShouldBeNull();
    }

    [Fact]
    public void MarkClean_clears_the_modified_flag()
    {
        _editor.Begin(new Form());
        _editor.Context!.NotifyFieldChanged(_editor.Context.Field(nameof(Form.Name)));
        _editor.Context.IsModified().ShouldBeTrue();

        _editor.MarkClean();

        _editor.Context.IsModified().ShouldBeFalse();
    }

    [Fact]
    public async Task A_saved_result_calls_onSaved_once_and_clears_the_saving_flag()
    {
        _editor.Begin(new Form());

        await RunAsync(new SaveResult(SaveStatus.Saved, Id: 5));

        _saved.ShouldBe(1);
        _editor.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task Field_errors_are_shown_on_their_fields_and_go_away_when_the_field_is_edited()
    {
        _editor.Begin(new Form());
        var field = _editor.Context!.Field(nameof(Form.Phone));

        await RunAsync(new SaveResult(
            SaveStatus.Invalid, FieldErrors: new Dictionary<string, string> { [nameof(Form.Phone)] = "Bad phone" }));

        _saved.ShouldBe(0);
        _editor.Context.GetValidationMessages(field).ShouldBe(["Bad phone"]);

        _editor.Context.NotifyFieldChanged(field);
        _editor.Context.GetValidationMessages(field).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_error_on_the_whole_form_uses_an_empty_key()
    {
        var model = new Form();
        _editor.Begin(model);

        await RunAsync(new SaveResult(
            SaveStatus.Invalid, FieldErrors: new Dictionary<string, string> { [string.Empty] = "Something is wrong" }));

        _editor.Context!.GetValidationMessages().ShouldBe(["Something is wrong"]);
    }

    [Fact]
    public async Task A_warning_is_kept_for_the_page_until_dismissed()
    {
        _editor.Begin(new Form());

        await RunAsync(new SaveResult(SaveStatus.Warning, Warnings: ["Acme Ltd"], WarningMessage: "Similar accounts exist"));

        _editor.Warning.ShouldNotBeNull();
        _editor.Warning.Message.ShouldBe("Similar accounts exist");
        _editor.Warning.Items.ShouldBe(["Acme Ltd"]);
        _saved.ShouldBe(0);

        _editor.DismissWarning();
        _editor.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task A_conflict_is_kept_and_cleared_by_the_next_save()
    {
        _editor.Begin(new Form());
        var conflict = new ConcurrencyConflict("Maria", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));

        await RunAsync(new SaveResult(SaveStatus.Conflict, Conflict: conflict));
        _editor.Conflict.ShouldBe(conflict);

        await RunAsync(new SaveResult(SaveStatus.Saved, Id: 1));
        _editor.Conflict.ShouldBeNull();
    }

    [Fact]
    public async Task Not_found_marks_the_form_clean_before_the_page_navigates_away()
    {
        _editor.Begin(new Form());
        _editor.Context!.NotifyFieldChanged(_editor.Context.Field(nameof(Form.Name)));

        await _editor.SaveAsync(
            _ => Task.FromResult(new SaveResult(SaveStatus.NotFound)),
            new SaveOptions(),
            _ => Task.CompletedTask,
            () =>
            {
                _editor.Context!.IsModified().ShouldBeFalse();
                _notFound++;
                return Task.CompletedTask;
            },
            ex => _error = ex);

        _notFound.ShouldBe(1);
    }

    [Fact]
    public async Task An_exception_goes_to_onError_and_still_clears_the_saving_flag()
    {
        _editor.Begin(new Form());

        await _editor.SaveAsync(
            _ => throw new InvalidOperationException("db down"),
            new SaveOptions(),
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            ex => _error = ex);

        _error.ShouldBeOfType<InvalidOperationException>();
        _editor.IsSaving.ShouldBeFalse();
    }

    [Fact]
    public async Task The_options_reach_the_save_call()
    {
        _editor.Begin(new Form());
        SaveOptions? seen = null;

        await _editor.SaveAsync(
            o =>
            {
                seen = o;
                return Task.FromResult(new SaveResult(SaveStatus.Saved));
            },
            new SaveOptions { AcceptWarnings = true, Overwrite = true },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            ex => _error = ex);

        seen.ShouldNotBeNull();
        seen.AcceptWarnings.ShouldBeTrue();
        seen.Overwrite.ShouldBeTrue();
    }
}
