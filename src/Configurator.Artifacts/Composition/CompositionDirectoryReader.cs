namespace Configurator.Artifacts.Composition;

using System.Security.Cryptography;
using System.Text;
using Configurator.Core.Composition;
using Microsoft.Win32.SafeHandles;
using static LinuxDirectoryInterop;

internal static class CompositionDirectoryReader
{
    internal static async ValueTask<CompositionResult> ReadAsync(
        string rootDirectory, CompositionDirectoryLimits limits,
        ICompositionPublicationCommitBarrier barrier, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            throw new CompositionException(CompositionFailureCode.UnsupportedFilesystem);

        string root = Path.GetFullPath(rootDirectory);
        if (!Path.IsPathFullyQualified(root))
            throw new CompositionException(CompositionFailureCode.UnsafePath);

        DirectorySnapshot first = await CaptureAsync(root, limits, cancellationToken).ConfigureAwait(false);
        var fragments = new List<CompositionSource>(first.Files.Count);
        foreach (SnapshotFile file in first.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fragments.Add(file.Format == FragmentFormat.Json
                ? new CompositionJsonSource(file.Bytes)
                : new CompositionYamlSource(file.Bytes));
        }

        CompositionResult candidate = await new CompositionCompiler().CompileAsync(
            fragments, cancellationToken).ConfigureAwait(false);
        if (!candidate.Succeeded) return candidate;
        cancellationToken.ThrowIfCancellationRequested();
        await barrier.AwaitPublicationCommitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        DirectorySnapshot second;
        try
        {
            second = await CaptureAsync(root, limits, cancellationToken).ConfigureAwait(false);
        }
        catch (CompositionException)
        {
            throw new CompositionException(CompositionFailureCode.SourceChanged);
        }
        if (!first.IdentityEquals(second))
            throw new CompositionException(CompositionFailureCode.SourceChanged);
        cancellationToken.ThrowIfCancellationRequested();
        return candidate;
    }

    private static async ValueTask<DirectorySnapshot> CaptureAsync(
        string root, CompositionDirectoryLimits limits, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeIdentity rootIdentity = NativeIdentity.ReadPath(root);
        if (!rootIdentity.IsDirectory || rootIdentity.IsLink || rootIdentity.IsSpecial)
            throw new CompositionException(CompositionFailureCode.UnsafeEntry);

        using SafeFileHandle rootHandle = NativeMethods.OpenRoot(root);
        NativeIdentity openedRoot = NativeIdentity.ReadHandle(rootHandle);
        if (!rootIdentity.StableEquals(openedRoot))
            throw new CompositionException(CompositionFailureCode.SourceChanged);

        var files = new List<SnapshotFile>();
        var directories = new List<SnapshotDirectory>();
        var pathIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(string.Empty);
        int aggregateBytes = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativeDirectory = pending.Dequeue();
            string absoluteDirectory = relativeDirectory.Length == 0
                ? root : Path.Combine(root, relativeDirectory);
            string[] entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(absoluteDirectory)
                    .Order(StringComparer.Ordinal).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new CompositionException(CompositionFailureCode.UnsafeEntry);
            }

            if (entries.Length > limits.EntriesPerDirectory)
                throw new CompositionException(CompositionFailureCode.LimitExceeded);
            if (checked(directories.Count + 1) > limits.Directories)
                throw new CompositionException(CompositionFailureCode.LimitExceeded);

            NativeIdentity directoryIdentity = relativeDirectory.Length == 0
                ? openedRoot : NativeMethods.OpenIdentity(rootHandle, relativeDirectory, directory: true);
            directories.Add(new SnapshotDirectory(relativeDirectory, directoryIdentity));

            foreach (string entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(root, entry);
                ValidateRelativePath(relative, limits, pathIdentities);
                string name = Path.GetFileName(relative);
                if (IsRejectedName(name))
                    throw new CompositionException(CompositionFailureCode.UnsafePath);

                NativeIdentity pathIdentity = NativeIdentity.ReadPath(entry);
                if (pathIdentity.IsLink || pathIdentity.IsSpecial)
                    throw new CompositionException(CompositionFailureCode.UnsafeEntry);
                if (pathIdentity.IsDirectory)
                {
                    NativeIdentity opened = NativeMethods.OpenIdentity(rootHandle, relative, directory: true);
                    if (!pathIdentity.StableEquals(opened))
                        throw new CompositionException(CompositionFailureCode.SourceChanged);
                    pending.Enqueue(relative);
                    continue;
                }
                if (!pathIdentity.IsRegularFile || pathIdentity.LinkCount != 1)
                    throw new CompositionException(CompositionFailureCode.UnsafeEntry);
                if (checked(files.Count + 1) > limits.Files)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);

