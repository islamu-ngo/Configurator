namespace Configurator.Core.Artifacts;

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
        (_, IReadOnlyList<DefinitionArtifact> artifacts, IReadOnlyList<DefinitionDiagnostic> diagnostics,
            IReadOnlyList<DefinitionDraftCompletion> completions) = DefinitionImportWork.CompleteSynchronously(
            DefinitionPreparationSession.PreparePartsAsync(definition, artifactIds, answers, draft: true,
                activeOnly, cooperativeYield: null, cancellationToken));
        return new(artifacts, diagnostics, completions);
    }
}
