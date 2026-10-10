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
        path = SafePath(path);
        CheckBytes(bytes);
        if (File.Exists(path))
        {
            if (acceptIdentical && Read(path).AsSpan().SequenceEqual(bytes.Span)) return false;
            throw new DefinitionFileException("definition-output-conflict");
        }
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        SafePath(directory);
        string temporary = Path.Combine(directory, ".configurator-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            WriteTemporary(temporary, bytes);
            SafePath(path);
            File.Move(temporary, path, false);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new DefinitionFileException("definition-output-conflict");
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static void Replace(string path, ReadOnlyMemory<byte> original, ReadOnlyMemory<byte> replacement)
    {
        path = SafePath(path);
        CheckBytes(replacement);
        string lockPath = path + ".configurator.lock";
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".configurator-" + Guid.NewGuid().ToString("N") + ".tmp");
        FileStream mutationLock;
        try { mutationLock = new FileStream(SafePath(lockPath), FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (IOException) { throw new DefinitionFileException("definition-source-locked"); }
        try
        {
            using (mutationLock)
            {
                WriteTemporary(temporary, replacement);
                byte[] current = Read(path);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), SHA256.HashData(original.Span)))
                    throw new DefinitionFileException("definition-source-changed");
                SafePath(path);
                File.Move(temporary, path, true);
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            File.Delete(lockPath);
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
