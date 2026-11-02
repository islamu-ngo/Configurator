using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionSchemaSettings : CliCommandSettings
{
    [CommandOption("--kind <KIND>")]
    public string Kind { get; set; } = "authoring";

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Kind is not ("authoring" or "runtime" or "answers" or "command") ? "definition-schema-kind" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { SchemaKind = Kind };
}

