using Configurator.Core.Definitions;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class SecretGenerateSettings : SecretSettings
{
    [CommandOption("--generated-password-output <PATH>")]
    public string? GeneratedPasswordOutput { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (GeneratedPasswordOutput is "" or "-" ? "generated-password-output-invalid" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { GeneratedPasswordOutput = GeneratedPasswordOutput };
}

