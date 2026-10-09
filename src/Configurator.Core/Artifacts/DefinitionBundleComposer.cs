namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

public sealed class DefinitionBundleResult : IDisposable
{
    internal DefinitionBundleResult(IReadOnlyList<DefinitionArtifact> artifacts, IReadOnlyList<DefinitionDiagnostic> diagnostics)
    {
        Artifacts = Array.AsReadOnly(artifacts.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<DefinitionArtifact> Artifacts { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public bool Succeeded => Diagnostics.Count == 0;
    public void Dispose()
    {
        foreach (DefinitionArtifact artifact in Artifacts) artifact.Dispose();
        GC.SuppressFinalize(this);
    }
    public override string ToString() => $"{nameof(DefinitionBundleResult)}:Succeeded={Succeeded}:Count={Artifacts.Count}";
}

public static class DefinitionBundleComposer
{
    public static DefinitionBundleResult Compose(SetupDefinition definition, IEnumerable<string> artifactIds,
        SetupAnswerSnapshot answers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactIds);
        ArgumentNullException.ThrowIfNull(answers);

        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in artifactIds)
        {
            if (id is null || !selected.Add(id))
                return new([], [new("artifact-duplicate", "")]);
            if (selected.Count > definition.Artifacts.Count)
                return new([], [new("artifact-limit", "")]);
        }
        if (selected.Count == 0) return new([], [new("artifact-empty", "")]);
        if (selected.Any(id => definition.Artifacts.All(item => item.Id != id)))
            return new([], [new("artifact-unknown", "")]);

        var composed = new List<DefinitionArtifact>(selected.Count);
        long totalBytes = 0;
        bool complete = false;
        try
        {
            foreach (SetupArtifactDefinition artifact in definition.Artifacts)
            {
                if (!selected.Contains(artifact.Id)) continue;
                DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifact.Id, answers);
                if (!result.Succeeded) return new([], result.Diagnostics);
                DefinitionArtifact output = result.Artifact!;
                composed.Add(output);
                totalBytes += output.ByteLength;
                if (totalBytes > SetupCompositionLimits.DefaultAggregateSourceBytes)
                    return new([], [new("bundle-limit", "")]);
            }
            complete = true;
            return new(composed, []);
        }
        finally
        {
            if (!complete)
                foreach (DefinitionArtifact artifact in composed) artifact.Dispose();
        }
    }
}
