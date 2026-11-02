using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineDiagnostic(string Code, string Path, string Severity)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Line { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Column { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ByteOffset { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CliMachineSourceLocation? Source { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CliMachineCorrectionLocation? Correction { get; init; }
}
