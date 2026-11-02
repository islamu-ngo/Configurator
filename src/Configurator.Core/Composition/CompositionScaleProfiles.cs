namespace Configurator.Core.Composition;

using Configurator.Core;

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
