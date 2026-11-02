using Configurator.Artifacts;

namespace Configurator.Cli;

public sealed record CliPublicationOutcome(ProtectedArtifactCommitResult? Result, bool Unknown = false,
    bool DeliveredToStandardOutput = false);
