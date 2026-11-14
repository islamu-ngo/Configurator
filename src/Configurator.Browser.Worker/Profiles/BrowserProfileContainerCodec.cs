namespace Configurator.Browser.Worker.Profiles;

using System.Security.Cryptography;
using Configurator.Core.Profiles;

/// <summary>The worker's maintained high-level cryptographic boundary, implemented by private JS imports.</summary>
public interface IProfileCrypto
{
    ValueTask<byte[]> DecryptAsync(byte[] container, int recipientIndex, byte[] passphrase);
    ValueTask<byte[]> EncryptAsync(byte[] payload, IReadOnlyList<byte[]> recipients);
    void Clear();
}

/// <summary>Admits through Core before exactly one library call. There is no native device recipient.</summary>
public sealed class BrowserProfileContainerCodec(IProfileCrypto crypto) : IProfileContainerCodec
{
    private static int _active;
    public Guid? DeviceRecipientId => null;

    public async ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
        ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (container.Length is 0 or > ProfileContainerPolicy.MaximumContainerBytes)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        if (passphraseUtf8.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
        byte[] owned = container.ToArray();
        byte[] credential = passphraseUtf8.ToArray();
        byte[]? plaintext = null;
        bool entered = false;
        try
        {
            ProfileContainerPolicy.ValidatePassphrase(credential, creating: false);
            int recipients = ProfileContainerPolicy.Admit(owned);
            if (recipientIndex < 0 || recipientIndex >= recipients)
                throw new ProfileException(ProfileFailureCode.InvalidContainer);
            Enter();
            entered = true;
            cancellationToken.ThrowIfCancellationRequested();
            try { plaintext = await crypto.DecryptAsync(owned, recipientIndex, credential).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OperationCanceledException and not ProfileException)
            {
                throw new ProfileException(ProfileFailureCode.AuthenticationFailed);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return ProfilePayloadCodec.Admit(plaintext);
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(owned);
            CryptographicOperations.ZeroMemory(credential);
            if (entered)
            {
                try { crypto.Clear(); }
                finally { Volatile.Write(ref _active, 0); }
            }
        }
    }

    public async ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
        IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = recipientMaterials.Count;
        if (payload.Length is 0 or > ProfileContainerPolicy.MaximumPayloadBytes ||
            count is < 1 or > ProfileContainerPolicy.MaximumRecipients)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        byte[] owned = payload.ToArray();
        var recipients = new List<byte[]>();
        byte[]? encrypted = null;
        bool entered = false;
        try
        {
            for (int index = 0; index < count; index++)
            {
                ReadOnlyMemory<byte> material = recipientMaterials[index];
                if (material.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                byte[] snapshot = material.ToArray();
                recipients.Add(snapshot);
                ProfileContainerPolicy.ValidatePassphrase(snapshot, creating: true);
            }
            ProfilePayloadCodec.Validate(owned);
            Enter();
            entered = true;
            cancellationToken.ThrowIfCancellationRequested();
            try { encrypted = await crypto.EncryptAsync(owned, recipients).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OperationCanceledException and not ProfileException)
            {
                throw new ProfileException(ProfileFailureCode.AuthenticationFailed);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (ProfileContainerPolicy.Admit(encrypted) != count)
                throw new ProfileException(ProfileFailureCode.InvalidContainer);
            byte[] result = encrypted;
            encrypted = null;
            return result;
        }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            CryptographicOperations.ZeroMemory(owned);
            foreach (byte[] bytes in recipients) CryptographicOperations.ZeroMemory(bytes);
            if (entered)
            {
                try { crypto.Clear(); }
                finally { Volatile.Write(ref _active, 0); }
            }
        }
    }

    private static void Enter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new ProfileException(ProfileFailureCode.Busy);
    }
}
