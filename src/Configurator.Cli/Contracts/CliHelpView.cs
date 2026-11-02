namespace Configurator.Cli;


public sealed record CliHelpView(string Scope, IReadOnlyList<string> CommandPath,
    IReadOnlyList<string> Commands, IReadOnlyList<CliHelpOption> Options);

