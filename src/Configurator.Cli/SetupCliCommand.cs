using Configurator.Core;
using Configurator.Core.Definitions;

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
}

internal sealed record SetupCliCommandResult(
    SetupCliExitCode Exit, IReadOnlyList<SetupCliMachineDiagnostic> Diagnostics,
    IReadOnlyList<SetupCliMachineArtifact> Artifacts, SetupCliMachineCoverage Coverage,
    SetupCliMachineReadiness Readiness)
{
    internal string? DefinitionDigest { get; init; }
    internal SetupCliHelpView? Help { get; init; }
    internal string Completion { get; init; } = "completed";
    internal string? Preparation { get; init; }
    internal string Cleanup { get; init; } = "not-required";
    internal SetupCliMachineSchema? Schema { get; init; }
}

internal static class SetupCliResults
{
    internal static SetupCliMachineDiagnostic Diagnostic(DefinitionDiagnostic diagnostic) =>
        new(diagnostic.Code, "/input", "error")
        {
            Correction = diagnostic.Location is { } location
                ? new(location.InputRevision, diagnostic.FieldId, Array.AsReadOnly(location.Segments.Select(segment =>
                    new SetupCliMachineLocationSegment(segment.Kind switch
                    {
                        DefinitionLocationSegmentKind.Property => "property",
                        DefinitionLocationSegmentKind.ArrayRow => "arrayRow",
                        DefinitionLocationSegmentKind.MapEntry => "mapEntry",
                        _ => throw new InvalidOperationException("diagnostic-location")
                    }, segment.PropertyName, segment.Index)).ToArray())) : null
        };

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
