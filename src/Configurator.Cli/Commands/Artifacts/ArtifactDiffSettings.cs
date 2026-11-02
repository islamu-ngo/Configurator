using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class ArtifactDiffSettings : ArtifactInputSettings
{
    [CommandOption("--baseline <PATH>")]
    public string? Baseline { get; set; }

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Baseline) ? "baseline-required" : null);

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateStdin(Input, Baseline, Answers);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Baseline = Baseline };
}

