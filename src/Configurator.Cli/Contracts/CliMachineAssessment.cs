using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineAssessment(string DefinitionDigest, string ArtifactId, string Compatibility,
    string Coverage, string Inventory, string Autofill, int RetainedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total)
{
    private ReadOnlyCollection<CliMachineFinding> _findings = Array.AsReadOnly(Array.Empty<CliMachineFinding>());
    [JsonIgnore]
    public IReadOnlyList<CliMachineFinding> Findings
    {
        get => _findings;
        init => _findings = Array.AsReadOnly(value.ToArray());
    }
}
