using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>
/// bUnit context with MudBlazor registered, JS interop in loose mode (MudBlazor calls into JS for
/// popovers and sizing) and the popover and dialog providers rendered, as MainLayout does.
/// </summary>
public abstract class MudTestContext : BunitContext
{
    protected MudTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IBrowserViewportService>(Viewport);

    }

    private IRenderedComponent<MudDialogProvider>? _dialogProvider;
    private IRenderedComponent<MudPopoverProvider>? _popoverProvider;

    /// <summary>
    /// Renders the popover, dialog and snackbar providers, as MainLayout does. Call it after registering
    /// any extra services (bUnit does not allow registering once something has been rendered).
    /// </summary>
    protected void StartProviders()
    {
        if (_dialogProvider is not null)
        {
            return;
        }

        _popoverProvider = Render<MudPopoverProvider>();
        _dialogProvider = Render<MudDialogProvider>();
        Render<MudSnackbarProvider>();
    }

    /// <summary>The rendered popover provider: open dropdowns and autocomplete results show up in its markup.</summary>
    protected IRenderedComponent<MudPopoverProvider> PopoverProvider =>
        _popoverProvider ?? throw new InvalidOperationException("Call StartProviders() first.");

    /// <summary>The rendered dialog provider: confirm dialogs show up in its markup.</summary>
    protected IRenderedComponent<MudDialogProvider> DialogProvider =>
        _dialogProvider ?? throw new InvalidOperationException("Call StartProviders() first.");

    /// <summary>Stands in for the browser: tests choose the breakpoint the list component sees.</summary>
    protected FakeViewport Viewport { get; } = new();
}

public sealed class FakeViewport : IBrowserViewportService, IAsyncDisposable
{
    private IBrowserViewportObserver? _observer;

    public Breakpoint Breakpoint { get; set; } = Breakpoint.Lg;

    /// <summary>Simulates the JS call failing (circuit trouble, blocked script).</summary>
    public bool Fail { get; set; }

    public ResizeOptions ResizeOptions { get; } = new();

    public async Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
    {
        if (Fail)
        {
            throw new InvalidOperationException("JS interop failed");
        }

        _observer = observer;
        if (fireImmediately)
        {
            await observer.NotifyBrowserViewportChangeAsync(Event());
        }
    }

    /// <summary>Simulates the user resizing the window across a breakpoint.</summary>
    public Task ResizeToAsync(Breakpoint breakpoint)
    {
        Breakpoint = breakpoint;
        return _observer!.NotifyBrowserViewportChangeAsync(Event());
    }

    private BrowserViewportEventArgs Event() =>
        new(Guid.Empty, new BrowserWindowSize { Width = 1000, Height = 800 }, Breakpoint, false);

    public Task SubscribeAsync(Guid subscriptionId, Action<BrowserViewportEventArgs> action, ResizeOptions? options = null, bool fireImmediately = true) =>
        Task.CompletedTask;

    public Task SubscribeAsync(Guid subscriptionId, Func<BrowserViewportEventArgs, Task> func, ResizeOptions? options = null, bool fireImmediately = true) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task UnsubscribeAsync(IBrowserViewportObserver observer)
    {
        _observer = null;
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(Guid subscriptionId) => Task.CompletedTask;

    public Task<bool> IsMediaQueryMatchAsync(string mediaQuery) => Task.FromResult(false);

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpoint) => Task.FromResult(false);

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpoint, Breakpoint reference) => Task.FromResult(false);

    public Task<Breakpoint> GetCurrentBreakpointAsync() => Task.FromResult(Breakpoint);

    public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync() =>
        Task.FromResult(new BrowserWindowSize { Width = 1000, Height = 800 });
}
