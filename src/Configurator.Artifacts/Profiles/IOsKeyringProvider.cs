namespace Configurator.Artifacts.Profiles;

using System.Security.Cryptography;
using System.Text.Json.Serialization;

/// <summary>Lookup never creates; creation selects a fresh identifier and independent key internally.</summary>
public interface IOsKeyringProvider
{
    ValueTask<OsKeyringResult> LookupAsync(Guid keyId, OsKeyringPurpose purpose, CancellationToken cancellationToken = default);
    ValueTask<OsKeyringResult> CreateAsync(OsKeyringPurpose purpose, CancellationToken cancellationToken = default);
}

public enum OsKeyringPurpose { NativeRecipient, ComparisonKey }
public enum OsKeyringStatus
{
    Success, Missing, ServiceMissing, Locked, Denied, Unavailable, Busy, Cancelled,
    CreationUncertain, UnsupportedPlatform, InvalidRequest, InvalidValue
}
public enum OsKeyringCreation { NotAttempted, Created, Uncertain }

/// <summary>Value-free native recipient refusal; native messages and paths are never retained.</summary>
public sealed class OsKeyringException(OsKeyringStatus status) : Exception(status.ToString())
{
    public OsKeyringStatus Status { get; } = status;
    public override string ToString() => $"{nameof(OsKeyringException)}:{Status}";
}

/// <summary>
/// Metadata describes the observed operation, not continuing custody. Disposal clears the lease,
/// never deletes an OS entry. Retain KeyId when Creation is Uncertain, even without a lease.
/// </summary>
public sealed class OsKeyringResult : IDisposable
{
    internal OsKeyringResult(OsKeyringStatus status, Guid keyId, OsKeyringPurpose purpose,
        OsKeyringCreation creation = OsKeyringCreation.NotAttempted, OsKeyringSecretLease? lease = null)
    {
        Status = status;
        KeyId = keyId;
        Purpose = purpose;
        Creation = creation;
        Lease = lease;
    }

    public OsKeyringStatus Status { get; }
    public Guid KeyId { get; }
    public OsKeyringPurpose Purpose { get; }
    public OsKeyringCreation Creation { get; }
    [JsonIgnore]
    public OsKeyringSecretLease? Lease { get; }
    public void Dispose() => Lease?.Dispose();
    public override string ToString() => $"{nameof(OsKeyringResult)}:{Status}:{KeyId:N}:{Purpose}:{Creation}";
}

/// <summary>The OS boundary completes only after physical native work and cleanup finish.</summary>
internal interface IOsKeyringNative
{
    ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken);
    ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
        ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken);
}

internal sealed class OsKeyringNativeResult(OsKeyringStatus status, byte[]? value = null,
    OsKeyringCreation creation = OsKeyringCreation.NotAttempted) : IDisposable
{
    internal OsKeyringStatus Status { get; } = status;
    internal byte[]? Value { get; } = value;
    internal OsKeyringCreation Creation { get; } = creation;
    public void Dispose()
    {
        if (Value is not null) CryptographicOperations.ZeroMemory(Value);
    }
}
