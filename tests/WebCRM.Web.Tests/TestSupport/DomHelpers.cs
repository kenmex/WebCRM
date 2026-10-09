using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace WebCRM.Web.Tests.TestSupport;

public static class DomHelpers
{
    /// <summary>The text input behind a MudBlazor field, found by its label text.</summary>
    public static IElement InputByLabel<T>(this IRenderedComponent<T> cut, string label)
        where T : IComponent
    {
        var labelElement = cut.FindAll("label").First(l => l.TextContent.Trim().StartsWith(label, StringComparison.Ordinal));
        return cut.Find("#" + labelElement.GetAttribute("for"));
    }

    public static IElement ButtonByText<T>(this IRenderedComponent<T> cut, string text)
        where T : IComponent =>
        cut.FindAll("button, a.mud-button-root").First(b => b.TextContent.Trim() == text);

    public static bool HasButton<T>(this IRenderedComponent<T> cut, string text)
        where T : IComponent =>
        cut.FindAll("button, a.mud-button-root").Any(b => b.TextContent.Trim() == text);
}
