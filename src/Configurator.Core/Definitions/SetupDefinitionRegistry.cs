namespace Configurator.Core.Definitions;

public sealed class SetupDefinitionRegistry
{
    private readonly SetupDefinition[] _definitions;

    public SetupDefinitionRegistry(IEnumerable<SetupDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = definitions.Take(4097).ToArray();
        if (_definitions.Length > 4096 || _definitions.Any(item => item is null))
            throw new SetupDefinitionException("registry-limit");
        foreach (var group in _definitions.GroupBy(item => (item.FamilyId, item.Revision)))
            if (group.Select(item => item.Digest).Distinct().Count() > 1)
                throw new SetupDefinitionException("revision-conflict");
        _definitions = _definitions.DistinctBy(item => item.Digest).ToArray();
    }

    public IReadOnlyList<SetupDefinition> Definitions => Array.AsReadOnly(_definitions);

    public SetupDefinition Resolve(Guid familyId, string? appVersion = null, ArtifactDigest? digest = null)
    {
        SetupDefinition[] family = _definitions.Where(item => item.FamilyId == familyId).ToArray();
        if (family.Length == 0) throw new SetupDefinitionException("family-unknown");
        SetupDefinition[] compatible = family.Where(item =>
            appVersion is null || item.ApplicationVersions.Contains(appVersion, StringComparer.Ordinal)).ToArray();
        if (compatible.Length == 0) throw new SetupDefinitionException("version-incompatible");
        if (appVersion is null && digest is null && compatible.Length != 1)
            throw new SetupDefinitionException("selection-ambiguous");
        SetupDefinition? selected = compatible.Where(item => digest is null || item.Digest == digest)
            .OrderByDescending(item => item.Revision).FirstOrDefault();
        return selected ?? throw new SetupDefinitionException("digest-mismatch");
    }
}
