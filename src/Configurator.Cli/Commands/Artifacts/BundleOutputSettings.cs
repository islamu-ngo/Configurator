using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class BundleOutputSettings : BundleInputSettings
{
    [CommandOption("--output <DIRECTORY>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, DryRun = DryRun };
}

