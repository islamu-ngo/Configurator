[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Configurator.Browser.Tests")]

namespace Configurator.Browser.Services;

using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>Admits bundled data without remote discovery or host authority.</summary>
public sealed class BrowserDefinitionRegistry
{
    public const int MaximumTotalCanonicalBytes = 32 * 1024 * 1024;
    public const int MaximumHistoryEntries = 4096;

    private readonly Dictionary<(Guid Family, long Revision), ArtifactDigest> _history = [];
    private readonly int _maximumTotalCanonicalBytes;

    public BrowserDefinitionRegistry(IEnumerable<Definition> definitions)
        : this(definitions, MaximumTotalCanonicalBytes)
    {
    }

    internal BrowserDefinitionRegistry(IEnumerable<Definition> definitions, int maximumTotalCanonicalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumTotalCanonicalBytes, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumTotalCanonicalBytes, MaximumTotalCanonicalBytes);
        _maximumTotalCanonicalBytes = maximumTotalCanonicalBytes;

        var initial = new DefinitionRegistry(definitions);
        Current = new DefinitionRegistry([]);
        ValidateAndCommit(initial, initial.Definitions);
    }

    public DefinitionRegistry Current { get; private set; }

    public void Admit(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var candidate = new DefinitionRegistry(Current.Definitions.Append(definition).DistinctBy(item => item.Digest));
        ValidateAndCommit(candidate, [definition]);
    }

    public void Replace(IEnumerable<Definition> definitions)
    {
        var replacement = new DefinitionRegistry(definitions);
        // A refreshed registry cannot relabel a revision that an active session may have pinned.
        ValidateAndCommit(replacement, replacement.Definitions);
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
        if (_history.Count == MaximumHistoryEntries) throw DefinitionException.RegistryLimit();
        _history.Add(key, digest);
    }

    private void ValidateAndCommit(DefinitionRegistry candidate, IEnumerable<Definition> historyCandidates)
    {
        long totalCanonicalBytes = 0;
        foreach (Definition active in candidate.Definitions)
        {
            totalCanonicalBytes += active.CanonicalBytes.Length;
            if (totalCanonicalBytes > _maximumTotalCanonicalBytes)
                throw DefinitionException.RegistryLimit();
        }

        Definition[] items = historyCandidates.ToArray();
        var additions = new HashSet<(Guid, long)>();
        foreach (Definition definition in items)
        {
            var key = (definition.FamilyId, definition.Revision);
            if (_history.TryGetValue(key, out ArtifactDigest prior))
            {
                if (prior != definition.Digest) throw DefinitionException.RevisionConflict();
            }
            else
            {
                additions.Add(key);
            }
        }

        if (_history.Count + additions.Count > MaximumHistoryEntries)
            throw DefinitionException.RegistryLimit();

        foreach (Definition definition in items)
            _history.TryAdd((definition.FamilyId, definition.Revision), definition.Digest);

        Current = candidate;
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
