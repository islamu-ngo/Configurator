namespace Configurator.Terminal;

internal enum TerminalPreparationCode
{
    Ready,
    Invalid,
    Cancelled,
    Retired,
    Failed
}

internal sealed record TerminalPreparationOutcome(TerminalPreparationCode Code,
    long InputRevision = 0, long Generation = 0, int ArtifactCount = 0,
    CancellationToken CancellationToken = default);
