namespace Configurator.Artifacts.Profiles;

using System.Runtime.InteropServices;

/// <summary>
/// Stores independent generic passwords in the user-domain default file keychain.
/// Classic Keychain Services interaction policy is saved, disabled and restored within the
/// common owner's exclusive native operation. No unlock, replacement or deletion is requested.
/// </summary>
internal sealed partial class MacOsKeychainProvider(MacOsKeychainProvider.IKeychainApi api) : IOsKeyringNative
{
    internal static IOsKeyringNative CreateNative() => new MacOsKeychainProvider(new KeychainApi());

    public ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, default, creating: false, cancellationToken);

    public ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken) =>
        RunAsync(id, purpose, canonicalUtf8, creating: true, cancellationToken);

    private async ValueTask<OsKeyringNativeResult> RunAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> value, bool creating, CancellationToken cancellationToken)
    {
        if (!api.IsSupported) return new(OsKeyringStatus.UnsupportedPlatform);
        // Cancellation fences adoption in the common owner; it cannot interrupt these C calls.
        return await Task.Run(() => Execute(id, purpose, value, creating, cancellationToken)).ConfigureAwait(false);
    }

    private OsKeyringNativeResult Execute(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> value, bool creating, CancellationToken token)
    {
        List<nint> owned = [];
        byte priorInteraction = 0;
        bool restoreInteraction = false;
        bool submitted = false;
        OsKeyringCreation creation = OsKeyringCreation.NotAttempted;
        OsKeyringNativeResult? result = null;
        int restoreStatus = 0;
        nint Own(nint handle)
        {
            if (handle == 0) throw new IOException("Keychain allocation unavailable.");
            owned.Add(handle);
            return handle;
        }
        try
        {
            result = Perform();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            result = new(OsKeyringStatus.Unavailable, creation: creation);
        }
        catch (Exception exception) when (exception is ExternalException or IOException or UnauthorizedAccessException)
        {
            result = new(submitted ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Unavailable,
                creation: submitted ? OsKeyringCreation.Uncertain : creation);
        }
        finally
        {
            try
            {
                for (int i = owned.Count - 1; i >= 0; i--) api.Release(owned[i]);
            }
            finally
            {
                if (restoreInteraction) restoreStatus = api.SetInteraction(priorInteraction);
            }
        }
        if (restoreStatus != 0)
        {
            result!.Dispose();
            return new(result.Creation == OsKeyringCreation.Uncertain
                ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Unavailable, creation: result.Creation);
        }
        return result!;

        OsKeyringNativeResult Perform()
        {
            if (token.IsCancellationRequested) return new(OsKeyringStatus.Cancelled);
            int status = api.GetInteraction(out priorInteraction);
            if (status != 0) return new(MapError(status));
            if (priorInteraction != 0)
            {
                status = api.SetInteraction(0);
                if (status != 0) return new(MapError(status));
                restoreInteraction = true;
            }
            // Explicit user preference domain, never a system-wide or default search-list query.
            status = api.CopyDefaultKeychain(0, out nint keychain);
            if (keychain != 0) owned.Add(keychain);
            if (status != 0) return new(MapError(status));
            if (keychain == 0) return new(OsKeyringStatus.Unavailable);

            nint service = Own(api.CreateString($"org.islamu.configurator/profile/{purpose}"));
            nint account = Own(api.CreateString(id.ToString("N")));
            nint scope = creating ? keychain : Own(api.CreateArray([keychain]));
            nint data = creating ? Own(api.CreateData(value.Span)) : 0;
            nint[] keys = creating
                ? [api.Constant("kSecClass"), api.Constant("kSecAttrService"), api.Constant("kSecAttrAccount"),
                    api.Constant("kSecUseKeychain"), api.Constant("kSecValueData")]
                : [api.Constant("kSecClass"), api.Constant("kSecAttrService"), api.Constant("kSecAttrAccount"),
                    api.Constant("kSecMatchSearchList"), api.Constant("kSecReturnData"), api.Constant("kSecMatchLimit")];
            nint[] values = creating
                ? [api.Constant("kSecClassGenericPassword"), service, account, scope, data]
                : [api.Constant("kSecClassGenericPassword"), service, account, scope,
                    api.Constant("kCFBooleanTrue"), api.Constant("kSecMatchLimitOne")];
            nint query = Own(api.CreateDictionary(keys, values));
            if (token.IsCancellationRequested) return new(OsKeyringStatus.Cancelled);
            if (creating)
            {
                submitted = true;
                status = api.Add(query);
                if (status == 0)
                {
                    creation = OsKeyringCreation.Created;
                    return new(OsKeyringStatus.Success, creation: creation);
                }
                if (status is not (-50 or -128 or -25291 or -25293 or -25243 or -25299 or -25300 or -25308 or -25315))
                    return new(OsKeyringStatus.CreationUncertain, creation: OsKeyringCreation.Uncertain);
                return new(MapError(status));
            }

            nint returned = 0;
            try
            {
                status = api.CopyMatching(query, out returned);
                if (status != 0) return new(status == -25300 ? OsKeyringStatus.Missing : MapError(status));
                if (returned == 0 || api.GetTypeId(returned) != api.GetDataTypeId() || api.GetDataLength(returned) != 44)
                    return new(OsKeyringStatus.InvalidValue);
                nint bytes = api.GetDataBytes(returned);
                if (bytes == 0) return new(OsKeyringStatus.InvalidValue);
                byte[] copied = new byte[44];
                Marshal.Copy(bytes, copied, 0, copied.Length);
                // The common provider admits canonical Base64 and owns/clears this bounded copy.
                return new(OsKeyringStatus.Success, copied);
            }
            finally { if (returned != 0) api.Release(returned); }
        }
    }

    private static OsKeyringStatus MapError(int status) => status switch
    {
        -50 or -25299 => OsKeyringStatus.InvalidRequest, // errSecParam / errSecDuplicateItem
        -128 => OsKeyringStatus.Cancelled, // errSecUserCanceled
        -25293 or -25243 => OsKeyringStatus.Denied, // errSecAuthFailed / errSecNoAccessForItem
        // Interaction refusal is locked custody, not evidence that the requested item is missing.
        -25308 or -25315 => OsKeyringStatus.Locked,
        _ => OsKeyringStatus.Unavailable
    };

    internal interface IKeychainApi
    {
        bool IsSupported { get; }
        int GetInteraction(out byte allowed);
        int SetInteraction(byte allowed);
        int CopyDefaultKeychain(int domain, out nint keychain);
        nint Constant(string name);
        nint CreateString(string value);
        nint CreateData(ReadOnlySpan<byte> value);
        nint CreateArray(ReadOnlySpan<nint> values);
        nint CreateDictionary(ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values);
        int Add(nint attributes);
        int CopyMatching(nint query, out nint result);
        nuint GetTypeId(nint value);
        nuint GetDataTypeId();
        nint GetDataLength(nint value);
        nint GetDataBytes(nint value);
        void Release(nint value);
    }

    private sealed unsafe class KeychainApi : IKeychainApi
    {
        public bool IsSupported => OperatingSystem.IsMacOS();
        public int GetInteraction(out byte allowed) => Native.SecKeychainGetUserInteractionAllowed(out allowed);
        public int SetInteraction(byte allowed) => Native.SecKeychainSetUserInteractionAllowed(allowed);
        public int CopyDefaultKeychain(int domain, out nint keychain) => Native.SecKeychainCopyDomainDefault(domain, out keychain);
        public nint Constant(string name)
        {
            // Framework references live for the process; exported CF globals remain borrowed.
            nint library = name.StartsWith("kCF", StringComparison.Ordinal) ? Exports.CoreFoundation.Value : Exports.Security.Value;
            return Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));
        }
        public nint CreateString(string value)
        {
            fixed (char* characters = value)
                return Native.CFStringCreateWithCharacters(0, characters, value.Length);
        }
        public nint CreateData(ReadOnlySpan<byte> value)
        {
            fixed (byte* bytes = value) return Native.CFDataCreate(0, bytes, value.Length);
        }
        public nint CreateArray(ReadOnlySpan<nint> values)
        {
            // Null callbacks leave retention with Execute's owned list until physical completion.
            fixed (nint* pointer = values) return Native.CFArrayCreate(0, pointer, values.Length, 0);
        }
        public nint CreateDictionary(ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values)
        {
            fixed (nint* keyPointer = keys)
            fixed (nint* valuePointer = values)
                return Native.CFDictionaryCreate(0, keyPointer, valuePointer, keys.Length, 0, 0);
        }
        public int Add(nint attributes) => Native.SecItemAdd(attributes, 0);
        public int CopyMatching(nint query, out nint result) => Native.SecItemCopyMatching(query, out result);
        public nuint GetTypeId(nint value) => Native.CFGetTypeID(value);
        public nuint GetDataTypeId() => Native.CFDataGetTypeID();
        public nint GetDataLength(nint value) => Native.CFDataGetLength(value);
        public nint GetDataBytes(nint value) => Native.CFDataGetBytePtr(value);
        public void Release(nint value) => Native.CFRelease(value);

        private static class Exports
        {
            internal static readonly Lazy<nint> Security = new(() => NativeLibrary.Load(Native.Security));
            internal static readonly Lazy<nint> CoreFoundation = new(() => NativeLibrary.Load(Native.CoreFoundation));
        }
    }

    // Public C ABI only. OSStatus is signed 32-bit, Boolean is one byte,
    // CFIndex is signed pointer-width and CFTypeID is unsigned pointer-width.
    private static unsafe partial class Native
    {
        internal const string Security = "/System/Library/Frameworks/Security.framework/Security";
        internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [LibraryImport(Security)]
        internal static partial int SecKeychainGetUserInteractionAllowed(out byte allowed);
        [LibraryImport(Security)]
        internal static partial int SecKeychainSetUserInteractionAllowed(byte allowed);
        [LibraryImport(Security)]
        internal static partial int SecKeychainCopyDomainDefault(int domain, out nint keychain);
        [LibraryImport(Security)]
        internal static partial int SecItemAdd(nint attributes, nint result);
        [LibraryImport(Security)]
        internal static partial int SecItemCopyMatching(nint query, out nint result);
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFStringCreateWithCharacters(nint allocator, char* characters, nint count);
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFDataCreate(nint allocator, byte* bytes, nint length);
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFArrayCreate(nint allocator, nint* values, nint count, nint callbacks);
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFDictionaryCreate(nint allocator, nint* keys, nint* values,
            nint count, nint keyCallbacks, nint valueCallbacks);
        [LibraryImport(CoreFoundation)]
        internal static partial nuint CFGetTypeID(nint value);
        [LibraryImport(CoreFoundation)]
        internal static partial nuint CFDataGetTypeID();
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFDataGetLength(nint value);
        [LibraryImport(CoreFoundation)]
        internal static partial nint CFDataGetBytePtr(nint value);
        [LibraryImport(CoreFoundation)]
        internal static partial void CFRelease(nint value);
    }
}
