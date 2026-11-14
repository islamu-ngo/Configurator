namespace Configurator.Core.Profiles;

using System.Security.Cryptography;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

/// <summary>Consumes retained values only inside a disposable scope and publishes restricted output ownership.</summary>
public static class ProfileArtifactComposer
{
    public static ProfileArtifactResult Compose(ProfileCompositionInput input, string artifactId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(artifactId);
        using ProfileCompositionInput.Evaluation evaluation = input.Evaluate(cancellationToken);
        var composition = new DefinitionArtifactComposer.ValidatedComposition(input.Target, evaluation.Answers, cancellationToken);
        DefinitionArtifactResult result = composition.Compose(artifactId, []);
        ProfileArtifact? output = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            input.EnsureAlive();
            if (result.Artifact is null) return new(null, result.Diagnostics);
            output = new ProfileArtifact(input, result.Artifact);
            input.Register(output, cancellationToken);
            var outcome = new ProfileArtifactResult(output, []);
            output = null;
            return outcome;
        }
        catch
        {
            if (output is null) result.Artifact?.Dispose();
            throw;
        }
        finally { output?.Dispose(); }
    }

    /// <summary>
    /// Seals the validated upgraded answers and target schema without exposing plaintext. Mode, profile identity,
    /// source locator provenance, environment context and comparison key are preserved. The host supplies a fresh
    /// snapshot identity and retains historical admission and recipient authority.
    /// </summary>
    public static async ValueTask<ProfileSealedSnapshot> SealAsync(ProfileCompositionInput input,
        IProfileContainerCodec codec, Guid newSnapshotId, IReadOnlyList<ReadOnlyMemory<byte>> portableRecipientMaterials,
        Guid? deviceBoundAcknowledgement = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(portableRecipientMaterials);
        cancellationToken.ThrowIfCancellationRequested();
        input.EnsureAlive();
        if (newSnapshotId == Guid.Empty || newSnapshotId == input.SourceSnapshotId)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.InvalidSnapshotIdentity);
        Guid? deviceId = codec.DeviceRecipientId;
        int portableCount = portableRecipientMaterials.Count;
        if (deviceId == Guid.Empty || portableCount < 0 ||
            portableCount > ProfileContainerPolicy.MaximumRecipients - (deviceId is null ? 0 : 1) ||
            portableCount == 0 && deviceId is null)
            throw new ProfileException(ProfileFailureCode.LimitExceeded);
        if (portableCount == 0 && deviceBoundAcknowledgement != input.ProfileId)
            throw new ProfileRecoveryException(ProfileRecoveryFailure.DeviceBoundAcknowledgementRequired);

        byte[]? payload = null;
        byte[]? encrypted = null;
        ProfileSealedSnapshot? output = null;
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
            payload = input.CreateSealingPayload(newSnapshotId, cancellationToken);
            input.EnsureAlive();
            encrypted = await codec.SealAsync(payload, materials, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            input.EnsureAlive();
            var reference = new ProfileRecoveryReference(input.ProfileId, newSnapshotId,
                ArtifactDigest.Compute(encrypted), deviceId, portableCount != 0);
            output = new ProfileSealedSnapshot(reference, encrypted);
            encrypted = null;
            input.Register(output, cancellationToken);
            ProfileSealedSnapshot sealedProfile = output;
            output = null;
            return sealedProfile;
        }
        finally
        {
            output?.Dispose();
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
            foreach (byte[]? material in ownedMaterials)
                if (material is not null) CryptographicOperations.ZeroMemory(material);
        }
    }
}

/// <summary>Value-free outcome. The caller owns the disposable restricted artifact, if preparation succeeded.</summary>
public sealed class ProfileArtifactResult
{
    internal ProfileArtifactResult(ProfileArtifact? artifact, IEnumerable<DefinitionDiagnostic> diagnostics)
    {
        Artifact = artifact;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public ProfileArtifact? Artifact { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public bool Succeeded => Artifact is not null && Diagnostics.Count == 0;
    public override string ToString() => $"{nameof(ProfileArtifactResult)}:Succeeded={Succeeded}";
}

/// <summary>
/// Restricted, one-shot output. There is no preview, clipboard or ordinary artifact conversion.
/// Each explicit sink callback is a deliberate disclosure boundary: it must not retain the supplied memory.
/// The owned callback copy clears in finally. Disposal retires new use and clears retained bytes, but cannot
/// undo a sink already entered or guarantee erasure of managed, interop or external copies.
/// </summary>
public sealed class ProfileArtifact : IDisposable
{
    private readonly ProfileCompositionInput _input;
    private DefinitionArtifact? _artifact;
    private int _started;

    internal ProfileArtifact(ProfileCompositionInput input, DefinitionArtifact artifact)
    {
        _input = input;
        _artifact = artifact;
        ArtifactId = artifact.ArtifactId;
        FileName = artifact.FileName;
        MediaType = artifact.MediaType;
        ByteLength = artifact.ByteLength;
        DefinitionDigest = artifact.DefinitionDigest;
        DefinitionRevision = artifact.DefinitionRevision;
        InputRevision = artifact.InputRevision;
        ApplySemantics = artifact.ApplySemantics;
        Restart = artifact.Restart;
    }

    public string ArtifactId { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public int ByteLength { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public long DefinitionRevision { get; }
    public long InputRevision { get; }
    public DefinitionTargetUpdateMode ApplySemantics { get; }
    public DefinitionRestartRequirement Restart { get; }
    public ArtifactSensitivity Sensitivity => ArtifactSensitivity.Restricted;

    /// <summary>Transfers only to the caller's deliberate protected native save operation.</summary>
    public ValueTask WriteToNativeSaveAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> save,
        CancellationToken cancellationToken = default) => WriteAsync(save, cancellationToken);

    /// <summary>Transfers only for the Browser worker's deliberate download, without native protection claims.</summary>
    public ValueTask WriteToBrowserDownloadAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> download,
        CancellationToken cancellationToken = default) => WriteAsync(download, cancellationToken);

    private async ValueTask WriteAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ObjectDisposedException.ThrowIf(Interlocked.Exchange(ref _started, 1) != 0, this);
        byte[]? copy = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _input.EnsureAlive();
            DefinitionArtifact artifact = Volatile.Read(ref _artifact) ?? throw new ObjectDisposedException(nameof(ProfileArtifact));
            copy = artifact.CopyBytes();
            cancellationToken.ThrowIfCancellationRequested();
            _input.EnsureAlive();
            await sink(copy, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (copy is not null) CryptographicOperations.ZeroMemory(copy);
            Dispose();
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _artifact, null)?.Dispose();
        _input.Release(this);
    }

    public override string ToString() => "ProfileArtifact:Restricted";
}
