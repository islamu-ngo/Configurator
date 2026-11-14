namespace Configurator.Browser.Services;

using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>Admits bundled data without remote discovery or host authority.</summary>
public sealed class BrowserDefinitionRegistry
{
    private readonly Dictionary<(Guid Family, long Revision), ArtifactDigest> _history = [];

    public BrowserDefinitionRegistry(IEnumerable<Definition> definitions)
    {
        Current = new DefinitionRegistry(definitions);
        AdmitHistory(Current.Definitions);
    }

    public DefinitionRegistry Current { get; private set; }

    public void Admit(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var current = new DefinitionRegistry(Current.Definitions.Append(definition).DistinctBy(item => item.Digest));
        AdmitHistory([definition]);
        Current = current;
    }

    public void Replace(IEnumerable<Definition> definitions)
    {
        var replacement = new DefinitionRegistry(definitions);
        // A refreshed registry cannot relabel a revision that an active session may have pinned.
        AdmitHistory(replacement.Definitions);
        Current = replacement;
    }

    /// <summary>Shares the catalogue's bounded, removal-resistant revision history with authenticated profile candidates.</summary>
    public void AdmitHistoricalIdentity(Guid familyId, long revision, ArtifactDigest digest)
    {
        if (familyId == Guid.Empty || revision < 1 || !ArtifactDigest.TryParse(digest.Value, out _))
            throw new ArgumentException("definition-identity-invalid");
        var key = (familyId, revision);
        if (_history.TryGetValue(key, out ArtifactDigest prior))
        {
            if (prior != digest) throw DefinitionException.RevisionConflict();
            return;
        }
        if (_history.Count == 4096) throw DefinitionException.RegistryLimit();
        _history.Add(key, digest);
    }

    private void AdmitHistory(IEnumerable<Definition> definitions)
    {
        Definition[] candidates = definitions.ToArray();
        var additions = new HashSet<(Guid, long)>();
        foreach (Definition definition in candidates)
        {
            var key = (definition.FamilyId, definition.Revision);
            if (_history.TryGetValue(key, out ArtifactDigest prior))
            {
                if (prior != definition.Digest) throw DefinitionException.RevisionConflict();
            }
            else additions.Add(key);
        }
        if (_history.Count + additions.Count > 4096) throw DefinitionException.RegistryLimit();
        foreach (Definition definition in candidates)
            _history.TryAdd((definition.FamilyId, definition.Revision), definition.Digest);
    }

    public static BrowserDefinitionRegistry LoadBundled()
    {
        var assembly = typeof(BrowserDefinitionRegistry).Assembly;
        var definitions = new List<Definition>();
        foreach (string name in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Configurator.Browser.Definitions.", StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using Stream input = assembly.GetManifestResourceStream(name)!;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            definitions.Add(DefinitionCodec.ParseJson(bytes.ToArray()));
        }
        return new BrowserDefinitionRegistry(definitions);
    }
}
