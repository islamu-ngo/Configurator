using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineFinding(string Classification, string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CliMachineFindingSource? Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CliMachineFindingSchema? Schema);
