using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class CatalogueListSettings : DefinitionSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, DryRun = DryRun };
}

