namespace Configurator.Artifacts.Profiles;

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

/// <summary>
/// One nonqueued process owner admits native work. Cancellation never abandons native work or
/// releases its permit early. Unsupported hosts refuse without another persistence mechanism.
/// </summary>
public sealed class OsKeyringProvider : IOsKeyringProvider
{
    private static readonly SemaphoreSlim Work = new(1, 1);
    private readonly IOsKeyringNative? _native;

    public OsKeyringProvider() : this(OperatingSystem.IsLinux() ? new LinuxLibSecretKeyringProvider()
        : OperatingSystem.IsWindows() ? WindowsCredentialKeyringProvider.CreateNative()
        : OperatingSystem.IsMacOS() ? MacOsKeychainProvider.CreateNative() : null) { }
    internal OsKeyringProvider(IOsKeyringNative? native) => _native = native;

    public ValueTask<OsKeyringResult> LookupAsync(Guid keyId, OsKeyringPurpose purpose, CancellationToken cancellationToken = default) =>
        ExecuteAsync(keyId, purpose, creating: false, cancellationToken);

    public ValueTask<OsKeyringResult> CreateAsync(OsKeyringPurpose purpose, CancellationToken cancellationToken = default) =>
        ExecuteAsync(Guid.Empty, purpose, creating: true, cancellationToken);

    private async ValueTask<OsKeyringResult> ExecuteAsync(Guid id, OsKeyringPurpose purpose,
        bool creating, CancellationToken cancellationToken)
    {
        OsKeyringResult Refuse(OsKeyringStatus status, OsKeyringCreation creation = OsKeyringCreation.NotAttempted) =>
            new(status, id, purpose, creation);
        if (!Enum.IsDefined(purpose) || (!creating && id == Guid.Empty)) return Refuse(OsKeyringStatus.InvalidRequest);
        if (_native is null) return Refuse(OsKeyringStatus.UnsupportedPlatform);
        if (cancellationToken.IsCancellationRequested) return Refuse(OsKeyringStatus.Cancelled);
        if (!Work.Wait(0, CancellationToken.None)) return Refuse(OsKeyringStatus.Busy);
        byte[]? key = null;
        byte[]? encoded = null;
        bool submitted = false;
        try
        {
            if (cancellationToken.IsCancellationRequested) return Refuse(OsKeyringStatus.Cancelled);
            if (creating)
            {
                id = Guid.NewGuid();
                key = RandomNumberGenerator.GetBytes(32);
                encoded = new byte[44];
                Base64.EncodeToUtf8(key, encoded, out _, out _);
            }
            submitted = creating;
            using OsKeyringNativeResult native = creating
                ? await _native.CreateAsync(id, purpose, encoded!, cancellationToken).ConfigureAwait(false)
                : await _native.LookupAsync(id, purpose, cancellationToken).ConfigureAwait(false);
            if (native.Creation == OsKeyringCreation.Uncertain)
                return Refuse(OsKeyringStatus.CreationUncertain, native.Creation);
            if (cancellationToken.IsCancellationRequested)
                return Refuse(OsKeyringStatus.Cancelled, native.Creation);
            if (native.Status != OsKeyringStatus.Success) return Refuse(native.Status, native.Creation);
            if (!creating)
            {
                key = new byte[32];
                encoded = new byte[44];
                if (native.Value is not { Length: 44 } ||
                    Base64.DecodeFromUtf8(native.Value, key, out int consumed, out int written) != OperationStatus.Done ||
                    consumed != 44 || written != 32)
                    return Refuse(OsKeyringStatus.InvalidValue);
                Base64.EncodeToUtf8(key, encoded, out _, out _);
                if (!encoded.AsSpan().SequenceEqual(native.Value)) return Refuse(OsKeyringStatus.InvalidValue);
            }
            return new(OsKeyringStatus.Success, id, purpose, native.Creation, new OsKeyringSecretLease(id, purpose, key!));
        }
        catch (OperationCanceledException)
        {
            return Refuse(submitted ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Cancelled,
                submitted ? OsKeyringCreation.Uncertain : OsKeyringCreation.NotAttempted);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.Runtime.InteropServices.ExternalException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Refuse(submitted ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Unavailable,
                submitted ? OsKeyringCreation.Uncertain : OsKeyringCreation.NotAttempted);
        }
        finally
        {
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (encoded is not null) CryptographicOperations.ZeroMemory(encoded);
            Work.Release();
        }
    }
}
