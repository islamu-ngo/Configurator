using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

/// <summary>Options for <c>definition lint</c>; <c>--quality</c> adds authoring quality advisories and <c>--strict</c> fails with Data when diagnostics exist.</summary>
internal sealed class DefinitionLintSettings : DefinitionSourceSettings
{
    [CommandOption("--quality")]
    public bool Quality { get; set; }

    [CommandOption("--strict")]
    public bool Strict { get; set; }

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Quality = Quality, Strict = Strict };
}
