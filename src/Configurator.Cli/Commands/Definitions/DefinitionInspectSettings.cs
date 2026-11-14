using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

/// <summary>Read-only public source inspection; no answer or publication authority is accepted.</summary>
internal sealed class DefinitionInspectSettings : CliCommandSettings
{
    [CommandOption("--source <PATH>")]
    public string? Source { get; set; }
    [CommandOption("--root <DIRECTORY>")]
    public string? Root { get; set; }
    [CommandOption("--offset <NUMBER>")]
    public int Offset { get; set; }
    [CommandOption("--limit <NUMBER>")]
    public int Limit { get; set; } = 16;
    [CommandOption("--expected-detail-digest <DIGEST>")]
    public string? ExpectedDetailDigest { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Offset < 0 ? "workflow-offset-invalid"
            : Limit is < 1 or > 32 ? "workflow-limit-invalid"
            : ExpectedDetailDigest is not null && !IsDigest(ExpectedDetailDigest) ? "workflow-digest-invalid" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Source = Source,
            Root = Root,
            Offset = Offset,
            Limit = Limit,
            ExpectedDetailDigest = ExpectedDetailDigest
        };
}
