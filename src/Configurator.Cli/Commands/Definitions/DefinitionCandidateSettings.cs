using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionCandidateSettings : DefinitionSourceSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output };
}
