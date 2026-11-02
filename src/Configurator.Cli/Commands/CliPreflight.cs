namespace Configurator.Cli;


internal sealed record CliPreflight(
    CliCommand Command,
    string[] Arguments,
    bool RunSpectre,
    string? Error);

