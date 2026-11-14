namespace Configurator.Terminal;

internal sealed record TerminalPreparation(long InputRevision, int ArtifactCount,
    Configurator.Presentation.DefinitionOutputExportContext ExportContext)
{
    public override string ToString() => $"{nameof(TerminalPreparation)}:Count={ArtifactCount}";
}
