namespace Configurator.Browser.Services.Accessibility;

using Microsoft.JSInterop;

/// <summary>Publishes screen-reader status updates to ARIA live regions without persisting message values.</summary>
public sealed class AccessibilityAnnouncerService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _disposed;

    public async Task AnnouncePoliteAsync(string message)
    {
        if (_disposed || string.IsNullOrWhiteSpace(message)) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("announce", message, "polite");
        }
        catch (JSException)
        {
        }
    }

    public async Task AnnounceAssertiveAsync(string message)
    {
        if (_disposed || string.IsNullOrWhiteSpace(message)) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("announce", message, "assertive");
        }
        catch (JSException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/accessibility.js");
        return _module;
    }
}
