using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineDetail(string Kind, int Offset, int Limit, int RetainedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total, string Digest,
    IReadOnlyList<CliMachineDiagnostic> Diagnostics, IReadOnlyList<CliMachineCoverageEntry> Coverage,
    IReadOnlyList<CliMachineFinding> Findings)
{
    private ReadOnlyCollection<CliMachineDiagnostic> _diagnostics = Array.AsReadOnly(Diagnostics.ToArray());
    private ReadOnlyCollection<CliMachineCoverageEntry> _coverage = Array.AsReadOnly(Coverage.ToArray());
    private ReadOnlyCollection<CliMachineFinding> _findings = Array.AsReadOnly(Findings.ToArray());
    public IReadOnlyList<CliMachineDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<CliMachineCoverageEntry> Coverage
    {
        get => _coverage;
        init => _coverage = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<CliMachineFinding> Findings
    {
        get => _findings;
        init => _findings = Array.AsReadOnly(value.ToArray());
    }
    public int ReturnedCount => Diagnostics.Count + Coverage.Count + Findings.Count;
    public bool Terminal => Offset + ReturnedCount == RetainedCount;
    public int? NextOffset => Terminal ? null : Offset + ReturnedCount;
}
