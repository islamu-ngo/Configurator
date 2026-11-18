namespace Configurator.Browser.Services.Docking;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

/// <summary>Persists strictly non-sensitive dock widths and panel states under a bounded localStorage key.</summary>
public sealed class LocalStorageDockLayoutPersistence(IJSRuntime jsRuntime) : IDockLayoutPersistence, IAsyncDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumStorageBytes = 2048;
    private const string JsModulePath = "./js/dock-layout-persistence.js";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 4,
        Converters = { new JsonStringEnumConverter() }
    };

    private IJSObjectReference? _jsModule;

    public async Task<DockLayoutSnapshot?> LoadAsync(string layoutKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutKey);

        try
        {
            IJSObjectReference module = await GetJsModuleAsync(cancellationToken);
            string? json = await module.InvokeAsync<string?>("get", cancellationToken, layoutKey);
            return Deserialize(layoutKey, json);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<bool> SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.LayoutKey);

        try
        {
            string json = Serialize(snapshot);
            if (Encoding.UTF8.GetByteCount(json) > MaximumStorageBytes)
                return false;

            IJSObjectReference module = await GetJsModuleAsync(cancellationToken);
            await module.InvokeVoidAsync("set", cancellationToken, snapshot.LayoutKey, json);
            return true;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string layoutKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutKey);

        try
        {
            IJSObjectReference module = await GetJsModuleAsync(cancellationToken);
            await module.InvokeVoidAsync("remove", cancellationToken, layoutKey);
            return true;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            return false;
        }
    }

    internal static string Serialize(DockLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.LayoutKey);

        DockLayoutStorageEnvelope envelope = new(CurrentSchemaVersion, snapshot.LayoutKey, snapshot);
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    internal static DockLayoutSnapshot? Deserialize(string layoutKey, string? json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutKey);

        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumStorageBytes)
            return null;

        try
        {
            DockLayoutStorageEnvelope? envelope = JsonSerializer.Deserialize<DockLayoutStorageEnvelope>(json, JsonOptions);
            if (envelope is null
                || envelope.SchemaVersion != CurrentSchemaVersion
                || !string.Equals(envelope.LayoutKey, layoutKey, StringComparison.Ordinal)
                || !string.Equals(envelope.Snapshot.LayoutKey, layoutKey, StringComparison.Ordinal))
            {
                return null;
            }

            return envelope.Snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is null)
            return;

        try
        {
            await _jsModule.DisposeAsync();
        }
        catch (JSException)
        {
            // Browser module cleanup may race with circuit teardown.
        }
        finally
        {
            _jsModule = null;
        }
    }

    private async ValueTask<IJSObjectReference> GetJsModuleAsync(CancellationToken cancellationToken) =>
        _jsModule ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, JsModulePath);

    private sealed record DockLayoutStorageEnvelope(
        int SchemaVersion,
        string LayoutKey,
        DockLayoutSnapshot Snapshot);
}
