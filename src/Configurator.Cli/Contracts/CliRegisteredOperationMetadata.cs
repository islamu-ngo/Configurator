namespace Configurator.Cli;


public sealed record CliRegisteredOperationMetadata(
    string Name,
    IReadOnlyList<string> Options);

