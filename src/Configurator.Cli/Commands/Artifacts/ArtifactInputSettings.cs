using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ArtifactInputSettings : ArtifactCompareSettings
{
    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    [CommandOption("--answers <PATH>")]
    public string? Answers { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? ValidateInputRevision(InputRevision) ?? ValidateStdin(Input, Answers);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifact = Artifact, Input = Input, InputRevision = InputRevision, Answers = Answers };
}

