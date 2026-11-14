namespace Configurator.Artifacts.Profiles;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Artifacts.Composition;
using Configurator.Core.Profiles;
using Microsoft.Win32.SafeHandles;
using static Configurator.Artifacts.Composition.LinuxDirectoryInterop;

internal static partial class ProfileFileReader
{
    internal const int MaximumEntries = 4096;

    internal static NativeIdentity EnsureRoot(string root)
    {
        using SafeFileHandle handle = OpenRoot(root, create: true);
        return NativeIdentity.ReadHandle(handle);
    }

    internal static bool RootMatches(string root, NativeIdentity expected)
    {
        try
        {
            using SafeFileHandle handle = OpenRoot(root, create: false);
            return SameRoot(expected, NativeIdentity.ReadHandle(handle));
        }
        catch (Exception exception) when (exception is ProfileVaultStoreException or CompositionException or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static IReadOnlyList<ProfileRevisionDescriptor> List(string root, CancellationToken cancellationToken,
        Action? afterFileObservation = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SafeFileHandle directory = OpenRoot(root, create: false);
        NativeIdentity rootIdentity = NativeIdentity.ReadHandle(directory);
        var results = new List<ProfileRevisionDescriptor>();
        int count = 0;
        bool addedReference = false;
        directory.DangerousAddRef(ref addedReference);
        try
        {
            // Only our held descriptor is followed through procfs; the operator pathname never drives enumeration.
            string descriptorPath = "/proc/self/fd/" + directory.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture);
            foreach (string path in Directory.EnumerateFileSystemEntries(descriptorPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > MaximumEntries) throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
                string name = Path.GetFileName(path);
                if (!ProfileRevisionDescriptor.TryParse(name, out Guid profileId, out Guid snapshotId, out ProfileFileKind kind))
                    continue;
                NativeIdentity named = NativeIdentity.ReadRelative(directory, name);
                ValidateFile(named, kind);
                using SafeFileHandle file = NativeMethods.OpenRelative(directory, name, directory: false, nonBlocking: true);
                NativeIdentity identity = NativeIdentity.ReadHandle(file);
                ValidateFile(identity, kind);
                if (!identity.StableEquals(named)) Changed();
                afterFileObservation?.Invoke();
                if (!identity.StableEquals(NativeIdentity.ReadRelative(directory, name))) Changed();
                results.Add(new(profileId, snapshotId, kind, rootIdentity, identity));
            }
        }
        finally { if (addedReference) directory.DangerousRelease(); }
        if (!RootMatches(root, rootIdentity)) Changed();
        cancellationToken.ThrowIfCancellationRequested();
        return results.OrderBy(item => item.ProfileId).ThenBy(item => item.SnapshotId).ThenBy(item => item.Kind).ToArray();
    }

    internal static async ValueTask<byte[]> ReadAsync(string root, ProfileRevisionDescriptor revision,
        CancellationToken cancellationToken, Action? afterFileObservation = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SafeFileHandle directory = OpenRoot(root, create: false);
        NativeIdentity rootIdentity = NativeIdentity.ReadHandle(directory);
        if (!SameRoot(revision.Root, rootIdentity)) Changed();
        NativeIdentity named = NativeIdentity.ReadRelative(directory, revision.Name);
        ValidateFile(named, revision.Kind);
        if (!revision.File.StableEquals(named)) Changed();
        using SafeFileHandle handle = NativeMethods.OpenRelative(directory, revision.Name, directory: false, nonBlocking: true);
        NativeIdentity identity = NativeIdentity.ReadHandle(handle);
        ValidateFile(identity, revision.Kind);
        if (!revision.File.StableEquals(identity)) Changed();
        using var stream = new FileStream(handle, FileAccess.Read);
        byte[]? bytes = new byte[checked((int)identity.Size)];
        try
        {
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            afterFileObservation?.Invoke();
            if (!identity.StableEquals(NativeIdentity.ReadHandle(handle)) ||
                !identity.StableEquals(NativeIdentity.ReadRelative(directory, revision.Name)) ||
                !RootMatches(root, rootIdentity)) Changed();
            cancellationToken.ThrowIfCancellationRequested();
            byte[] transferred = bytes;
            bytes = null;
            return transferred;
        }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); }
    }

    private static SafeFileHandle OpenRoot(string root, bool create)
    {
        if (!OperatingSystem.IsLinux())
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsupportedPlatform);
        string[] segments = root.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!Path.IsPathFullyQualified(root) || root.Length > 4096 || segments.Length > 64)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
        SafeFileHandle? current = NativeMethods.OpenRoot("/");
        try
        {
            ValidateDirectory(NativeIdentity.ReadHandle(current), final: segments.Length == 0);
            for (int index = 0; index < segments.Length; index++)
            {
                SafeFileHandle next;
                try
                {
                    next = NativeMethods.OpenRelative(current, segments[index], directory: true, allowMountCrossing: true);
                }
                catch (CompositionException) when (create && Marshal.GetLastPInvokeError() == 2)
                {
                    if (MakeDirectoryAt(current, segments[index], 0x1c0) != 0 && Marshal.GetLastPInvokeError() != 17)
                        throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsafeTarget);
                    next = NativeMethods.OpenRelative(current, segments[index], directory: true, allowMountCrossing: true);
                }
                SafeFileHandle previous = current;
                current = next;
                previous.Dispose();
                ValidateDirectory(NativeIdentity.ReadHandle(current), final: index == segments.Length - 1);
            }
            SafeFileHandle transferred = current;
            current = null;
            return transferred;
        }
        finally { current?.Dispose(); }
    }

    private static void ValidateDirectory(NativeIdentity identity, bool final)
    {
        if (!OperatingSystem.IsLinux())
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsupportedPlatform);
        uint user = UnixProtectedArtifactWriter.CurrentEffectiveUserId;
        if (!identity.IsDirectory || identity.UserId != 0 && identity.UserId != user ||
            (identity.Mode & 0x12) != 0 && (final || identity.UserId != 0 || (identity.Mode & 0x200) == 0) ||
            final && (identity.UserId != user || identity.Mode != (0x4000 | 0x1c0)))
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsafeTarget);
    }

    private static void ValidateFile(NativeIdentity identity, ProfileFileKind kind)
    {
        if (!OperatingSystem.IsLinux())
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsupportedPlatform);
        int maximum = kind == ProfileFileKind.Encrypted
            ? ProfileContainerPolicy.MaximumContainerBytes : ProfileContainerPolicy.MaximumPayloadBytes;
        if (!identity.IsRegularFile || identity.UserId != UnixProtectedArtifactWriter.CurrentEffectiveUserId ||
            identity.Mode != (0x8000 | 0x180) || identity.LinkCount != 1)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsafeTarget);
        if (identity.Size < 1 || identity.Size > maximum)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
    }

    private static bool SameRoot(NativeIdentity left, NativeIdentity right) =>
        left.Inode == right.Inode && left.DeviceMajor == right.DeviceMajor && left.DeviceMinor == right.DeviceMinor &&
        left.UserId == right.UserId && left.GroupId == right.GroupId && left.Mode == right.Mode;

    private static void Changed() => throw new ProfileVaultStoreException(ProtectedArtifactStatus.TargetChanged);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "mkdirat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int MakeDirectoryAt(SafeFileHandle directory, string name, uint mode);
}
