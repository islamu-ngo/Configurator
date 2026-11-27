namespace Configurator.Browser.Services.Accessibility;

using Microsoft.JSInterop;

/// <summary>Manages programmatic focus transitions and modal focus restoration across Browser components.</summary>
public sealed class AccessibilityFocusService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _disposed;

    public async Task FocusAsync(string cssSelector, bool preventScroll = false)
    {
        if (_disposed || string.IsNullOrWhiteSpace(cssSelector)) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("setFocus", cssSelector, preventScroll);
        }
        catch (JSException)
        {
            // Browser focus interop must not interrupt interactive navigation.
        }
    }

    public async Task FocusByIdAsync(string elementId, bool preventScroll = false)
    {
        if (_disposed || string.IsNullOrWhiteSpace(elementId)) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("setFocusById", elementId, preventScroll);
        }
        catch (JSException)
        {
        }
    }

    public Task FocusMainContentAsync() => FocusByIdAsync("main-content");

    public async Task FocusOnNavigateAsync()
    {
        if (_disposed) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("focusOnNavigate");
        }
        catch (JSException)
        {
        }
    }

    public async Task SaveFocusAsync(string scope = "default")
    {
        if (_disposed) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("saveActiveElement", scope);
        }
        catch (JSException)
        {
        }
    }

    public async Task RestoreFocusAsync(string? fallbackSelector = null, bool preventScroll = false, string scope = "default")
    {
        if (_disposed) return;
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            await module.InvokeVoidAsync("restoreFocus", fallbackSelector, preventScroll, scope);
        }
        catch (JSException)
        {
        }
    }

    public async Task<string> GetPreferredMotionAsync()
    {
        if (_disposed) return "no-preference";
        try
        {
            IJSObjectReference module = await GetModuleAsync();
            return await module.InvokeAsync<string>("getPreferredMotion");
        }
        catch (JSException)
        {
            return "no-preference";
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
