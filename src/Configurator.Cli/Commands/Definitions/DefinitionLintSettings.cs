using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

/// <summary>Options for <c>definition lint</c>; <c>--quality</c> adds authoring quality advisories.</summary>
internal sealed class DefinitionLintSettings : DefinitionSourceSettings
{
    [CommandOption("--quality")]
    public bool Quality { get; set; }

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Quality = Quality };
}
