namespace Configurator.Browser.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;

/// <summary>Models browser navigation on the renderer dispatcher, including delayed location guards.</summary>
internal sealed class RendererNavigationManager : NavigationManager
{
    private readonly BunitContext _context;

    internal RendererNavigationManager(BunitContext context)
    {
        _context = context;
        Initialize("http://localhost/", "http://localhost/");
    }

    internal Task Completion { get; private set; } = Task.CompletedTask;

    protected override void NavigateToCore(string uri, NavigationOptions options) =>
        Completion = _context.Renderer.Dispatcher.InvokeAsync(async () =>
        {
            string destination = ToAbsoluteUri(uri).AbsoluteUri;
            if (!await NotifyLocationChangingAsync(destination, options.HistoryEntryState, false)) return;
            Uri = destination;
            HistoryEntryState = options.HistoryEntryState;
            NotifyLocationChanged(false);
        });

    protected override void SetNavigationLockState(bool value) { }
}
