using Configurator.Core;
using Configurator.Core.Definitions;

namespace Configurator.Cli;

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

    internal static CliCommandResult Failure(CliExitCode exit, string code, string path = "/arguments") =>
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
