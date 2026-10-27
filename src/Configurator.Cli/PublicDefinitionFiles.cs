using Configurator.Artifacts;

namespace Configurator.Cli;

/// <summary>Explicit native public-file authority; path checks do not establish captured-handle authority.</summary>
public sealed class PublicDefinitionFiles : ISetupCliAuthoringFiles
{
    public ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken) =>
        new(Task.Run(() => DefinitionSourceFiles.Discover(root, source), cancellationToken));

    public async ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = DefinitionSourceFiles.SafePath(path);
        try
        {
            using BoundedFileInput input = await BoundedFileInput.ReadAsync(path,
                Math.Min(maximumBytes, DefinitionSourceFiles.MaximumBytes), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(input.Bytes.ToArray());
        }
        catch (InvalidDataException) { throw new DefinitionFileException("definition-source-bound"); }
    }

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
        new(Task.Run(() => File.Exists(DefinitionSourceFiles.SafePath(path)), cancellationToken));

    public ISetupCliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical)
    {
        if (bytes.Length < 1 || bytes.Length > Math.Min(maximumBytes, DefinitionSourceFiles.MaximumBytes))
            throw new DefinitionFileException("definition-source-bound");
        return new Preparation(this, path, bytes, maximumBytes, acceptIdentical);
    }

    private sealed class Preparation(PublicDefinitionFiles files, string path, ReadOnlyMemory<byte> bytes,
        int maximumBytes, bool acceptIdentical) : ISetupCliPublicFilePreparation
    {
        // Identical-file reads remain bounded by this invocation rather than the native owner's fixed ceiling.
        private readonly DefinitionSourceFiles.PreparedArtifact _prepared = DefinitionSourceFiles.PrepareCreate(path, bytes);
        private bool _staged;
        private bool _disposed;

        public async ValueTask StageAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _prepared.StageAsync(cancellationToken).ConfigureAwait(false);
            _staged = true;
        }

        public async ValueTask<bool> CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_staged) throw new InvalidOperationException("definition-not-staged");
            if (acceptIdentical && await files.ExistsAsync(path, cancellationToken).ConfigureAwait(false))
            {
                using SetupCliInputBuffer existing = await files.ReadAsync(path, maximumBytes, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (existing.Bytes.Span.SequenceEqual(bytes.Span)) return false;
            }
            return await _prepared.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _prepared.Dispose();
        }
    }
}