                FragmentFormat format = Extension(relative);
                SnapshotFile file = await ReadFileAsync(
                    rootHandle, relative, format, pathIdentity, limits, cancellationToken).ConfigureAwait(false);
                try { aggregateBytes = checked(aggregateBytes + file.Bytes.Length); }
                catch (OverflowException) { throw new CompositionException(CompositionFailureCode.LimitExceeded); }
                if (aggregateBytes > limits.AggregateDirectoryBytes
                    || aggregateBytes > CompositionLimits.DefaultAggregateSourceBytes)
                    throw new CompositionException(CompositionFailureCode.LimitExceeded);
                files.Add(file);
            }
        }

        NativeIdentity finalRoot = NativeIdentity.ReadPath(root);
        if (!rootIdentity.StableEquals(finalRoot))
            throw new CompositionException(CompositionFailureCode.SourceChanged);
        return new DirectorySnapshot(
            rootIdentity,
            directories.OrderBy(static item => item.RelativePath, StringComparer.Ordinal).ToArray(),
            files.OrderBy(static item => item.RelativePath, StringComparer.Ordinal).ToArray());
    }

    private static async ValueTask<SnapshotFile> ReadFileAsync(
        SafeFileHandle rootHandle, string relative, FragmentFormat format,
        NativeIdentity discovered, CompositionDirectoryLimits limits, CancellationToken cancellationToken)
    {
        using SafeFileHandle handle = NativeMethods.OpenRelative(rootHandle, relative, directory: false);
        NativeIdentity before = NativeIdentity.ReadHandle(handle);
        if (!discovered.Equals(before) || !before.IsRegularFile || before.LinkCount != 1)
            throw new CompositionException(CompositionFailureCode.SourceChanged);
        if (before.Size < 0 || before.Size > limits.PerFileBytes || before.Size > int.MaxValue)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);

        byte[] bytes = new byte[(int)before.Size];
        NativeIdentity after;
        using (var stream = new FileStream(handle, FileAccess.Read, bufferSize: 16_384, isAsync: false))
        {
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream.ReadByte() != -1)
                throw new CompositionException(CompositionFailureCode.SourceChanged);
            after = NativeIdentity.ReadHandle(handle);
        }

        if (!before.StableEquals(after))
            throw new CompositionException(CompositionFailureCode.SourceChanged);
        byte[] digest = SHA256.HashData(bytes);
        return new SnapshotFile(relative, format, before, bytes, digest);
    }

    private static void ValidateRelativePath(
        string relative, CompositionDirectoryLimits limits, Dictionary<string, string> identities)
    {
        if (relative.Length == 0 || relative.Length > limits.RelativePathCharacters
            || Path.IsPathFullyQualified(relative) || Path.IsPathRooted(relative)
            || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal)
            || relative.Contains("/../", StringComparison.Ordinal))
            throw new CompositionException(CompositionFailureCode.UnsafePath);
        int depth = relative.Count(static character => character == Path.DirectorySeparatorChar) + 1;
        if (depth > limits.PathDepth)
            throw new CompositionException(CompositionFailureCode.LimitExceeded);
        string normalized = relative.Normalize(NormalizationForm.FormC);
        if (identities.TryGetValue(normalized, out _))
            throw new CompositionException(CompositionFailureCode.PathCollision);
        identities.Add(normalized, relative);
    }

    private static bool IsRejectedName(string name)
    {
        if (name.Length == 0 || name[0] == '.' || name.EndsWith('~'))
            return true;
        string folded = name.ToLowerInvariant();
        if (folded.EndsWith(".tmp", StringComparison.Ordinal)
            || folded.EndsWith(".temp", StringComparison.Ordinal)
            || folded.EndsWith(".bak", StringComparison.Ordinal)
            || folded.EndsWith(".backup", StringComparison.Ordinal))
            return true;
        string stem = Path.GetFileNameWithoutExtension(name);
        return stem.Equals("con", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("prn", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("aux", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("nul", StringComparison.OrdinalIgnoreCase)
            || stem.Length == 4 && (stem.StartsWith("com", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("lpt", StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '1' and <= '9';
    }

    private static FragmentFormat Extension(string relative) => Path.GetExtension(relative) switch
    {
        ".json" => FragmentFormat.Json,
        ".yaml" or ".yml" => FragmentFormat.Yaml,
        _ => throw new CompositionException(CompositionFailureCode.UnsafePath)
    };

    private enum FragmentFormat { Json, Yaml }

    private sealed record SnapshotDirectory(string RelativePath, NativeIdentity Identity);
    private sealed record SnapshotFile(
        string RelativePath, FragmentFormat Format, NativeIdentity Identity, byte[] Bytes, byte[] Digest);

    private sealed record DirectorySnapshot(
        NativeIdentity Root, IReadOnlyList<SnapshotDirectory> Directories, IReadOnlyList<SnapshotFile> Files)
    {
        internal bool IdentityEquals(DirectorySnapshot other)
        {
            if (!Root.StableEquals(other.Root)
                || Directories.Count != other.Directories.Count || Files.Count != other.Files.Count)
                return false;
            for (int index = 0; index < Directories.Count; index++)
            {
                if (Directories[index].RelativePath != other.Directories[index].RelativePath
                    || !Directories[index].Identity.StableEquals(other.Directories[index].Identity))
                    return false;
            }
            for (int index = 0; index < Files.Count; index++)
            {
                SnapshotFile left = Files[index];
                SnapshotFile right = other.Files[index];
                if (left.RelativePath != right.RelativePath || left.Format != right.Format
                    || !left.Identity.StableEquals(right.Identity)
                    || !left.Digest.AsSpan().SequenceEqual(right.Digest))
                    return false;
            }
            return true;
        }
    }

}
