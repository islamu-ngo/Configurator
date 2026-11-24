namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

/// <summary>Result of a single-pass cooperative or synchronous artifact bundle preparation.</summary>
public sealed class DefinitionPreparationResult : IDisposable
{
    internal DefinitionPreparationResult(
        DefinitionValidationResult? validation,
        IEnumerable<DefinitionArtifact> artifacts,
        IEnumerable<DefinitionDiagnostic> diagnostics,
        IEnumerable<DefinitionDraftCompletion> completions,
        bool isDraftMode)
    {
        Validation = validation;
        Artifacts = Array.AsReadOnly(artifacts.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Completions = Array.AsReadOnly(completions.ToArray());
        IsDraftMode = isDraftMode;
    }

    public DefinitionValidationResult? Validation { get; }
    public IReadOnlyList<DefinitionArtifact> Artifacts { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DefinitionDraftCompletion> Completions { get; }
    public bool IsDraftMode { get; }
    public bool IsDraft => Completions.Count != 0;
    public bool Succeeded => IsDraftMode
        ? Artifacts.Count > 0 && Diagnostics.Count == 0
        : Diagnostics.Count == 0;

    public void Dispose()
    {
        foreach (DefinitionArtifact artifact in Artifacts) artifact.Dispose();
        GC.SuppressFinalize(this);
    }

    public override string ToString() =>
        $"{nameof(DefinitionPreparationResult)}:Succeeded={Succeeded}:Draft={IsDraftMode}:Count={Artifacts.Count}:Deferred={Completions.Count}";
}

/// <summary>Owns single-pass cooperative validation and artifact projection with bounded yielding and private-buffer zeroing.</summary>
public static class DefinitionPreparationSession
{
    private const int DefaultYieldQuantum = 256;

    /// <summary>Prepares the explicitly selected artifacts in one validation pass.</summary>
    public static ValueTask<DefinitionPreparationResult> PrepareAsync(
        Definition definition,
        IEnumerable<string> artifactIds,
        AnswerSnapshot answers,
        bool draft = false,
        Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default) =>
        PrepareCoreAsync(definition, artifactIds, answers, draft, skipOmitted: false, cooperativeYield, cancellationToken);

    /// <summary>Prepares all active, eligible complete artifacts in one validation pass.</summary>
    public static ValueTask<DefinitionPreparationResult> PrepareActiveAsync(
        Definition definition,
        AnswerSnapshot answers,
        Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return PrepareCoreAsync(definition, definition.Artifacts.Select(item => item.Id), answers, draft: false,
            skipOmitted: true, cooperativeYield, cancellationToken);
    }

    /// <summary>Prepares all active, eligible draft artifacts (explicitly deferring missing secrets) in one validation pass.</summary>
    public static ValueTask<DefinitionPreparationResult> PrepareActiveDraftAsync(
        Definition definition,
        AnswerSnapshot answers,
        Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return PrepareCoreAsync(definition, definition.Artifacts.Select(item => item.Id), answers, draft: true,
            skipOmitted: true, cooperativeYield, cancellationToken);
    }

    internal static async ValueTask<DefinitionPreparationResult> PrepareCoreAsync(
        Definition definition,
        IEnumerable<string> artifactIds,
        AnswerSnapshot answers,
        bool draft,
        bool skipOmitted,
        Func<CancellationToken, ValueTask>? cooperativeYield,
        CancellationToken cancellationToken)
    {
        (DefinitionValidationResult? validation, IReadOnlyList<DefinitionArtifact> artifacts,
            IReadOnlyList<DefinitionDiagnostic> diagnostics, IReadOnlyList<DefinitionDraftCompletion> completions) =
            await PreparePartsAsync(definition, artifactIds, answers, draft, skipOmitted, cooperativeYield, cancellationToken);
        return new(validation, artifacts, diagnostics, completions, draft);
    }

    internal static async ValueTask<(DefinitionValidationResult? Validation,
        IReadOnlyList<DefinitionArtifact> Artifacts,
        IReadOnlyList<DefinitionDiagnostic> Diagnostics,
        IReadOnlyList<DefinitionDraftCompletion> Completions)> PreparePartsAsync(
        Definition definition,
        IEnumerable<string> artifactIds,
        AnswerSnapshot answers,
        bool draft,
        bool skipOmitted,
        Func<CancellationToken, ValueTask>? cooperativeYield,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactIds);
        ArgumentNullException.ThrowIfNull(answers);
        cancellationToken.ThrowIfCancellationRequested();

        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in artifactIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (id is null || !selected.Add(id))
                return (null, [], [new("artifact-duplicate", "")], []);
            if (selected.Count > definition.Artifacts.Count)
                return (null, [], [new("artifact-limit", "")], []);
        }
        if (selected.Count == 0)
            return (null, [], [new("artifact-empty", "")], []);
        if (selected.Any(id => definition.Artifacts.All(item => item.Id != id)))
            return (null, [], [new("artifact-unknown", "")], []);

        CooperativeYieldGate? yieldGate = cooperativeYield is null
            ? null
            : new CooperativeYieldGate(cooperativeYield, DefaultYieldQuantum, cancellationToken);

        DefinitionArtifactComposer.ValidatedComposition composition =
            await DefinitionArtifactComposer.ValidatedComposition.CreateAsync(
                definition, answers, draft, yieldGate, cancellationToken);
        DefinitionValidationResult validation = composition.Validation;
        if (composition.ValidationDiagnostics.Count != 0)
            return (validation, [], composition.ValidationDiagnostics, []);

        var composed = new List<DefinitionArtifact>(selected.Count);
        var completions = draft ? new List<DefinitionDraftCompletion>() : null;
        var diagnostics = draft ? new List<DefinitionDiagnostic>() : null;
        long totalBytes = 0;
        bool transferred = false;
        try
        {
            int artifactIndex = 0;
            foreach (ArtifactDefinition artifact in definition.Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!selected.Contains(artifact.Id)) continue;
                if (skipOmitted && !composition.IsEligible(artifact.Id)) continue;
                if (artifactIndex++ > 0 && yieldGate is not null)
                    await yieldGate.YieldStageAsync();

                var locations = draft ? new List<DefinitionDraftCompletion>() : [];
                DefinitionArtifactResult result = await composition.ComposeAsync(artifact.Id, locations);
                if (!result.Succeeded)
                {
                    if (draft)
                    {
                        if (result.Diagnostics.All(item => item.Code is "draft-dependency-unresolved" or "artifact-omitted"))
                        {
                            diagnostics!.AddRange(result.Diagnostics);
                            continue;
                        }
                        return (validation, [], result.Diagnostics, []);
                    }
                    if (skipOmitted && result.Diagnostics.Count == 1 && result.Diagnostics[0].Code == "artifact-omitted")
                        continue;
                    return (validation, [], result.Diagnostics, []);
                }

                DefinitionArtifact output = result.Artifact!;
                composed.Add(output);
                totalBytes += output.ByteLength;
                if (totalBytes > CompositionLimits.DefaultAggregateSourceBytes)
                    return (validation, [], [new("bundle-limit", "")], []);
                if (draft)
                    completions!.AddRange(locations);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!draft && composed.Count == 0)
                return (validation, [], [new("artifact-empty", "")], []);

            transferred = true;
            return (validation, composed, diagnostics ?? (IReadOnlyList<DefinitionDiagnostic>)[],
                completions ?? (IReadOnlyList<DefinitionDraftCompletion>)[]);
        }
        finally
        {
            if (!transferred)
            {
                foreach (DefinitionArtifact artifact in composed)
                    artifact.Dispose();
            }
        }
    }
}
