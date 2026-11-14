namespace Configurator.Artifacts.Profiles;

using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// User-token Credential Manager storage. CredWrite replaces an existing target/type: only the
/// common provider's fresh identifiers may enter creation. There is no create-only OS primitive.
/// Synchronous OS work completes, including cleanup, before the common owner releases admission.
/// </summary>
internal sealed partial class WindowsCredentialKeyringProvider(
    WindowsCredentialKeyringProvider.ICredentialApi api) : IOsKeyringNative
{
    internal static IOsKeyringNative CreateNative() => new WindowsCredentialKeyringProvider(new CredentialApi());

    public ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, default, creating: false, cancellationToken);

    public ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, canonicalUtf8, creating: true, cancellationToken);

    private async ValueTask<OsKeyringNativeResult> RunAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> value, bool creating, CancellationToken cancellationToken)
    {
        if (!api.IsSupported) return new(OsKeyringStatus.UnsupportedPlatform);
        // Never pass cancellation to Task.Run: cancellation cannot abandon physical work.
        return await Task.Run(() => Execute(id, purpose, value, creating, cancellationToken)).ConfigureAwait(false);
    }

    private unsafe OsKeyringNativeResult Execute(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> value, bool creating, CancellationToken token)
    {
        if (token.IsCancellationRequested) return new(OsKeyringStatus.Cancelled);
        string target = $"org.islamu.configurator/profile/{purpose}/{id:N}";
        bool writeStarted = false;
        try
        {
            if (!creating)
            {
                nint allocation = 0;
                try
                {
                    if (!api.Read(target, out allocation, out int error)) return new(MapError(error, creating: false));
                    if (allocation == 0) return new(OsKeyringStatus.InvalidValue);
                    Credential credential = Marshal.PtrToStructure<Credential>(allocation);
                    // CredentialBlobSize is bytes, not characters; OS maximum is 2560.
                    // Our admitted value is stricter: canonical Base64 UTF-8, exactly 44 bytes.
                    if (credential.Type != 1 || credential.CredentialBlobSize != 44 || credential.CredentialBlob == 0)
                        return new(OsKeyringStatus.InvalidValue);
                    byte[] owned = new byte[44];
                    Marshal.Copy(credential.CredentialBlob, owned, 0, owned.Length);
                    return new(OsKeyringStatus.Success, owned);
                }
                finally
                {
                    if (allocation != 0) api.Free(allocation);
                }
            }

            using MemoryHandle pinned = value.Pin();
            nint targetUtf16 = Marshal.StringToHGlobalUni(target);
            try
            {
                Credential credential = new()
                {
                    Type = 1, TargetName = targetUtf16,
                    CredentialBlobSize = (uint)value.Length, CredentialBlob = (nint)pinned.Pointer,
                    // LOCAL_MACHINE persists for this user on this computer, not all users.
                    Persist = 2
                };
                if (token.IsCancellationRequested) return new(OsKeyringStatus.Cancelled);
                writeStarted = true;
                if (!api.Write(ref credential, out int error)) return new(MapError(error, creating: true));
                return new(OsKeyringStatus.Success, creation: OsKeyringCreation.Created);
            }
            finally { Marshal.FreeHGlobal(targetUtf16); }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return new(OsKeyringStatus.Unavailable);
        }
        catch (Exception exception) when (exception is ExternalException or IOException or UnauthorizedAccessException)
        {
            return new(writeStarted ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Unavailable,
                creation: writeStarted ? OsKeyringCreation.Uncertain : OsKeyringCreation.NotAttempted);
        }
    }

    private static OsKeyringStatus MapError(int error, bool creating) => error switch
    {
        1168 when !creating => OsKeyringStatus.Missing, // ERROR_NOT_FOUND
        5 => OsKeyringStatus.Denied, // ERROR_ACCESS_DENIED
        170 => OsKeyringStatus.Busy, // ERROR_BUSY
        1223 => OsKeyringStatus.Cancelled, // ERROR_CANCELLED
        87 or 1004 => OsKeyringStatus.InvalidRequest, // INVALID_PARAMETER / INVALID_FLAGS
        // NO_SUCH_LOGON_SESSION (1312) is unavailable custody, not a missing key or "locked".
        _ => OsKeyringStatus.Unavailable
    };

    internal interface ICredentialApi
    {
        bool IsSupported { get; }
        bool Read(string target, out nint credential, out int error);
        bool Write(ref Credential credential, out int error);
        void Free(nint credential);
    }

    // Public Windows ABI fields; pointer members are deliberately not marshalled as managed strings.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Credential
    {
        internal uint Flags;
        internal uint Type;
        internal nint TargetName;
        internal nint Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal nint CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal nint Attributes;
        internal nint TargetAlias;
        internal nint UserName;
    }

    private sealed class CredentialApi : ICredentialApi
    {
        public bool IsSupported => OperatingSystem.IsWindows();

        public bool Read(string target, out nint credential, out int error)
        {
            credential = 0;
            error = 0;
            if (!OperatingSystem.IsWindows()) return false;
            int success = Native.CredRead(target, 1, 0, out credential);
            error = Marshal.GetLastPInvokeError();
            return success != 0;
        }

        public unsafe bool Write(ref Credential credential, out int error)
        {
            error = 0;
            if (!OperatingSystem.IsWindows()) return false;
            fixed (Credential* pointer = &credential)
            {
                int success = Native.CredWrite(pointer, 0);
                error = Marshal.GetLastPInvokeError();
                return success != 0;
            }
        }

        public void Free(nint credential)
        {
            if (OperatingSystem.IsWindows()) Native.CredFree(credential);
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe partial class Native
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [LibraryImport("Advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        internal static partial int CredRead(string target, uint type, uint flags, out nint credential);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [LibraryImport("Advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
        internal static partial int CredWrite(Credential* credential, uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [LibraryImport("Advapi32.dll", EntryPoint = "CredFree")]
        internal static partial void CredFree(nint credential);
    }
}
