namespace Configurator.Core.Composition;

using Configurator.Core;

public enum CompositionScaleProfileId
{
    Small,
    Medium,
    Large,
    Ceiling
}

public enum CompositionScaleAdmissionCode
{
    Accepted,
    UnknownProfile,
    ProfileDisabled,
    EvidenceMismatch,
    TargetIncompatible
}

public enum CompositionScaleOutcome
{
    Succeeded,
    Rejected,
    Cancelled
}

public sealed class CompositionScaleProfile
{
    public CompositionScaleProfile(
        CompositionScaleProfileId id,
        string name,
        CompositionSourceKind sourceKind,
        string evidenceDigest,
        int serializedArtifactBytes)
    {
        if (!Enum.IsDefined(id) || !Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serializedArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            serializedArtifactBytes, CompositionLimits.DefaultAggregateSourceBytes);
        Id = id;
        Name = name;
        SourceKind = sourceKind;
        EvidenceDigest = ArtifactDigest.Parse(evidenceDigest);
        SerializedArtifactBytes = serializedArtifactBytes;
    }

    public CompositionScaleProfileId Id { get; }
    public string Name { get; }
    public CompositionSourceKind SourceKind { get; }
    public ArtifactDigest EvidenceDigest { get; }
    public int SerializedArtifactBytes { get; }
    public CompositionLimits EffectiveLimits => CompositionLimits.Default;

    public override string ToString() =>
        $"{nameof(CompositionScaleProfile)}:{Name}:{SourceKind}";
}

public sealed class CompositionScaleAdmission
{
    private CompositionScaleAdmission(
        CompositionScaleAdmissionCode code,
        CompositionScaleProfile? profile)
    {
        Code = code;
        Profile = profile;
    }

    public bool Succeeded =>
        Code == CompositionScaleAdmissionCode.Accepted && Profile is not null;
    public CompositionScaleAdmissionCode Code { get; }
    public CompositionScaleProfile? Profile { get; }

    internal static CompositionScaleAdmission Accepted(
        CompositionScaleProfile profile) =>
        new(CompositionScaleAdmissionCode.Accepted, profile);

    internal static CompositionScaleAdmission Rejected(
        CompositionScaleAdmissionCode code) =>
        new(code, null);

    public override string ToString() =>
        $"{nameof(CompositionScaleAdmission)}:{Code}:Succeeded={Succeeded}";
}

public sealed record CompositionScaleTelemetry(
    CompositionSourceKind SourceKind,
    CompositionScaleProfileId Profile,
    CompositionScaleOutcome Outcome,
    int AggregateBytes,
    int Nodes,
    int Files,
    long DurationMicroseconds);

public sealed class CompositionScaleProfiles
{
    public const string DisabledExpandedProfileName = "expanded";

    private readonly CompositionScaleProfile[] _profiles;

    public CompositionScaleProfiles(IEnumerable<CompositionScaleProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles.Take(5).ToArray();
        if (_profiles.Length > 4 || _profiles.Any(profile => profile is null)
            || _profiles.Select(profile => profile.Id).Distinct().Count() != _profiles.Length
            || _profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != _profiles.Length)
            throw new ArgumentException("Scale profiles must have unique identities.", nameof(profiles));
    }

    public IReadOnlyList<CompositionScaleProfile> All => Array.AsReadOnly(_profiles);

    public CompositionScaleAdmission Admit(
        string? profileName,
        ArtifactDigest evidenceDigest,
        int targetMaximumArtifactBytes)
    {
        if (string.Equals(
                profileName, DisabledExpandedProfileName, StringComparison.Ordinal))
            return CompositionScaleAdmission.Rejected(
                CompositionScaleAdmissionCode.ProfileDisabled);

        CompositionScaleProfile? profile = _profiles.FirstOrDefault(item =>
            string.Equals(item.Name, profileName, StringComparison.Ordinal));
        if (profile is null)
            return CompositionScaleAdmission.Rejected(
                CompositionScaleAdmissionCode.UnknownProfile);
        if (profile.EvidenceDigest != evidenceDigest)
            return CompositionScaleAdmission.Rejected(
                CompositionScaleAdmissionCode.EvidenceMismatch);
        if (targetMaximumArtifactBytes < profile.SerializedArtifactBytes)
            return CompositionScaleAdmission.Rejected(
                CompositionScaleAdmissionCode.TargetIncompatible);
        return CompositionScaleAdmission.Accepted(profile);
    }
}
