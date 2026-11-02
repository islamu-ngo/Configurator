using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionBuildSettings : DefinitionSourceSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }
    [CommandOption("--id-map <PATH>")]
    public string? IdMap { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, IdMap = IdMap };
}

