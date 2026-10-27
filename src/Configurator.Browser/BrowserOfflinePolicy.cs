namespace Configurator.Browser;

using Configurator.Core;
using Configurator.Core.Definitions;
using Microsoft.JSInterop;

/// <summary>Prepares public app assets before input and fences connection and cache operations during answer custody.</summary>
public sealed class BrowserOfflinePolicy(IJSRuntime javascript) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserOfflinePolicy>? _reference;
    private long? _connectivitySubscription;
    private Task? _initialization;
    private bool _appPrepared;
    private bool _disposed;
    private long _generation;

    public bool OfflineReady { get; private set; }
    public bool IsOffline { get; private set; }
    public bool IsWorkspaceActive { get; private set; }
    public long WorkspaceGeneration => _generation;
    public bool CanFetchDefinition => !_disposed && !IsOffline && !IsWorkspaceActive;

    public ValueTask InitializeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        return new(_initialization ??= InitializeCoreAsync());
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            IJSObjectReference module = await javascript.InvokeAsync<IJSObjectReference>("import", "./pwa-lifecycle.js");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            BrowserOfflineState? state = await module.InvokeAsync<BrowserOfflineState?>("initialize");
            if (_disposed || state is null) return;
            _appPrepared = state.AppPrepared;
            OfflineReady = state.AppPrepared && state.OfflineReady;
            IsOffline = !state.Online;
            _reference = DotNetObjectReference.Create(this);
            _connectivitySubscription = await module.InvokeAsync<long>("subscribeConnectivity", _reference);
        }
        catch (JSException) { _appPrepared = false; OfflineReady = false; }
    }

    /// <summary>Observes connection eligibility only; online status does not prove remote reachability or release custody.</summary>
    [JSInvokable]
    public void ConnectivityChanged(bool online)
    {
        if (_disposed) return;
        IsOffline = !online;
    }

    public async ValueTask<bool> BeginWorkspaceAsync()
    {
        if (_disposed) return false;
        long generation = ++_generation;
        IsWorkspaceActive = true;
        await InitializeAsync();
        if (_disposed || generation != _generation) return false;
        if (!_appPrepared || _module is null) { IsWorkspaceActive = false; return false; }
        bool accepted;
        try { accepted = await _module.InvokeAsync<bool>("beginWorkspace", generation); }
        catch (JSException) { accepted = false; }
        if (_disposed || generation != _generation) return false;
        IsWorkspaceActive = accepted;
        return accepted;
    }

    public async ValueTask EndWorkspaceAsync()
    {
        long generation = ++_generation;
        IsWorkspaceActive = false;
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("endWorkspace", generation); }
        catch (JSException) { }
    }

    /// <summary>Releases custody only for the editor that began the current workspace.</summary>
    public ValueTask EndWorkspaceAsync(long expectedGeneration) =>
        expectedGeneration == _generation ? EndWorkspaceAsync() : ValueTask.CompletedTask;

    /// <summary>Retains only an admitted definition after explicit confirmation that its text and literals are public.</summary>
    public async ValueTask<bool> PrepareOfflineAsync(SetupDefinition definition, bool publicSafeConfirmed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_disposed || IsWorkspaceActive || !publicSafeConfirmed) return false;
        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive || !OfflineReady || _module is null) return false;
        try
        {
            return await _module.InvokeAsync<bool>("retainDefinition", definition.Digest.Value, definition.CanonicalBytes.ToArray());
        }
        catch (JSException) { return false; }
    }

    public async ValueTask<SetupDefinition?> ReadRetainedDefinitionAsync(string digest)
    {
        if (_disposed || IsWorkspaceActive || !ArtifactDigest.TryParse(digest, out ArtifactDigest expected)) return null;
        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive || _module is null) return null;
        try
        {
            byte[]? bytes = await _module.InvokeAsync<byte[]?>("readDefinition", expected.Value);
            if (_disposed || generation != _generation || IsWorkspaceActive || bytes is null) return null;
            SetupDefinition definition = SetupDefinitionCodec.ParseJson(bytes);
            return definition.Digest == expected ? definition : null;
        }
        catch (JSException) { return null; }
        catch (SetupDefinitionException) { return null; }
    }

    public async ValueTask<IReadOnlyList<SetupDefinition>> ReadRetainedDefinitionsAsync()
    {
        if (_disposed || IsWorkspaceActive) return [];
        long generation = _generation;
        await InitializeAsync();
        if (_module is null) return [];
        var definitions = new List<SetupDefinition>();
        try
        {
            string[]? digests = await _module.InvokeAsync<string[]?>("retainedDigests");
            foreach (string digest in (digests ?? []).Take(8))
                if (await ReadRetainedDefinitionAsync(digest) is { } definition) definitions.Add(definition);
        }
        catch (JSException) { }
        return !_disposed && generation == _generation && !IsWorkspaceActive ? definitions.AsReadOnly() : [];
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (IsWorkspaceActive) await EndWorkspaceAsync();
            if (_initialization is not null) await _initialization;
            if (_module is not null)
            {
                if (_connectivitySubscription is { } subscription)
                {
                    try { await _module.InvokeVoidAsync("unsubscribeConnectivity", subscription); }
                    catch (JSException) { }
                }
                await _module.DisposeAsync();
            }
        }
        finally { _reference?.Dispose(); }
    }
}

/// <summary>Reports worker-confirmed complete app preload separately from installed offline capability.</summary>
public sealed record BrowserOfflineState(bool AppPrepared, bool OfflineReady, bool Online);
