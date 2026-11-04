namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using System.Text.Json.Serialization;

/// <summary>Public scope facts for one immutable answer snapshot, never a caller-supplied validation authority.</summary>
public sealed class DefinitionConfigurationContext
{
    private readonly FrozenSet<DefinitionPath> _locations;
    internal DefinitionConfigurationContext(string? recipeId, string? scopeId, bool ready,
        IEnumerable<string> artifactIds, IEnumerable<DefinitionPath> locations, string? primaryArtifactId = null,
        bool hasUnresolvedDependencies = false)
    {
        RecipeId = recipeId;
        ScopeId = scopeId;
        IsReady = ready;
        EligibleArtifactIds = Array.AsReadOnly(artifactIds.ToArray());
        _locations = locations.ToFrozenSet();
        RelevantLocations = Array.AsReadOnly(_locations.OrderBy(path => path.ToPointer(), StringComparer.Ordinal).ToArray());
        PrimaryArtifactId = primaryArtifactId;
        HasUnresolvedDependencies = hasUnresolvedDependencies;
    }
    public string? RecipeId { get; }
    public string? ScopeId { get; }
    public bool IsReady { get; }
    public string? PrimaryArtifactId { get; }
    public IReadOnlyList<string> EligibleArtifactIds { get; }
    [JsonIgnore]
    public IReadOnlyList<DefinitionPath> RelevantLocations { get; }
    internal bool HasUnresolvedDependencies { get; }
    internal bool Includes(IEnumerable<string> segments) => _locations.Contains(DefinitionPath.FromSegments(segments));
}
