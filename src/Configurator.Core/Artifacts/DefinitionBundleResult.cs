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
