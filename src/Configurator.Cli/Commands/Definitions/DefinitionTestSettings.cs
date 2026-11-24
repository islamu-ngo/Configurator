using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionTestSettings : CliCommandSettings
{
    [CommandOption("--fixtures <PATH>")]
    public string? Fixtures { get; set; }

    [CommandOption("--source <PATH>")]
    public string? Source { get; set; }

    [CommandOption("--root <DIRECTORY>")]
    public string? Root { get; set; }

    protected override string? ValidateRequired() =>
        string.IsNullOrEmpty(Fixtures) ? "fixtures-required" : null;

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Fixtures == "-" || Source == "-" || Root == "-" ? "stdin-not-supported" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Fixtures = Fixtures,
            Source = Source,
            Root = Root
        };
}
