using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class SchemaExportSettings : CliCommandSettings
{
    [CommandOption("--kind <KIND>")]
    public string Kind { get; set; } = "authoring";

    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (!CliDiscoveryHandlers.IsSupportedSchemaKind(Kind) ? "schema-kind-unknown" : null)
        ?? ValidateOutput(Output, required: false);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            SchemaKind = Kind,
            Output = Output,
            DryRun = DryRun
        };
}
