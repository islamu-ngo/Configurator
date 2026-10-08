namespace Configurator.Core.Composition;

using Configurator.Core;

public enum SetupCompositionScaleProfileId
{
    Small,
    Medium,
    Large,
    Ceiling
}

public enum SetupCompositionScaleAdmissionCode
{
    Accepted,
    UnknownProfile,
    ProfileDisabled,
    EvidenceMismatch,
    TargetIncompatible
}

public enum SetupCompositionScaleOutcome
{
    Succeeded,
    Rejected,
    Cancelled
}

public sealed class SetupCompositionScaleProfile
{
    public SetupCompositionScaleProfile(
        SetupCompositionScaleProfileId id,
        string name,
        SetupCompositionSourceKind sourceKind,
        string evidenceDigest,
        int serializedArtifactBytes)
    {
        if (!Enum.IsDefined(id) || !Enum.IsDefined(sourceKind))
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(serializedArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            serializedArtifactBytes, SetupCompositionLimits.DefaultAggregateSourceBytes);
        Id = id;
        Name = name;
        SourceKind = sourceKind;
        EvidenceDigest = ArtifactDigest.Parse(evidenceDigest);
        SerializedArtifactBytes = serializedArtifactBytes;
    }

    public SetupCompositionScaleProfileId Id { get; }
    public string Name { get; }
    public SetupCompositionSourceKind SourceKind { get; }
    public ArtifactDigest EvidenceDigest { get; }
    public int SerializedArtifactBytes { get; }
    public SetupCompositionLimits EffectiveLimits => SetupCompositionLimits.Default;

    public override string ToString() =>
        $"{nameof(SetupCompositionScaleProfile)}:{Name}:{SourceKind}";
}

public sealed class SetupCompositionScaleAdmission
{
    private SetupCompositionScaleAdmission(
        SetupCompositionScaleAdmissionCode code,
        SetupCompositionScaleProfile? profile)
    {
        Code = code;
        Profile = profile;
    }

    public bool Succeeded =>
        Code == SetupCompositionScaleAdmissionCode.Accepted && Profile is not null;
    public SetupCompositionScaleAdmissionCode Code { get; }
    public SetupCompositionScaleProfile? Profile { get; }

    internal static SetupCompositionScaleAdmission Accepted(
        SetupCompositionScaleProfile profile) =>
        new(SetupCompositionScaleAdmissionCode.Accepted, profile);

    internal static SetupCompositionScaleAdmission Rejected(
        SetupCompositionScaleAdmissionCode code) =>
        new(code, null);

    public override string ToString() =>
        $"{nameof(SetupCompositionScaleAdmission)}:{Code}:Succeeded={Succeeded}";
}

public sealed record SetupCompositionScaleTelemetry(
    SetupCompositionSourceKind SourceKind,
    SetupCompositionScaleProfileId Profile,
    SetupCompositionScaleOutcome Outcome,
    int AggregateBytes,
    int Nodes,
    int Files,
    long DurationMicroseconds);

public sealed class SetupCompositionScaleProfiles
{
    public const string DisabledExpandedProfileName = "expanded";

    private readonly SetupCompositionScaleProfile[] _profiles;

    public SetupCompositionScaleProfiles(IEnumerable<SetupCompositionScaleProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles.Take(5).ToArray();
        if (_profiles.Length > 4 || _profiles.Any(profile => profile is null)
            || _profiles.Select(profile => profile.Id).Distinct().Count() != _profiles.Length
            || _profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != _profiles.Length)
            throw new ArgumentException("Scale profiles must have unique identities.", nameof(profiles));
    }

    public IReadOnlyList<SetupCompositionScaleProfile> All => Array.AsReadOnly(_profiles);

    public SetupCompositionScaleAdmission Admit(
        string? profileName,
        ArtifactDigest evidenceDigest,
        int targetMaximumArtifactBytes)
    {
        if (string.Equals(
                profileName, DisabledExpandedProfileName, StringComparison.Ordinal))
            return SetupCompositionScaleAdmission.Rejected(
                SetupCompositionScaleAdmissionCode.ProfileDisabled);

        SetupCompositionScaleProfile? profile = _profiles.FirstOrDefault(item =>
            string.Equals(item.Name, profileName, StringComparison.Ordinal));
        if (profile is null)
            return SetupCompositionScaleAdmission.Rejected(
                SetupCompositionScaleAdmissionCode.UnknownProfile);
        if (profile.EvidenceDigest != evidenceDigest)
            return SetupCompositionScaleAdmission.Rejected(
                SetupCompositionScaleAdmissionCode.EvidenceMismatch);
        if (targetMaximumArtifactBytes < profile.SerializedArtifactBytes)
            return SetupCompositionScaleAdmission.Rejected(
                SetupCompositionScaleAdmissionCode.TargetIncompatible);
        return SetupCompositionScaleAdmission.Accepted(profile);
    }
}
