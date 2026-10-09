namespace Configurator.Browser;

using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>Owns current private export bytes and revokes browser capabilities on invalidation.</summary>
public sealed class BrowserArtifactExport(IJSRuntime javascript) : IAsyncDisposable
{
    private byte[] _bytes = [];
    private long _generation;
    private long _openedGeneration = -1;
    private readonly string _id = Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private bool _disposed;

    public bool HasOutput => _bytes.Length > 0 && !_disposed;

    public void SetOutput(ReadOnlyMemory<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ++_generation;
        CryptographicOperations.ZeroMemory(_bytes);
        _bytes = bytes.ToArray();
    }

    public Task<bool> CopyAsync() => ExposeAsync("copy");
    public Task<bool> DownloadAsync(string fileName, string mediaType) => ExposeAsync("download", fileName, mediaType);
    public Task<bool> RevealAsync(ElementReference element) => ExposeAsync("reveal", element);

    private async Task<bool> ExposeAsync(string action, params object[] arguments)
    {
        if (!HasOutput) return false;
        long generation = _generation;
        try
        {
            if (_module is null)
            {
                IJSObjectReference imported = await javascript.InvokeAsync<IJSObjectReference>("import", "./browser-artifacts.js");
                if (_disposed) { await imported.DisposeAsync(); return false; }
                _module = imported;
            }
            if (_disposed || generation != _generation || !HasOutput) return false;
            // JS rejects a late opening after its generation has been revoked.
            if (_openedGeneration != generation)
            {
                await _module.InvokeVoidAsync("open", _id, generation, _bytes);
                _openedGeneration = generation;
            }
            if (_disposed || generation != _generation) return false;
            return await _module.InvokeAsync<bool>(action, new object[] { _id, generation }.Concat(arguments).ToArray());
        }
        catch (JSException) { return false; }
    }

    public async ValueTask InvalidateAsync()
    {
        ++_generation;
        CryptographicOperations.ZeroMemory(_bytes);
        _bytes = [];
        if (_module is not null)
            try { await _module.InvokeVoidAsync("invalidate", _id, _generation); }
            catch (JSException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await InvalidateAsync();
        if (_module is not null)
            try { await _module.DisposeAsync(); }
            catch (JSException) { }
    }
}
