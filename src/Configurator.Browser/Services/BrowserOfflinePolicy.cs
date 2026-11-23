namespace Configurator.Browser.Services;

using Configurator.Browser.Models;
using Configurator.Core;
using Configurator.Core.Definitions;
using Microsoft.JSInterop;

/// <summary>Prepares public app assets before input and fences connection and cache operations during answer custody.</summary>
public sealed class BrowserOfflinePolicy(IJSRuntime javascript) : IAsyncDisposable
{
    public const int MaximumRetainedDefinitions = 8;
    private const int MaximumRetainedDefinitionBytes = 4 * 1024 * 1024;

    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserOfflinePolicy>? _reference;
    private long? _connectivitySubscription;
    private bool _connectivitySubscribing;
    private Task? _initialization;
    private bool _appPrepared;
    private bool _disposed;
    private long _generation;

    public bool OfflineReady { get; private set; }
    public bool IsOffline { get; private set; }
    public bool UpdateWaiting { get; private set; }
    public bool IsWorkspaceActive { get; private set; }
    public long WorkspaceGeneration => _generation;
    public bool CanFetchDefinition => !_disposed && !IsOffline && !IsWorkspaceActive;

    public ValueTask InitializeAsync()
    {
        if (_disposed || _appPrepared) return ValueTask.CompletedTask;
        if (_initialization?.IsCompleted == true) _initialization = null;
        return new(_initialization ??= InitializeCoreAsync());
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            IJSObjectReference? module = _module;
            if (module is null)
            {
                module = await javascript.InvokeAsync<IJSObjectReference>("import", "./pwa-lifecycle.js");
                if (_disposed) { await module.DisposeAsync(); return; }
                _module = module;
            }

            BrowserOfflineState? state = await module.InvokeAsync<BrowserOfflineState?>("initialize");
            if (_disposed || state is null) return;
            ApplyState(state);
            await EnsureConnectivitySubscribedAsync(module);
        }
        catch (JSException)
        {
            _appPrepared = false;
            OfflineReady = false;
        }
    }

    private async Task EnsureConnectivitySubscribedAsync(IJSObjectReference module)
    {
        if (_disposed || _connectivitySubscription is not null || _connectivitySubscribing) return;
        _connectivitySubscribing = true;
        try
        {
            _reference ??= DotNetObjectReference.Create(this);
            long subscription = await module.InvokeAsync<long>("subscribeConnectivity", _reference);
            _connectivitySubscription = subscription;
        }
        catch (JSException)
        {
        }
        finally
        {
            _connectivitySubscribing = false;
        }
    }

    private void ApplyState(BrowserOfflineState state)
    {
        _appPrepared = state.AppPrepared;
        OfflineReady = state.AppPrepared && state.OfflineReady;
        IsOffline = !state.Online;
        UpdateWaiting = state.UpdateWaiting;
    }

    /// <summary>Observes connection eligibility only; online status does not prove remote reachability or release custody.</summary>
    [JSInvokable]
    public void ConnectivityChanged(bool online)
    {
        if (_disposed) return;
        IsOffline = !online;
    }

    /// <summary>Retries offline service-worker preparation cleanly outside an active answer workspace.</summary>
    public async ValueTask<bool> RetryInitializeAsync()
    {
        if (_disposed || IsWorkspaceActive) return false;
        long generation = _generation;

        if (_module is null)
        {
            await InitializeAsync();
            if (_disposed || generation != _generation || IsWorkspaceActive) return false;
            return OfflineReady;
        }

        if (_initialization?.IsCompleted == true && !_appPrepared) _initialization = null;
        BrowserOfflineState? state;
        try
        {
            state = await _module.InvokeAsync<BrowserOfflineState?>("retryInitialize");
        }
        catch (JSException)
        {
            if (!_disposed && generation == _generation && !IsWorkspaceActive)
            {
                _appPrepared = false;
                OfflineReady = false;
            }
            return false;
        }

        if (_disposed || generation != _generation || IsWorkspaceActive || state is null) return false;
        ApplyState(state);
        await EnsureConnectivitySubscribedAsync(_module);
        if (_disposed || generation != _generation || IsWorkspaceActive) return false;
        return OfflineReady;
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
    public async ValueTask<BrowserOfflineRetentionStatus> PrepareOfflineAsync(Definition definition, bool publicSafeConfirmed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_disposed || IsWorkspaceActive) return BrowserOfflineRetentionStatus.BusyInWorkspace;
        if (!publicSafeConfirmed
            || definition.CanonicalBytes.Length > MaximumRetainedDefinitionBytes
            || !ArtifactDigest.TryParse(definition.Digest.Value, out _))
        {
            return BrowserOfflineRetentionStatus.InvalidDefinition;
        }

        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive) return BrowserOfflineRetentionStatus.BusyInWorkspace;
        if (!OfflineReady || _module is null) return BrowserOfflineRetentionStatus.StorageUnavailable;

        try
        {
            string? outcome = await _module.InvokeAsync<string?>("retainDefinition", definition.Digest.Value, definition.CanonicalBytes.ToArray());
            if (_disposed || generation != _generation || IsWorkspaceActive) return BrowserOfflineRetentionStatus.BusyInWorkspace;
            return outcome switch
            {
                "retained" => BrowserOfflineRetentionStatus.Retained,
                "capacity-reached" => BrowserOfflineRetentionStatus.CapacityReached,
                "workspace-active" => BrowserOfflineRetentionStatus.BusyInWorkspace,
                "invalid" => BrowserOfflineRetentionStatus.InvalidDefinition,
                _ => BrowserOfflineRetentionStatus.StorageUnavailable
            };
        }
        catch (JSException)
        {
            return BrowserOfflineRetentionStatus.StorageUnavailable;
        }
    }

    /// <summary>Removes one explicitly retained public definition outside active answer custody.</summary>
    public async ValueTask<bool> RemoveRetainedDefinitionAsync(string digest)
    {
        if (_disposed || IsWorkspaceActive || !ArtifactDigest.TryParse(digest, out ArtifactDigest expected)) return false;
        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive || _module is null) return false;
        try
        {
            bool deleted = await _module.InvokeAsync<bool>("deleteDefinition", expected.Value);
            return !_disposed && generation == _generation && !IsWorkspaceActive && deleted;
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async ValueTask<Definition?> ReadRetainedDefinitionAsync(string digest)
    {
        if (_disposed || IsWorkspaceActive || !ArtifactDigest.TryParse(digest, out ArtifactDigest expected)) return null;
        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive || _module is null) return null;
        try
        {
            byte[]? bytes = await _module.InvokeAsync<byte[]?>("readDefinition", expected.Value);
            if (_disposed || generation != _generation || IsWorkspaceActive || bytes is null) return null;
            Definition definition = DefinitionCodec.ParseJson(bytes);
            return definition.Digest == expected ? definition : null;
        }
        catch (JSException) { return null; }
        catch (DefinitionException) { return null; }
    }

    public async ValueTask<IReadOnlyList<Definition>> ReadRetainedDefinitionsAsync()
    {
        if (_disposed || IsWorkspaceActive) return [];
        long generation = _generation;
        await InitializeAsync();
        if (_disposed || generation != _generation || IsWorkspaceActive || _module is null) return [];
        var definitions = new List<Definition>();
        try
        {
            string[]? digests = await _module.InvokeAsync<string[]?>("retainedDigests");
            foreach (string digest in (digests ?? []).Take(MaximumRetainedDefinitions))
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
            else _generation++;
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

