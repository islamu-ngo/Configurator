using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineArtifact(string Kind, string MediaType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Digest, string Sensitivity,
    [property: JsonIgnore] CliMachineCoverage Coverage,
    [property: JsonIgnore] CliMachineReadiness Readiness, string PathIntent, string WriteStatus)
{
    public string Id { get; init; } = Kind;
    public string Installation { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
    public string Verification { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
}

