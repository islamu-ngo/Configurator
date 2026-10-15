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

    internal static byte[] Read(string path)
    {
        path = SafePath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > MaximumBytes) throw new DefinitionFileException("definition-source-bound");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int read;
        while ((read = stream.Read(chunk)) != 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new DefinitionFileException("definition-source-bound");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    internal static bool Create(string path, ReadOnlyMemory<byte> bytes, bool acceptIdentical)
    {
        using PreparedArtifact prepared = PrepareCreate(path, bytes, acceptIdentical);
        prepared.Stage();
        return prepared.Commit();
    }

    internal static void Replace(string path, ReadOnlyMemory<byte> original, ReadOnlyMemory<byte> replacement)
    {
        using PreparedArtifact prepared = PrepareReplace(path, original, replacement);
        prepared.Stage();
        prepared.Commit();
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

        internal void Stage()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_staged) throw new InvalidOperationException("definition-already-staged");
            if (_original is null && _acceptIdentical && File.Exists(_path) && Read(_path).AsSpan().SequenceEqual(_bytes.Span))
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
            WriteTemporary(_temporary, _bytes);
            _staged = true;
        }

        internal bool Commit()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_staged) throw new InvalidOperationException("definition-not-staged");
            if (_acceptedIdentical && !File.Exists(_path)) throw new DefinitionFileException("definition-output-conflict");
            if (_original is { } original)
            {
                byte[] current = Read(_path);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), SHA256.HashData(original.Span)))
                    throw new DefinitionFileException("definition-source-changed");
            }
            else if (File.Exists(_path))
            {
                if (_acceptIdentical && Read(_path).AsSpan().SequenceEqual(_bytes.Span)) return false;
                throw new DefinitionFileException("definition-output-conflict");
            }
            SafePath(_path);
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

    private static void WriteTemporary(string path, ReadOnlyMemory<byte> bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes.Span);
        stream.Flush(true);
    }
}
