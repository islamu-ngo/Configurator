namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

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
