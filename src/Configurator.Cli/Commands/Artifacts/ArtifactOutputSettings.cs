using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ArtifactOutputSettings : ArtifactInputSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    [CommandOption("--format <FORMAT>")]
    public string? Format { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun)
        ?? (Format is not null && Format is not ("json" or "yaml" or "dotenv" or "markdown") ? "format-not-supported" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Output = Output, DryRun = DryRun, Format = Format };
}

