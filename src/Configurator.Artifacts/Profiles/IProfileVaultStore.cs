namespace Configurator.Artifacts.Profiles;

public interface IProfileVaultStore
{
    ValueTask<IReadOnlyList<ProfileRevisionDescriptor>> ListAsync(CancellationToken cancellationToken = default);
    ValueTask<byte[]> ReadAsync(ProfileRevisionDescriptor revision, CancellationToken cancellationToken = default);
    Task<ProtectedArtifactBundleResult> SaveAsync(Guid profileId, Guid snapshotId, ProfileFileKind kind,
        ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}

public enum ProfileFileKind { Encrypted, Comparison }

/// <summary>Value-free storage refusal; native paths and original exception details are not retained.</summary>
public sealed class ProfileVaultStoreException(ProtectedArtifactStatus status) : Exception(status.ToString())
{
    public ProtectedArtifactStatus Status { get; } = status;
    public override string ToString() => $"{nameof(ProfileVaultStoreException)}:{Status}";
}
