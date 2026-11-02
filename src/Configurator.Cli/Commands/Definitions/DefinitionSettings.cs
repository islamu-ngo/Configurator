using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class DefinitionSettings : CliCommandSettings
{
    [CommandOption("--definition <PATH>")]
    public string? Definition { get; set; }

    [CommandOption("--detail <KIND>")]
    public string? Detail { get; set; }
    [CommandOption("--offset <NUMBER>")]
    public int Offset { get; set; }
    [CommandOption("--limit <NUMBER>")]
    public int Limit { get; set; } = 16;
    [CommandOption("--expected-detail-digest <DIGEST>")]
    public string? ExpectedDetailDigest { get; set; }

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Definition) ? "definition-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Definition == "-" ? "definition-local-file-required" : null)
        ?? ValidateDetail(Detail, Offset, Limit, ExpectedDetailDigest);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Definition = Definition,
            Detail = Detail,
            Offset = Offset,
            Limit = Limit,
            ExpectedDetailDigest = ExpectedDetailDigest
        };
}

