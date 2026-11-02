using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineLocationSegment(string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PropertyName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Index);
