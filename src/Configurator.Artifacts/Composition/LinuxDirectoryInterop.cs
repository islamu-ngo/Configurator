using System.Runtime.InteropServices;
using Configurator.Core.Composition;
using Microsoft.Win32.SafeHandles;

namespace Configurator.Artifacts.Composition;

internal static partial class LinuxDirectoryInterop
{
    internal static (int Directory, int NoFollow) GetOpenFlags(Architecture architecture) =>
        architecture switch
        {
            Architecture.X64 => (0x10000, 0x20000),
            Architecture.Arm64 => (0x4000, 0x8000),
            _ => throw new CompositionException(CompositionFailureCode.UnsupportedFilesystem)
        };

    // Linux statx is a fixed 256-byte ABI. Every field used in equality is
    // requested and checked; access time is deliberately excluded.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct NativeIdentity
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(4)] internal uint BlockSize;
        [FieldOffset(16)] internal uint LinkCount;
        [FieldOffset(20)] internal uint UserId;
        [FieldOffset(24)] internal uint GroupId;
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(32)] internal ulong Inode;
        [FieldOffset(40)] internal long Size;
        [FieldOffset(48)] internal ulong Blocks;
        [FieldOffset(96)] internal long ChangeSeconds;
        [FieldOffset(104)] internal uint ChangeNanoseconds;
        [FieldOffset(112)] internal long ModifySeconds;
        [FieldOffset(120)] internal uint ModifyNanoseconds;
        [FieldOffset(128)] internal uint DeviceTypeMajor;
        [FieldOffset(132)] internal uint DeviceTypeMinor;
        [FieldOffset(136)] internal uint DeviceMajor;
        [FieldOffset(140)] internal uint DeviceMinor;

        internal readonly bool IsRegularFile => (Mode & 0xf000) == 0x8000;
        internal readonly bool IsDirectory => (Mode & 0xf000) == 0x4000;
        internal readonly bool IsLink => (Mode & 0xf000) == 0xa000;
        internal readonly bool IsSpecial => !IsRegularFile && !IsDirectory && !IsLink;

        internal readonly bool StableEquals(NativeIdentity other) =>
            DeviceMajor == other.DeviceMajor && DeviceMinor == other.DeviceMinor
            && Inode == other.Inode && LinkCount == other.LinkCount && Mode == other.Mode
            && UserId == other.UserId && GroupId == other.GroupId
            && DeviceTypeMajor == other.DeviceTypeMajor && DeviceTypeMinor == other.DeviceTypeMinor
            && Size == other.Size && Blocks == other.Blocks && BlockSize == other.BlockSize
            && ModifySeconds == other.ModifySeconds && ModifyNanoseconds == other.ModifyNanoseconds
            && ChangeSeconds == other.ChangeSeconds && ChangeNanoseconds == other.ChangeNanoseconds;

        internal readonly void Validate()
        {
            if ((Mask & 0x7ff) != 0x7ff || Size < 0
                || ChangeNanoseconds >= 1_000_000_000 || ModifyNanoseconds >= 1_000_000_000)
                throw new CompositionException(CompositionFailureCode.UnsupportedFilesystem);
        }

        internal static NativeIdentity ReadPath(string path)
        {
            _ = GetOpenFlags(RuntimeInformation.ProcessArchitecture);
            if (StatPath(-100, path, 0x100, 0x7ff, out NativeIdentity identity) != 0)
                throw new CompositionException(CompositionFailureCode.UnsafeEntry);
            identity.Validate();
            return identity;
        }

        internal static NativeIdentity ReadHandle(SafeFileHandle handle)
        {
            if (StatHandle(handle, "", 0x1000, 0x7ff, out NativeIdentity identity) != 0)
                throw new CompositionException(CompositionFailureCode.UnsupportedFilesystem);
            identity.Validate();
            return identity;
        }

        internal static NativeIdentity ReadRelative(SafeFileHandle directory, string name)
        {
            if (StatHandle(directory, name, 0x100, 0x7ff, out NativeIdentity identity) != 0)
                throw new CompositionException(CompositionFailureCode.UnsafeEntry);
            identity.Validate();
            return identity;
        }
    }

    internal static class NativeMethods
    {
        internal static SafeFileHandle OpenRoot(string root)
        {
            var flags = GetOpenFlags(RuntimeInformation.ProcessArchitecture);
            int descriptor = Open(root, 0x80000 | flags.NoFollow | flags.Directory);
            if (descriptor < 0)
                throw new CompositionException(CompositionFailureCode.UnsupportedFilesystem);
            return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        }

        internal static SafeFileHandle OpenRelative(SafeFileHandle root, string relative, bool directory,
            bool allowMountCrossing = false, bool nonBlocking = false)
        {
            var flags = GetOpenFlags(RuntimeInformation.ProcessArchitecture);
            var how = new OpenHow
            {
                Flags = (ulong)(0x80000 | flags.NoFollow | (directory ? flags.Directory : 0) | (nonBlocking ? 0x800 : 0)),
                Resolve = 0x02UL | 0x04UL | 0x08UL | (allowMountCrossing ? 0UL : 0x01UL)
            };
            long descriptor = SystemCall(437, root, relative, ref how, 24);
            if (descriptor < 0)
                throw new CompositionException(CompositionFailureCode.UnsafeEntry);
            return new SafeFileHandle(checked((IntPtr)descriptor), ownsHandle: true);
        }

        internal static NativeIdentity OpenIdentity(SafeFileHandle root, string relative, bool directory)
        {
            using SafeFileHandle handle = OpenRelative(root, relative, directory);
            return NativeIdentity.ReadHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenHow
    {
        internal ulong Flags;
        internal ulong Mode;
        internal ulong Resolve;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "syscall", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial long SystemCall(long number, SafeFileHandle root, string path, ref OpenHow how, nuint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int StatPath(int root, string path, int flags, uint mask, out NativeIdentity identity);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int StatHandle(SafeFileHandle root, string path, int flags, uint mask, out NativeIdentity identity);
}
