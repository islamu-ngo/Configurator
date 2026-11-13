using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

/// <summary>
/// Options for <c>definition answer-schema</c>. Only a public definition is read; there is deliberately no answer,
/// input or workspace option, so private values cannot reach the generated editor schema.
/// </summary>
internal sealed class DefinitionAnswerSchemaSettings : DefinitionSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, DryRun = DryRun };
}
