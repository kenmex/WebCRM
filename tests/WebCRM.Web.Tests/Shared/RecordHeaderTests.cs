using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using WebCRM.Web.Components.Shared;
using WebCRM.Web.Tests.TestSupport;

namespace WebCRM.Web.Tests.Shared;

public class RecordHeaderTests : MudTestContext
{
    private sealed class Form
    {
        [Required]
        public string? Name { get; set; }
    }

    private readonly Form _model = new();
    private int _edits;
    private int _saves;
    private int _cancels;

    public RecordHeaderTests() => StartProviders();

    private IRenderedComponent<RecordHeader> RenderHeader(bool editing, EditContext? context = null) =>
        Render<RecordHeader>(p => p
            .Add(x => x.Title, "Acme")
            .Add(x => x.IsEditing, editing)
            .Add(x => x.EditContext, context ?? (editing ? new EditContext(_model) : null))
            .Add(x => x.ReadContent, "<p class=\"read\">read body</p>")
            .Add(x => x.EditContent, "<p class=\"edit\">edit body</p>")
            .Add(x => x.OnEdit, () => _edits++)
            .Add(x => x.OnSave, () => _saves++)
            .Add(x => x.OnCancel, () => _cancels++));

    private static string[] ButtonTexts(IRenderedComponent<RecordHeader> cut) =>
        [.. cut.FindAll("button").Select(b => b.TextContent.Trim())];

    [Fact]
    public void Read_mode_shows_the_title_the_read_body_and_Edit()
    {
        var cut = RenderHeader(editing: false);

        cut.Find("h1").TextContent.ShouldBe("Acme");
        cut.Find(".read").TextContent.ShouldBe("read body");
        cut.FindAll(".edit").ShouldBeEmpty();
        ButtonTexts(cut).ShouldContain("Edit");
        ButtonTexts(cut).ShouldNotContain("Save");
    }

    [Fact]
    public void Edit_is_hidden_when_the_user_cannot_edit()
    {
        var cut = Render<RecordHeader>(p => p.Add(x => x.Title, "Acme").Add(x => x.CanEdit, false));

        ButtonTexts(cut).ShouldNotContain("Edit");
    }

    [Fact]
    public void Edit_raises_OnEdit()
    {
        var cut = RenderHeader(editing: false);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Edit").Click();

        _edits.ShouldBe(1);
    }

    [Fact]
    public void Edit_mode_shows_the_form_with_Save_and_Cancel_instead_of_the_read_body()
    {
        var cut = RenderHeader(editing: true);

        cut.Find(".edit").TextContent.ShouldBe("edit body");
        cut.FindAll(".read").ShouldBeEmpty();
        ButtonTexts(cut).ShouldContain("Save");
        ButtonTexts(cut).ShouldContain("Cancel");
        ButtonTexts(cut).ShouldNotContain("Edit");
    }

    [Fact]
    public void Save_does_nothing_while_the_form_is_invalid_and_saves_once_it_is_valid()
    {
        var cut = RenderHeader(editing: true);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        _saves.ShouldBe(0);

        _model.Name = "Acme";
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        _saves.ShouldBe(1);
    }

    [Fact]
    public void Save_is_disabled_while_saving()
    {
        var cut = Render<RecordHeader>(p => p
            .Add(x => x.Title, "Acme")
            .Add(x => x.IsEditing, true)
            .Add(x => x.Saving, true)
            .Add(x => x.EditContext, new EditContext(_model)));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Saving")).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Cancel_on_a_clean_form_leaves_edit_mode_without_asking()
    {
        var cut = RenderHeader(editing: true);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        _cancels.ShouldBe(1);
        DialogProvider.Markup.ShouldNotContain("Discard changes?");
    }

    [Fact]
    public void Cancel_on_a_dirty_form_asks_first_and_keeps_editing_when_declined()
    {
        var context = new EditContext(_model);
        var cut = RenderHeader(editing: true, context);
        cut.InvokeAsync(() => context.NotifyFieldChanged(context.Field(nameof(Form.Name))));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Discard changes?"));
        DialogProvider.FindAll("button").Single(b => b.TextContent.Trim() == "Keep editing").Click();

        _cancels.ShouldBe(0);
    }

    [Fact]
    public void Cancel_on_a_dirty_form_discards_when_confirmed()
    {
        var context = new EditContext(_model);
        var cut = RenderHeader(editing: true, context);
        cut.InvokeAsync(() => context.NotifyFieldChanged(context.Field(nameof(Form.Name))));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        DialogProvider.WaitForAssertion(() => DialogProvider.Markup.ShouldContain("Discard changes?"));
        DialogProvider.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").Click();

        cut.WaitForAssertion(() => _cancels.ShouldBe(1));
    }

    [Fact]
    public void Escape_cancels_the_edit()
    {
        var cut = RenderHeader(editing: true);

        cut.Find("form > div").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        _cancels.ShouldBe(1);
    }

    [Fact]
    public void Other_keys_do_not_cancel()
    {
        var cut = RenderHeader(editing: true);

        cut.Find("form > div").KeyDown(new KeyboardEventArgs { Key = "a" });

        _cancels.ShouldBe(0);
    }
}
