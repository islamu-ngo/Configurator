using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;


internal interface ICliCommandSettings
{
    string? ValidateInput();
    CliCommand Bind(string family, string operation, CliMode invocationMode);
}

