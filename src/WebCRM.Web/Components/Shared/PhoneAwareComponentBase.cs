using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;

namespace WebCRM.Web.Components.Shared;

/// <summary>
/// Base for components that look different on a phone (below the md breakpoint): grid or cards, tabs or
/// stacked panels. Reports the browser size once after the first render (prerender is off, so JS is available)
/// and again when the screen crosses the breakpoint. If the size cannot be read it falls back to the desktop
/// layout instead of waiting forever.
/// </summary>
public abstract class PhoneAwareComponentBase : ComponentBase, IBrowserViewportObserver, IAsyncDisposable
{
    private bool _subscribed;

    [Inject] private IBrowserViewportService Viewport { get; set; } = default!;

    [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;

    /// <summary>True once the browser size is known (or known to be unreadable). Show a skeleton until then.</summary>
    protected bool ViewportKnown { get; private set; }

    protected bool IsPhone { get; private set; }

    Guid IBrowserViewportObserver.Id { get; } = Guid.NewGuid();

    ResizeOptions IBrowserViewportObserver.ResizeOptions { get; } = new();

    /// <summary>Called when the viewport becomes known and each time the layout switches between phone and desktop.</summary>
    protected virtual Task OnLayoutChangedAsync() => Task.CompletedTask;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _subscribed)
        {
            return;
        }

        _subscribed = true;
        try
        {
            await Viewport.SubscribeAsync(this, fireImmediately: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggerFactory.CreateLogger(GetType()).LogWarning(ex, "Could not read the browser size; showing the desktop layout.");
            _subscribed = false;
            ViewportKnown = true;
            IsPhone = false;
            await InvokeAsync(LayoutChangedAsync);
        }
    }

    Task IBrowserViewportObserver.NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs args)
    {
        var phone = args.Breakpoint is Breakpoint.Xs or Breakpoint.Sm;
        if (ViewportKnown && phone == IsPhone)
        {
            return Task.CompletedTask;
        }

        ViewportKnown = true;
        IsPhone = phone;
        return InvokeAsync(LayoutChangedAsync);
    }

    private async Task LayoutChangedAsync()
    {
        await OnLayoutChangedAsync();

        // The layout changed outside any event handler, so Blazor would not re-render by itself.
        StateHasChanged();
    }

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (!_subscribed)
        {
            return;
        }

        try
        {
            await Viewport.UnsubscribeAsync(this);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone; nothing to unsubscribe from.
        }
    }
}
