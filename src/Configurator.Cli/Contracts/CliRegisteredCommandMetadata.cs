namespace Configurator.Cli;

public sealed record CliRegisteredCommandMetadata(
    string Name,
    IReadOnlyList<CliRegisteredOperationMetadata> Operations,
    IReadOnlyList<string> Options);

