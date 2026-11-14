namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;

/// <summary>
/// Native custody owned by one form. Only the form's existing mutation gate may access it.
/// Permanent exclusions are metadata; undo never restores their retired authority.
/// </summary>
internal sealed class ProfileWorkspaceSession : IDisposable
{
    private UnlockedProfile _profile;
    private readonly Definition _target;
    private readonly ProfileUpgradePlan _upgrade;
    private readonly HashSet<DefinitionPath> _excluded = [];
    private readonly string[] _protectedRoots;

    internal ProfileWorkspaceSession(UnlockedProfile profile, Definition target)
    {
        _upgrade = ProfileUpgradePlanner.Plan(profile, target);
        _profile = profile;
        _target = target;
        _protectedRoots = profile.Mode == ProfileProtectionMode.ImportUpgradeOnly
            ? _upgrade.Fields.Where(field => field.Eligibility is not
                    (ProfileCarryEligibility.Missing or ProfileCarryEligibility.Removed))
                .Where(field => DefinitionPath.Parse(field.FieldPath).Segments is { Count: 1 } segments &&
                    target.Configuration.Properties.TryGetValue(segments[0], out DefinitionNode? schema) &&
                    (DefinitionSecretPolicy.DeclaresProtected(target.Configuration) || DefinitionSecretPolicy.IsProtected(schema)))
                .Select(field => field.FieldPath).ToArray()
            : [];
    }

    internal ProfileComparisonProjection Projection => new(_profile.ProfileId, _profile.SnapshotId,
        _profile.Mode, _profile.DefinitionDigest, _upgrade,
        Array.AsReadOnly(_protectedRoots.Select(path => new BlindSecretSlot(path,
            _upgrade.Fields.Single(delta => delta.FieldPath == path).Eligibility,
            !_excluded.Contains(DefinitionPath.Parse(path)))).ToArray()));

    internal bool Blocks(DefinitionPath path) => _protectedRoots.Any(root =>
        !_excluded.Contains(DefinitionPath.Parse(root)) &&
        (path.Segments.Count == 0 || path.Segments[0] == DefinitionPath.Parse(root).Segments[0]));

    internal bool Retire(DefinitionPath path)
    {
        DefinitionPath[] retiring = _protectedRoots.Select(DefinitionPath.Parse)
            .Where(root => !_excluded.Contains(root) &&
                (path.Segments.Count == 0 || path.Segments[0] == root.Segments[0])).ToArray();
        if (retiring.Length == 0) return false;
        _profile = ProfilePayloadCodec.RetireRetainedFields(_profile, retiring);
        _excluded.UnionWith(retiring);
        return true;
    }

    internal IReadOnlyDictionary<string, JsonElement> OpeningAnswers()
    {
        if (_profile.Mode == ProfileProtectionMode.InspectableEncrypted)
            return ProfilePayloadCodec.InspectAnswers(_profile).Values;
        JsonNode? publicValues = JsonNode.Parse(CompareBaselineCodec.Create(_profile).PublicValues.GetRawText());
        if (DefinitionSecretPolicy.RemoveProtected(_target.Configuration, publicValues) || publicValues is not JsonObject mapping)
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        return mapping.ToDictionary(property => property.Key,
            property => JsonSerializer.SerializeToElement(property.Value), StringComparer.Ordinal);
    }

    internal ProfileCompositionInput Capture(AnswerSnapshot current) => new(_profile, _target, current, _excluded);

    public void Dispose() => _profile.Dispose();
}
