namespace Configurator.Cli;

internal sealed record CliFamilyDescriptor(string Name, IReadOnlyList<CliOperationDescriptor> Operations);

