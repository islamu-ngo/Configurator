using System.Security.Cryptography;

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

    internal static async ValueTask<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = SafePath(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < 1 or > MaximumBytes) throw new DefinitionFileException("definition-source-bound");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, MaximumBytes - (int)buffer.Length + 1)),
            cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new DefinitionFileException("definition-source-bound");
            buffer.Write(chunk, 0, read);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }

    internal static async ValueTask<bool> CreateAsync(string path, ReadOnlyMemory<byte> bytes, bool acceptIdentical,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using PreparedArtifact prepared = PrepareCreate(path, bytes, acceptIdentical);
        await prepared.StageAsync(cancellationToken).ConfigureAwait(false);
        return await prepared.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask ReplaceAsync(string path, ReadOnlyMemory<byte> original, ReadOnlyMemory<byte> replacement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using PreparedArtifact prepared = PrepareReplace(path, original, replacement);
        await prepared.StageAsync(cancellationToken).ConfigureAwait(false);
        await prepared.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static PreparedArtifact PrepareCreate(string path, ReadOnlyMemory<byte> bytes, bool acceptIdentical) =>
        new(path, bytes, acceptIdentical, null);

    internal static PreparedArtifact PrepareReplace(string path, ReadOnlyMemory<byte> original, ReadOnlyMemory<byte> replacement) =>
        new(path, replacement, false, original);

    /// <summary>Separates staging, publication and cleanup so established writes can be recorded before cleanup.</summary>
    internal sealed class PreparedArtifact : IDisposable
    {
        private readonly string _path;
        private readonly string _temporary;
        private readonly ReadOnlyMemory<byte> _bytes;
        private readonly ReadOnlyMemory<byte>? _original;
        private readonly bool _acceptIdentical;
        private FileStream? _mutationLock;
        private bool _staged;
        private bool _acceptedIdentical;
        private bool _disposed;

        internal PreparedArtifact(string path, ReadOnlyMemory<byte> bytes, bool acceptIdentical, ReadOnlyMemory<byte>? original)
        {
            _path = SafePath(path);
            CheckBytes(bytes);
            _bytes = bytes;
            _original = original;
            _acceptIdentical = acceptIdentical;
            _temporary = Path.Combine(Path.GetDirectoryName(_path)!, ".configurator-" + Guid.NewGuid().ToString("N") + ".tmp");
        }

        internal async ValueTask StageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_staged) throw new InvalidOperationException("definition-already-staged");
            if (_original is null && _acceptIdentical && File.Exists(_path)
                && (await ReadAsync(_path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(_bytes.Span))
            {
                _acceptedIdentical = true;
                _staged = true;
                return;
            }
            if (_original is not null)
            {
                try { _mutationLock = new FileStream(SafePath(_path + ".configurator.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                catch (IOException) { throw new DefinitionFileException("definition-source-locked"); }
            }
            else
            {
                string directory = Path.GetDirectoryName(_path)!;
                Directory.CreateDirectory(directory);
                SafePath(directory);
            }
            await WriteTemporaryAsync(_temporary, _bytes, cancellationToken).ConfigureAwait(false);
            _staged = true;
        }

        internal async ValueTask<bool> CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_staged) throw new InvalidOperationException("definition-not-staged");
            if (_acceptedIdentical && !File.Exists(_path)) throw new DefinitionFileException("definition-output-conflict");
            if (_original is { } original)
            {
                byte[] current = await ReadAsync(_path, cancellationToken).ConfigureAwait(false);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), SHA256.HashData(original.Span)))
                    throw new DefinitionFileException("definition-source-changed");
            }
            else if (File.Exists(_path))
            {
                if (_acceptIdentical && (await ReadAsync(_path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(_bytes.Span)) return false;
                throw new DefinitionFileException("definition-output-conflict");
            }
            SafePath(_path);
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(_temporary, _path, _original is not null); }
            catch (IOException) when (_original is null && File.Exists(_path))
            {
                throw new DefinitionFileException("definition-output-conflict");
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (File.Exists(_temporary)) File.Delete(_temporary);
            }
            finally
            {
                if (_mutationLock is not null)
                {
                    _mutationLock.Dispose();
                    _mutationLock = null;
                    File.Delete(_path + ".configurator.lock");
                }
            }
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
