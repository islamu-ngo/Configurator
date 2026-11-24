using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionDiffSettings : CliCommandSettings
{
    [CommandOption("--against <PATH>")]
    public string? Against { get; set; }

    [CommandOption("--source <PATH>")]
    public string? Source { get; set; }

    [CommandOption("--root <DIRECTORY>")]
    public string? Root { get; set; }

    protected override string? ValidateRequired() =>
        string.IsNullOrEmpty(Against) ? "against-required" : null;

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Against == "-" || Source == "-" || Root == "-" ? "stdin-not-supported" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Against = Against,
            Source = Source,
            Root = Root
        };
}
