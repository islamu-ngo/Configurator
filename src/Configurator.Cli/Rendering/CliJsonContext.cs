using System.Text.Json.Serialization;

namespace Configurator.Cli;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false)]
[JsonSerializable(typeof(CliMachineEnvelope))]
public partial class CliJsonContext : JsonSerializerContext;

