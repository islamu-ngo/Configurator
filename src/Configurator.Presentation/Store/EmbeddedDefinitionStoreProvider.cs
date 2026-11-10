namespace Configurator.Presentation.Store;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>Reads the curated store exclusively from assembly manifest resources.</summary>
public sealed class EmbeddedDefinitionStoreProvider : IDefinitionStoreProvider, IDisposable
{
    public const int MaximumIndexBytes = DefinitionStoreIndexParser.MaximumIndexBytes;
    private const int MaximumDefinitionBytes = 4 * 1024 * 1024;
    private const string ResourcePrefix = "Configurator.Store.";

    private readonly Func<string, Stream?> _openResource;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<DefinitionStoreProject>? _projects;
    private bool _disposed;

    public EmbeddedDefinitionStoreProvider()
        : this(typeof(EmbeddedDefinitionStoreProvider).Assembly.GetManifestResourceStream) { }

    internal EmbeddedDefinitionStoreProvider(Func<string, Stream?> openResource) =>
        _openResource = openResource ?? throw new ArgumentNullException(nameof(openResource));

    public async Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_projects is { } cached)
            return cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_projects is { } loaded)
                return loaded;

            byte[] bytes = await ReadResourceAsync(ResourcePrefix + "index.json", MaximumIndexBytes, cancellationToken);
            try
            {
                _projects = DefinitionStoreIndexParser.ParseIndex(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
            return _projects;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DefinitionStoreResult> OpenAsync(
        string projectId,
        long revision,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(projectId))
            return new DefinitionStoreResult.ProjectNotFound();
        if (revision < 1)
            return new DefinitionStoreResult.RevisionNotFound();

        try
        {
            IReadOnlyList<DefinitionStoreProject> projects = await LoadAsync(cancellationToken);
            DefinitionStoreProject? project = projects.SingleOrDefault(item => item.ProjectId == projectId);
            if (project is null)
                return new DefinitionStoreResult.ProjectNotFound();

            DefinitionStoreRevision? selected = project.Revisions.SingleOrDefault(item => item.Revision == revision);
            if (selected is null)
                return new DefinitionStoreResult.RevisionNotFound();

            string resourceName = ResourcePrefix + selected.SnapshotPath;
            byte[] bytes = await ReadResourceAsync(resourceName, MaximumDefinitionBytes, cancellationToken);
            try
            {
                Definition definition = DefinitionCodec.ParseJson(bytes);
                return DefinitionStoreIndexParser.MatchesDefinition(project, selected, definition)
                    ? new DefinitionStoreResult.Success(definition)
                    : new DefinitionStoreResult.SnapshotMismatch();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (Exception exception) when (exception is DefinitionStoreException or DefinitionException or JsonException
            or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new DefinitionStoreResult.CorruptAsset();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
    }

    private async Task<byte[]> ReadResourceAsync(string name, int maximumBytes, CancellationToken cancellationToken)
    {
        Stream? stream = _openResource(name);
        if (stream is null)
            throw new DefinitionStoreException("store-index-invalid");

        await using (stream)
        {
            using var output = new MemoryStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                while (output.Length <= maximumBytes)
                {
                    int count = (int)Math.Min(buffer.Length, maximumBytes + 1L - output.Length);
                    int read = await stream.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
                    if (read == 0) break;
                    output.Write(buffer, 0, read);
                }
                if (output.Length is 0 || output.Length > maximumBytes)
                    throw new DefinitionStoreException("store-index-invalid");
                return output.ToArray();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
                ArrayPool<byte>.Shared.Return(buffer);
                CryptographicOperations.ZeroMemory(output.GetBuffer());
            }
        }
    }
}
