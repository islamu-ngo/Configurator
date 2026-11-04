namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

/// <summary>Prepares explicitly incomplete output while complete composition remains strict.</summary>
public static class DefinitionDraftArtifactComposer
{
    public static DefinitionDraftArtifactResult Compose(Definition definition, string artifactId,
        AnswerSnapshot answers, CancellationToken cancellationToken = default)
    {
        var completions = new List<DefinitionDraftCompletion>();
        DefinitionArtifactResult result = DefinitionArtifactComposer.ComposeCore(definition, artifactId, answers, true, completions, cancellationToken);
        return new(result.Artifact, result.Diagnostics, result.Succeeded ? completions : []);
    }

    public static DefinitionDraftBundleResult ComposeBundle(Definition definition,
        IEnumerable<string> artifactIds, AnswerSnapshot answers, CancellationToken cancellationToken = default)
        => ComposeBundleCore(definition, artifactIds, answers, false, cancellationToken);

    /// <summary>Prepares only destinations eligible for the current admitted context.</summary>
    public static DefinitionDraftBundleResult ComposeActiveBundle(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ComposeBundleCore(definition, definition.Artifacts.Select(artifact => artifact.Id), answers, true, cancellationToken);
    }

    private static DefinitionDraftBundleResult ComposeBundleCore(Definition definition,
        IEnumerable<string> artifactIds, AnswerSnapshot answers, bool activeOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactIds);
        ArgumentNullException.ThrowIfNull(answers);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in artifactIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (id is null || !selected.Add(id)) return new([], [new("artifact-duplicate", "")], []);
            if (selected.Count > definition.Artifacts.Count) return new([], [new("artifact-limit", "")], []);
        }
        if (selected.Count == 0) return new([], [new("artifact-empty", "")], []);
        if (selected.Any(id => definition.Artifacts.All(item => item.Id != id)))
            return new([], [new("artifact-unknown", "")], []);

        var composition = new DefinitionArtifactComposer.ValidatedComposition(definition, answers, true, cancellationToken);
        if (composition.ValidationDiagnostics.Count != 0) return new([], composition.ValidationDiagnostics, []);
        var artifacts = new List<DefinitionArtifact>();
        var completions = new List<DefinitionDraftCompletion>();
        var diagnostics = new List<DefinitionDiagnostic>();
        long bytes = 0;
        bool transferred = false;
        try
        {
            foreach (ArtifactDefinition artifact in definition.Artifacts)
            {
                if (!selected.Contains(artifact.Id)) continue;
                if (activeOnly && !composition.IsEligible(artifact.Id)) continue;
                var locations = new List<DefinitionDraftCompletion>();
                DefinitionArtifactResult result = composition.Compose(artifact.Id, locations);
                if (!result.Succeeded)
                {
                    if (result.Diagnostics.All(item => item.Code is "draft-dependency-unresolved" or "artifact-omitted"))
                    {
                        diagnostics.AddRange(result.Diagnostics);
                        continue;
                    }
                    return new([], result.Diagnostics, []);
                }
                artifacts.Add(result.Artifact!);
                bytes += result.Artifact!.ByteLength;
                if (bytes > CompositionLimits.DefaultAggregateSourceBytes)
                    return new([], [new("bundle-limit", "")], []);
                completions.AddRange(locations);
            }
            cancellationToken.ThrowIfCancellationRequested();
            transferred = true;
            return new(artifacts, diagnostics, completions);
        }
        finally
        {
            if (!transferred) foreach (DefinitionArtifact artifact in artifacts) artifact.Dispose();
        }
    }
}
