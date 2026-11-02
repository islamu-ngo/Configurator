namespace Configurator.Cli;

public sealed record CliPublicationResult(IReadOnlyList<CliPublicationOutcome> Outcomes,
    bool CleanupFailed = false, bool Cancelled = false);

