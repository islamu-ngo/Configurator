namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

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
