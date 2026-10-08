using Configurator.Core;

namespace Configurator.Cli;

internal sealed record SetupCliCommand(
    string Family, string Operation, bool Machine, bool DryRun, bool Help,
    string? Definition, string? Artifact, string? Input, string? Baseline,
    string? Output, string? Key)
{
    internal string? Format { get; init; }
    internal string InputRevision { get; init; } = "0";
    internal string? ExpectedRevision { get; init; }
    internal string? RevisionField { get; init; }
    internal string? Answers { get; init; }
}

internal sealed record SetupCliCommandResult(
    SetupCliExitCode Exit, IReadOnlyList<SetupCliMachineDiagnostic> Diagnostics,
    IReadOnlyList<SetupCliMachineArtifact> Artifacts, SetupCliMachineCoverage Coverage,
    SetupCliMachineReadiness Readiness);

internal static class SetupCliResults
{
    internal static SetupCliCommandResult Success(IReadOnlyList<SetupCliMachineArtifact>? artifacts = null,
        SetupCliMachineCoverage? coverage = null, SetupCliMachineReadiness? readiness = null) =>
        new(SetupCliExitCode.Success, [], artifacts ?? [], coverage ?? EmptyCoverage(), readiness ?? Ready());

    internal static SetupCliCommandResult Failure(SetupCliExitCode exit, string code, string path = "$.arguments") =>
        new(exit, [new(code, path, "error")], [], EmptyCoverage(), exit switch
        {
            SetupCliExitCode.Blocked => new("blocked", [], ["command-boundary"]),
            SetupCliExitCode.Validation or SetupCliExitCode.Data or SetupCliExitCode.Incomplete =>
                new("incomplete", ["input-invalid"], []),
            _ => Ready()
        });

    internal static SetupCliMachineArtifact Artifact(string kind, string mediaType, ReadOnlySpan<byte> bytes,
        string sensitivity, string pathIntent, string writeStatus, SetupCliMachineCoverage? coverage = null,
        SetupCliMachineReadiness? readiness = null) =>
        new(kind, mediaType, ArtifactDigest.Compute(bytes).Value, sensitivity, coverage ?? EmptyCoverage(),
            readiness ?? Ready(), pathIntent, writeStatus);

    internal static SetupCliMachineCoverage EmptyCoverage() => new([], []);
    internal static SetupCliMachineReadiness Ready() => new("ready", [], []);
    internal static string PathIntent(string? path) => path == "-" ? "stdout" : path is null ? "none" : "output";
    internal static string Lower<T>(T value) => value!.ToString()!.ToLowerInvariant();
}
