using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ArtifactInputSettings : ArtifactCompareSettings
{
    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    [CommandOption("--format <FORMAT>")]
    public string? Format { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? ValidateInputRevision(InputRevision)
        ?? (Format is not null && Format is not ("json" or "yaml" or "dotenv" or "markdown") ? "format-not-supported" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { InputRevision = InputRevision, Format = Format };
}

