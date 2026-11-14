namespace Configurator.Artifacts.Profiles;

using System.Buffers.Text;
using System.Security.Cryptography;
using Configurator.Core.Profiles;

/// <summary>Owns clearable raw key material. Public inspection exposes identity and purpose only.</summary>
public sealed class OsKeyringSecretLease : IDisposable
{
    private readonly object _access = new();
    private byte[]? _key;

    internal OsKeyringSecretLease(Guid keyId, OsKeyringPurpose purpose, ReadOnlySpan<byte> key)
    {
        if (keyId == Guid.Empty || !Enum.IsDefined(purpose) || key.Length != 32)
            throw new ArgumentException("Invalid keyring material.");
        KeyId = keyId;
        Purpose = purpose;
        _key = key.ToArray();
    }

    public Guid KeyId { get; }
    public OsKeyringPurpose Purpose { get; }

    /// <summary>Creates an independent disposable commitment owner without exporting key bytes.</summary>
    public ProfileSecretCommitment CreateCommitment()
    {
        lock (_access)
        {
            ObjectDisposedException.ThrowIf(_key is null, this);
            if (Purpose != OsKeyringPurpose.ComparisonKey)
                throw new InvalidOperationException("The key purpose does not permit commitments.");
            return new ProfileSecretCommitment(KeyId, _key);
        }
    }

    /// <summary>Trusted Artifacts orchestration owns and must clear the returned credential.</summary>
    internal byte[] CopyRecipientUtf8()
    {
        lock (_access)
        {
            ObjectDisposedException.ThrowIf(_key is null, this);
            if (Purpose != OsKeyringPurpose.NativeRecipient)
                throw new InvalidOperationException("The key purpose does not permit recipient use.");
            byte[] encoded = new byte[44];
            Base64.EncodeToUtf8(_key, encoded, out _, out _);
            return encoded;
        }
    }

    public void Dispose()
    {
        lock (_access)
        {
            if (_key is null) return;
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }

    public override string ToString() => $"{nameof(OsKeyringSecretLease)}:{KeyId:N}:{Purpose}";
}
