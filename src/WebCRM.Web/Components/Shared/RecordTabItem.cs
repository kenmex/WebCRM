using Microsoft.AspNetCore.Components;

namespace WebCRM.Web.Components.Shared;

/// <summary>One tab of <see cref="RecordTabs"/>. The content is only rendered once the tab has been opened.</summary>
public sealed record RecordTabItem(string Title, RenderFragment Content);
