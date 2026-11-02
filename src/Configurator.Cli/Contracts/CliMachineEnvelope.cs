using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineEnvelope
{
    private readonly CliMachineDiagnostic[] _diagnostics;
    private readonly CliMachineArtifact[] _artifacts;
    public CliMachineEnvelope(string schemaVersion, CliMachineInvocation invocation,
        string status, string exitCategory, int exitCode, bool dryRun,
        IEnumerable<CliMachineDiagnostic> diagnostics, IEnumerable<CliMachineArtifact> artifacts,
        CliMachineCoverage coverage, CliMachineReadiness readiness)
    {
        SchemaVersion = schemaVersion;
        Invocation = invocation;
        Status = status;
        ExitCategory = exitCategory;
        ExitCode = exitCode;
        DryRun = dryRun;
        _diagnostics = diagnostics.ToArray();
        _artifacts = artifacts.ToArray();
        Coverage = coverage;
        Readiness = readiness;
    }
    public string SchemaVersion { get; }
    public CliMachineInvocation Invocation { get; }
    public string Status { get; }
    public string ExitCategory { get; }
    public int ExitCode { get; }
    public bool DryRun { get; }
    public string Completion { get; init; } = "completed";
    public string? Preparation { get; init; }
    public string TargetAcceptance { get; init; } = "unassessed";
    public string Cleanup { get; init; } = "not-required";
    public string Delivery { get; init; } = "attempt";
    public int DiagnosticsTotal { get; init; }
    public int DiagnosticsOmitted { get; init; }
    public string DetailDigest { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliMachineDetail? Detail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliMachineAssessment? Assessment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliHelpView? Help { get; init; }
    public IReadOnlyList<CliMachineDiagnostic> Diagnostics => Array.AsReadOnly((CliMachineDiagnostic[])_diagnostics.Clone());
    public IReadOnlyList<CliMachineArtifact> Artifacts => Array.AsReadOnly((CliMachineArtifact[])_artifacts.Clone());
    public CliMachineCoverage Coverage { get; }
    public CliMachineReadiness Readiness { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoringContractVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuntimeSchemaVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliMachineSchema? Schema { get; init; }
}

