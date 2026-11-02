using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class CatalogueItemSettings : CatalogueListSettings
{
    [CommandOption("--key <ID>")]
    public string? Key { get; set; }

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Key) ? "key-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Key is not null && !IsIdentifier(Key) ? "catalogue-key-invalid" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Key = Key };
}

