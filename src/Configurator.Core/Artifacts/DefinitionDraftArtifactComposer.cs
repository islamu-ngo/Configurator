namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

public sealed record DefinitionDraftCompletion(string ArtifactId, string FieldPath, string OutputPath);

public sealed class DefinitionDraftArtifactResult : IDisposable
{
    internal DefinitionDraftArtifactResult(DefinitionArtifact? artifact,
        IEnumerable<DefinitionDiagnostic> diagnostics, IEnumerable<DefinitionDraftCompletion> completions)
    {
        Artifact = artifact;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Completions = Array.AsReadOnly(completions.ToArray());
    }
    public DefinitionArtifact? Artifact { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DefinitionDraftCompletion> Completions { get; }
    public bool Succeeded => Artifact is not null && Diagnostics.Count == 0;
    public bool IsDraft => Completions.Count != 0;
    public void Dispose() { Artifact?.Dispose(); GC.SuppressFinalize(this); }
    public override string ToString() => $"{nameof(DefinitionDraftArtifactResult)}:Succeeded={Succeeded}:Deferred={Completions.Count}";
}

public sealed class DefinitionDraftBundleResult : IDisposable
{
    internal DefinitionDraftBundleResult(IEnumerable<DefinitionArtifact> artifacts,
        IEnumerable<DefinitionDiagnostic> diagnostics, IEnumerable<DefinitionDraftCompletion> completions)
    {
        Artifacts = Array.AsReadOnly(artifacts.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Completions = Array.AsReadOnly(completions.ToArray());
    }
    public IReadOnlyList<DefinitionArtifact> Artifacts { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DefinitionDraftCompletion> Completions { get; }
    public bool Succeeded => Artifacts.Count > 0 && Diagnostics.Count == 0;
    public bool IsDraft => Completions.Count != 0;
    public void Dispose() { foreach (DefinitionArtifact artifact in Artifacts) artifact.Dispose(); GC.SuppressFinalize(this); }
    public override string ToString() => $"{nameof(DefinitionDraftBundleResult)}:Succeeded={Succeeded}:Deferred={Completions.Count}";
}

/// <summary>Prepares explicitly incomplete output while complete composition remains strict.</summary>
public static class DefinitionDraftArtifactComposer
{
    public static DefinitionDraftArtifactResult Compose(SetupDefinition definition, string artifactId,
        SetupAnswerSnapshot answers, CancellationToken cancellationToken = default)
    {
        var completions = new List<DefinitionDraftCompletion>();
        DefinitionArtifactResult result = DefinitionArtifactComposer.ComposeCore(definition, artifactId, answers, true, completions, cancellationToken);
        return new(result.Artifact, result.Diagnostics, result.Succeeded ? completions : []);
    }

    public static DefinitionDraftBundleResult ComposeBundle(SetupDefinition definition,
        IEnumerable<string> artifactIds, SetupAnswerSnapshot answers, CancellationToken cancellationToken = default)
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
        var artifacts = new List<DefinitionArtifact>();
        var completions = new List<DefinitionDraftCompletion>();
        var diagnostics = new List<DefinitionDiagnostic>();
        long bytes = 0;
        bool transferred = false;
        try
        {
            foreach (SetupArtifactDefinition artifact in definition.Artifacts)
            {
                if (!selected.Contains(artifact.Id)) continue;
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
                if (bytes > SetupCompositionLimits.DefaultAggregateSourceBytes)
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
