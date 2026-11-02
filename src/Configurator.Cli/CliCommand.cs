using Configurator.Core;
using Configurator.Core.Definitions;

namespace Configurator.Cli;

internal sealed record CliCommand(
    string Family, string Operation, bool Machine, bool DryRun, bool Help,
    string? Definition, string? Artifact, string? Input, string? Baseline,
    string? Output, string? Key)
{
    internal string? Format { get; init; }
    internal string InputRevision { get; init; } = "0";
    internal string? ExpectedRevision { get; init; }
    internal string? RevisionField { get; init; }
    internal string? Answers { get; init; }
    internal string[]? Artifacts { get; init; }
    internal string? Detail { get; init; }
    internal int Offset { get; init; }
    internal int Limit { get; init; } = 16;
    internal string? ExpectedDetailDigest { get; init; }
    internal string? Source { get; init; }
    internal string? Root { get; init; }
    internal string? Against { get; init; }
    internal string? IdMap { get; init; }
    internal string? ProjectId { get; init; }
    internal string? ProjectName { get; init; }
    internal string? ApplicationVersion { get; init; }
    internal string? ForkFrom { get; init; }
    internal string? FromEnvExample { get; init; }
    internal string? FromSchema { get; init; }
    internal bool AcceptBoundedSemantics { get; init; }
    internal string SchemaKind { get; init; } = "authoring";
    internal string? Pointer { get; init; }
    internal string? SecretInput { get; init; }
    internal string? GeneratedPasswordOutput { get; init; }
}

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

internal static class CliResults
{
    internal static CliMachineDiagnostic Diagnostic(DefinitionDiagnostic diagnostic) =>
        new(diagnostic.Code, "/input", "error")
        {
            Correction = diagnostic.Location is { } location
                ? new(location.InputRevision, diagnostic.FieldId, Array.AsReadOnly(location.Segments.Select(segment =>
                    new CliMachineLocationSegment(segment.Kind switch
                    {
                        DefinitionLocationSegmentKind.Property => "property",
                        DefinitionLocationSegmentKind.ArrayRow => "arrayRow",
                        DefinitionLocationSegmentKind.MapEntry => "mapEntry",
                        _ => throw new InvalidOperationException("diagnostic-location")
                    }, segment.PropertyName, segment.Index)).ToArray())) : null
        };

    internal static CliCommandResult Success(IReadOnlyList<CliMachineArtifact>? artifacts = null,
        CliMachineCoverage? coverage = null, CliMachineReadiness? readiness = null) =>
        new(CliExitCode.Success, [], artifacts ?? [], coverage ?? EmptyCoverage(), readiness ?? Ready());

    internal static CliCommandResult Failure(CliExitCode exit, string code, string path = "$.arguments") =>
        new(exit, [new(code, path, "error")], [], EmptyCoverage(), exit switch
        {
            CliExitCode.Blocked => new("blocked", [], ["command-boundary"]),
            CliExitCode.Validation or CliExitCode.Data or CliExitCode.Incomplete =>
                new("incomplete", ["input-invalid"], []),
            _ => Ready()
        });

    internal static CliMachineArtifact Artifact(string kind, string mediaType, ReadOnlySpan<byte> bytes,
        string sensitivity, string pathIntent, string writeStatus, CliMachineCoverage? coverage = null,
        CliMachineReadiness? readiness = null) =>
        new(kind, mediaType, ArtifactDigest.Compute(bytes).Value, sensitivity, coverage ?? EmptyCoverage(),
            readiness ?? Ready(), pathIntent, writeStatus);

    internal static CliMachineCoverage EmptyCoverage() => new([], []);
    internal static CliMachineReadiness Ready() => new("ready", [], []);
    internal static string PathIntent(string? path) => path == "-" ? "stdout" : path is null ? "none" : "output";
    internal static string Lower<T>(T value) => value!.ToString()!.ToLowerInvariant();
}
