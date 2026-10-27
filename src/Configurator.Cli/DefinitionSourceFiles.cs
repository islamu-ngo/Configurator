namespace Configurator.Cli;

internal sealed class DefinitionFileException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

/// <summary>Owns bounded public source reads and optimistic, serialized source mutations.</summary>
internal static class DefinitionSourceFiles
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly string[] Candidates = ["configurator.yaml", "configurator.yml", "configurator.json"];

    internal static string Discover(string root, string? source)
    {
        if (source is not null) return SafePath(source);
        string directory = SafePath(root);
        string[] found = Candidates.Select(name => Path.Combine(directory, name))
            .Where(path => File.Exists(path) || new FileInfo(path).LinkTarget is not null).ToArray();
        if (found.Length != 1)
            throw new DefinitionFileException(found.Length == 0 ? "definition-source-missing" : "definition-source-ambiguous");
        return SafePath(found[0]);
    }

    internal static PreparedArtifact PrepareCreate(string path, ReadOnlyMemory<byte> bytes) => new(path, bytes);

    /// <summary>Separates staging, publication and cleanup so established writes can be recorded before cleanup.</summary>
    internal sealed class PreparedArtifact : IDisposable
    {
        private readonly string _path;
        private readonly string _temporary;
        private readonly ReadOnlyMemory<byte> _bytes;
        private bool _staged;
        private bool _disposed;

        internal PreparedArtifact(string path, ReadOnlyMemory<byte> bytes)
        {
            _path = SafePath(path);
            CheckBytes(bytes);
            _bytes = bytes;
            _temporary = Path.Combine(Path.GetDirectoryName(_path)!, ".configurator-" + Guid.NewGuid().ToString("N") + ".tmp");
        }

        internal async ValueTask StageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_staged) throw new InvalidOperationException("definition-already-staged");
            string directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            SafePath(directory);
            await WriteTemporaryAsync(_temporary, _bytes, cancellationToken).ConfigureAwait(false);
            _staged = true;
        }

        internal ValueTask<bool> CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_staged) throw new InvalidOperationException("definition-not-staged");
            if (File.Exists(_path)) throw new DefinitionFileException("definition-output-conflict");
            SafePath(_path);
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(_temporary, _path); }
            catch (IOException) when (File.Exists(_path))
            {
                throw new DefinitionFileException("definition-output-conflict");
            }
            return ValueTask.FromResult(true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (File.Exists(_temporary)) File.Delete(_temporary);
        }
    }

    internal static string SafePath(string path)
    {
        if (path == "-") throw new DefinitionFileException("definition-local-file-required");
        string full = Path.GetFullPath(path);
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            FileSystemInfo item = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (item.LinkTarget is not null || item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new DefinitionFileException("definition-path-link");
        }
        return full;
    }

    private static void CheckBytes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is < 1 or > MaximumBytes) throw new DefinitionFileException("definition-source-bound");
    }

    private static async ValueTask WriteTemporaryAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
