using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineFixtureCase(
    string CaseId,
    string Kind,
    string Outcome,
    bool ExpectedRefusal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FailureCode,
    IReadOnlyList<string> ObservedDiagnosticCodes,
    IReadOnlyList<string> ObservedArtifactIds);

public sealed record CliMachineFixtureReport(
    string DefinitionDigest,
    string FixtureDigest,
    string EngineVersion,
    string Scope,
    string ClaimBoundary,
    bool Passed,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    int ExpectedRefusalCases,
    IReadOnlyList<CliMachineFixtureCase> Cases);
