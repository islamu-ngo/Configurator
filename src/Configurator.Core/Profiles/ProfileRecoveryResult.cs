namespace Configurator.Core.Profiles;

public enum ProfileRecoveryState { DeviceBoundOnly, ConfiguredUnverified, VerifiedForSnapshot }

public enum ProfileRecoveryFailure
{
    None,
    DeviceBoundAcknowledgementRequired,
    InvalidSnapshotIdentity,
    IdentityMismatch,
    CiphertextMismatch,
    NotPortableRecipient,
    MissingRecipient,
    MissingPassphrase,
    InvalidPassphrase,
    AuthenticationFailed,
    InvalidContainer,
    InvalidPayload,
    LimitExceeded,
    Busy,
    StorageUnavailable
}

/// <summary>Value-free lifecycle refusal; no private payload or native exception is retained.</summary>
public sealed class ProfileRecoveryException(ProfileRecoveryFailure failure) : Exception(failure.ToString())
{
    public ProfileRecoveryFailure Failure { get; } = failure;
    public override string ToString() => $"{nameof(ProfileRecoveryException)}:{Failure}";
}

/// <summary>
/// Caller-trusted reference retained independently of the candidate file, normally when sealing.
/// Computing a new digest from an untrusted candidate does not establish this trust.
/// Routing/configuration facts describe intended recipients, never proof that they work.
/// </summary>
public sealed record ProfileRecoveryReference
{
    public ProfileRecoveryReference(Guid profileId, Guid snapshotId, ArtifactDigest digest,
        Guid? deviceRecipientId, bool portableRecoveryConfigured)
    {
        if (profileId == Guid.Empty || snapshotId == Guid.Empty ||
            !ArtifactDigest.TryParse(digest.Value, out _) || deviceRecipientId == Guid.Empty ||
            (deviceRecipientId is null && !portableRecoveryConfigured))
            throw new ProfileRecoveryException(ProfileRecoveryFailure.InvalidSnapshotIdentity);
        ProfileId = profileId;
        SnapshotId = snapshotId;
        Digest = digest;
        DeviceRecipientId = deviceRecipientId;
        PortableRecoveryConfigured = portableRecoveryConfigured;
    }

    public Guid ProfileId { get; }
    public Guid SnapshotId { get; }
    public ArtifactDigest Digest { get; }
    public Guid? DeviceRecipientId { get; }
    public bool PortableRecoveryConfigured { get; }
    public ProfileRecoveryState State => PortableRecoveryConfigured
        ? ProfileRecoveryState.ConfiguredUnverified : ProfileRecoveryState.DeviceBoundOnly;
}

/// <summary>
/// Facts about one attempt against one trusted reference. Verification covers only the selected
/// portable recipient and exact ciphertext; it grants no workspace, backup-retention or target authority.
/// </summary>
public sealed record ProfileRecoveryResult(ProfileRecoveryState State, ProfileRecoveryFailure Failure,
    ProfileRecoveryReference Reference, int RecipientIndex);
