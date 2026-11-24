using System.Text.Json.Serialization;

namespace Configurator.Cli;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false)]
[JsonSerializable(typeof(CliMachineEnvelope))]
[JsonSerializable(typeof(CliMachineInfo))]
[JsonSerializable(typeof(CliMachineDefinitionDescription))]
[JsonSerializable(typeof(CliMachineRevisionDiff))]
[JsonSerializable(typeof(CliMachineFixtureReport))]
public partial class CliJsonContext : JsonSerializerContext;

