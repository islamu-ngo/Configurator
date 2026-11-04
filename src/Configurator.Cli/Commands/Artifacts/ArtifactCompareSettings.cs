using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class ArtifactCompareSettings : DefinitionSettings
{
    [CommandOption("--artifact <ID>")]
    public string? Artifact { get; set; }

    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }

    [CommandOption("--answers <PATH>")]
    public string? Answers { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Artifact) ? "artifact-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Artifact is not null && !IsIdentifier(Artifact) ? "identifier-invalid" : null)
        ?? ValidateStdin(Input, Answers);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Artifact = Artifact, Input = Input, Answers = Answers };
}

