using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class DefinitionSourceSettings : CliCommandSettings
{
    [CommandOption("--source <PATH>")]
    public string? Source { get; set; }
    [CommandOption("--root <DIRECTORY>")]
    public string? Root { get; set; }
    [CommandOption("--against <PATH>")]
    public string? Against { get; set; }
    [CommandOption("--detail <KIND>")]
    public string? Detail { get; set; }
    [CommandOption("--offset <NUMBER>")]
    public int Offset { get; set; }
    [CommandOption("--limit <NUMBER>")]
    public int Limit { get; set; } = 16;
    [CommandOption("--expected-detail-digest <DIGEST>")]
    public string? ExpectedDetailDigest { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? ValidateDetail(Detail, Offset, Limit, ExpectedDetailDigest);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Source = Source,
            Root = Root,
            Against = Against,
            Detail = Detail,
            Offset = Offset,
            Limit = Limit,
            ExpectedDetailDigest = ExpectedDetailDigest
        };
}

