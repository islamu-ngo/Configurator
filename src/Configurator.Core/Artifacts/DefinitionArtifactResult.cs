namespace Configurator.Core.Artifacts;

using Configurator.Core.Definitions;

public sealed class DefinitionArtifactResult
{
    internal DefinitionArtifactResult(DefinitionArtifact? artifact, IEnumerable<DefinitionDiagnostic> diagnostics)
    {
        Artifact = artifact;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
    public DefinitionArtifact? Artifact { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public bool Succeeded => Artifact is not null && Diagnostics.Count == 0;
    public override string ToString() => $"{nameof(DefinitionArtifactResult)}:Succeeded={Succeeded}";
}

