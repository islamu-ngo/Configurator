using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ArtifactInputSettings : ArtifactCompareSettings
{
    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    protected override string? ValidateValues() => base.ValidateValues()
        ?? ValidateInputRevision(InputRevision);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { InputRevision = InputRevision };
}

