namespace Configurator.Artifacts.Profiles;

using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Profiles;

/// <summary>Checks an exact stored copy with one portable credential in a fresh context with no OS keyring dependency.</summary>
public static class ProfileRecoveryVerifier
{
    public static async ValueTask<ProfileRecoveryResult> VerifyAsync(IProfileVaultStore store,
        ProfileRevisionDescriptor revision, ProfileRecoveryReference trustedReference, int recipientIndex,
        ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(trustedReference);
        ProfileRecoveryResult Refuse(ProfileRecoveryFailure failure) =>
            new(trustedReference.State, failure, trustedReference, recipientIndex);
        cancellationToken.ThrowIfCancellationRequested();
        if (revision.Kind != ProfileFileKind.Encrypted || revision.ProfileId != trustedReference.ProfileId ||
            revision.SnapshotId != trustedReference.SnapshotId)
            return Refuse(ProfileRecoveryFailure.IdentityMismatch);
        if (!trustedReference.PortableRecoveryConfigured ||
            recipientIndex < (trustedReference.DeviceRecipientId is null ? 0 : 1))
            return Refuse(ProfileRecoveryFailure.NotPortableRecipient);
        if (passphraseUtf8.IsEmpty) return Refuse(ProfileRecoveryFailure.MissingPassphrase);
        if (passphraseUtf8.Length > ProfileContainerPolicy.MaximumPassphraseUtf8Bytes)
            return Refuse(ProfileRecoveryFailure.InvalidPassphrase);
        byte[] material = passphraseUtf8.ToArray();
        byte[]? container = null;
        try
        {
            container = await store.ReadAsync(revision, cancellationToken).ConfigureAwait(false);
            if (ArtifactDigest.Compute(container) != trustedReference.Digest)
                return Refuse(ProfileRecoveryFailure.CiphertextMismatch);
            int count = ProfileContainerPolicy.Admit(container);
            if (recipientIndex >= count) return Refuse(ProfileRecoveryFailure.MissingRecipient);
            using var codec = new NativeProfileContainerCodec();
            using UnlockedProfile profile = await codec.OpenAsync(container, recipientIndex,
                material, cancellationToken).ConfigureAwait(false);
            if (profile.ProfileId != trustedReference.ProfileId || profile.SnapshotId != trustedReference.SnapshotId)
                return Refuse(ProfileRecoveryFailure.IdentityMismatch);
            cancellationToken.ThrowIfCancellationRequested();
            return new(ProfileRecoveryState.VerifiedForSnapshot, ProfileRecoveryFailure.None, trustedReference, recipientIndex);
        }
        catch (ProfileVaultStoreException)
        {
            return Refuse(ProfileRecoveryFailure.StorageUnavailable);
        }
        catch (ProfileException exception)
        {
            return Refuse(exception.Code switch
            {
                ProfileFailureCode.AuthenticationFailed => ProfileRecoveryFailure.AuthenticationFailed,
                ProfileFailureCode.InvalidPassphrase => ProfileRecoveryFailure.InvalidPassphrase,
                ProfileFailureCode.InvalidPayload => ProfileRecoveryFailure.InvalidPayload,
                ProfileFailureCode.LimitExceeded => ProfileRecoveryFailure.LimitExceeded,
                ProfileFailureCode.Busy => ProfileRecoveryFailure.Busy,
                _ => ProfileRecoveryFailure.InvalidContainer
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
            if (container is not null) CryptographicOperations.ZeroMemory(container);
        }
    }
}
