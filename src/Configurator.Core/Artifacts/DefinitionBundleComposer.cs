namespace Configurator.Core.Artifacts;

using Configurator.Core.Definitions;

public static class DefinitionBundleComposer
{
    public static DefinitionBundleResult Compose(Definition definition, IEnumerable<string> artifactIds,
        AnswerSnapshot answers, CancellationToken cancellationToken = default)
        => ComposeCore(definition, artifactIds, answers, skipOmitted: false, cancellationToken);

    /// <summary>Composes declared active artifacts. Omitted artifacts are skipped; validation and output limits remain strict.</summary>
    public static DefinitionBundleResult ComposeActive(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ComposeCore(definition, definition.Artifacts.Select(item => item.Id), answers, skipOmitted: true, cancellationToken);
    }

    private static DefinitionBundleResult ComposeCore(Definition definition, IEnumerable<string> artifactIds,
        AnswerSnapshot answers, bool skipOmitted, CancellationToken cancellationToken)
    {
        (_, IReadOnlyList<DefinitionArtifact> artifacts, IReadOnlyList<DefinitionDiagnostic> diagnostics, _) =
            DefinitionImportWork.CompleteSynchronously(
                DefinitionPreparationSession.PreparePartsAsync(definition, artifactIds, answers, draft: false,
                    skipOmitted, cooperativeYield: null, cancellationToken));
        return new(artifacts, diagnostics);
    }
}
