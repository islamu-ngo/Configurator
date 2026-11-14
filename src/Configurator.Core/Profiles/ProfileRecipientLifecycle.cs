namespace Configurator.Core.Profiles;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Seals authenticated application data without exporting retained plaintext or generating host identities.</summary>
public static class ProfileRecipientLifecycle
{
    public static ValueTask<ProfileSealedSnapshot> SealAsync(UnlockedProfile profile, IProfileContainerCodec codec,
        IReadOnlyList<ReadOnlyMemory<byte>> portableRecipientMaterials, Guid? deviceBoundAcknowledgement = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.EnsureAlive();
        if (profile.IsSourceSubset)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.InvalidSnapshotIdentity);
        return SealOwnedAsync(profile, profile.SnapshotId, codec, portableRecipientMaterials,
            deviceBoundAcknowledgement, cancellationToken);
    }

    /// <summary>
    /// The host supplies a fresh nonempty snapshot ID. Existing files and recipients remain unchanged;
    /// the codec creates new encryption keys/nonces, while target credentials and payload custody are retained.
    /// </summary>
    public static ValueTask<ProfileSealedSnapshot> ResealAsync(UnlockedProfile profile, Guid newSnapshotId,
        IProfileContainerCodec codec, IReadOnlyList<ReadOnlyMemory<byte>> portableRecipientMaterials,
        Guid? deviceBoundAcknowledgement = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (newSnapshotId == Guid.Empty || newSnapshotId == profile.SnapshotId)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.InvalidSnapshotIdentity);
        return SealOwnedAsync(profile, newSnapshotId, codec, portableRecipientMaterials,
            deviceBoundAcknowledgement, cancellationToken);
    }

    private static async ValueTask<ProfileSealedSnapshot> SealOwnedAsync(UnlockedProfile profile, Guid snapshotId,
        IProfileContainerCodec codec, IReadOnlyList<ReadOnlyMemory<byte>> portableRecipientMaterials,
        Guid? deviceBoundAcknowledgement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(portableRecipientMaterials);
        cancellationToken.ThrowIfCancellationRequested();
        profile.EnsureAlive();
        Guid? deviceId = codec.DeviceRecipientId;
        int portableCount = portableRecipientMaterials.Count;
        int maximumPortable = ProfileContainerPolicy.MaximumRecipients - (deviceId is null ? 0 : 1);
        if (deviceId == Guid.Empty || portableCount < 0 || portableCount > maximumPortable ||
            (portableCount == 0 && deviceId is null))
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        if (portableCount == 0 && deviceBoundAcknowledgement != profile.ProfileId)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.DeviceBoundAcknowledgementRequired);

        byte[] payload = profile.CopyPayload();
        var buffer = new ArrayBufferWriter<byte>(payload.Length);
        byte[]? rewritten = null;
        byte[]? encrypted = null;
        var ownedMaterials = new byte[portableCount][];
        var materials = new ReadOnlyMemory<byte>[portableCount];
        try
        {
            for (int index = 0; index < portableCount; index++)
            {
                ReadOnlyMemory<byte> supplied = portableRecipientMaterials[index];
                if (supplied.Length is 0 or > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
                    throw new ProfileException(ProfileFailureCode.InvalidPassphrase);
                ownedMaterials[index] = supplied.ToArray();
                materials[index] = ownedMaterials[index];
            }
            using (JsonDocument document = JsonDocument.Parse(payload, ProfilePayloadCodec.DocumentOptions))
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("snapshotId")) writer.WriteString("snapshotId", snapshotId);
                    else property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            rewritten = buffer.WrittenSpan.ToArray();
            encrypted = await codec.SealAsync(rewritten, materials, cancellationToken).ConfigureAwait(false);
            var reference = new ProfileRecoveryReference(profile.ProfileId, snapshotId,
                ArtifactDigest.Compute(encrypted), deviceId, portableCount != 0);
            cancellationToken.ThrowIfCancellationRequested();
            profile.EnsureAlive();
            var result = new ProfileSealedSnapshot(reference, encrypted);
            encrypted = null;
            return result;
        }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (rewritten is not null) CryptographicOperations.ZeroMemory(rewritten);
            buffer.Clear();
            CryptographicOperations.ZeroMemory(payload);
            foreach (byte[]? material in ownedMaterials)
                if (material is not null) CryptographicOperations.ZeroMemory(material);
        }
    }
}

/// <summary>Owns restricted encrypted output; each deliberate export receives an independent clearable copy.</summary>
public sealed class ProfileSealedSnapshot : IDisposable
{
    private readonly object _access = new();
    private byte[]? _container;

    internal ProfileSealedSnapshot(ProfileRecoveryReference reference, byte[] container)
    {
        Reference = reference;
        _container = container;
    }

    public ProfileRecoveryReference Reference { get; }

    public byte[] CopyContainer()
    {
        lock (_access)
        {
            byte[] source = Volatile.Read(ref _container) ??
                throw new ObjectDisposedException(nameof(ProfileSealedSnapshot));
            byte[] copy = source.ToArray();
            if (Volatile.Read(ref _container) is null)
            {
                CryptographicOperations.ZeroMemory(copy);
                throw new ObjectDisposedException(nameof(ProfileSealedSnapshot));
            }
            return copy;
        }
    }

    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _container, null);
        if (bytes is not null)
            lock (_access) CryptographicOperations.ZeroMemory(bytes);
    }

    public override string ToString() => nameof(ProfileSealedSnapshot);
}
