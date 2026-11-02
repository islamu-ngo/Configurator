namespace Configurator.Cli;

internal sealed record CliCommandResult(
    CliExitCode Exit, IReadOnlyList<CliMachineDiagnostic> Diagnostics,
    IReadOnlyList<CliMachineArtifact> Artifacts, CliMachineCoverage Coverage,
    CliMachineReadiness Readiness)
{
    internal string? DefinitionDigest { get; init; }
    internal CliHelpView? Help { get; init; }
    internal string Completion { get; init; } = "completed";
    internal string? Preparation { get; init; }
    internal string Cleanup { get; init; } = "not-required";
    internal CliMachineSchema? Schema { get; init; }
    internal CliMachineAssessment? Assessment { get; init; }
}

