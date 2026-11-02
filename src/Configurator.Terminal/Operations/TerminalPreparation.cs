namespace Configurator.Terminal;

internal sealed record TerminalPreparation(long InputRevision, int ArtifactCount)
{
    public override string ToString() => $"{nameof(TerminalPreparation)}:Count={ArtifactCount}";
}
