using Configurator.Core.Definitions;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class SecretDeriveSettings : SecretSettings
{
    [CommandOption("--secret-input-file <PATH>")]
    public string? SecretInputFile { get; set; }
    [CommandOption("--secret-input-stdin")]
    public bool SecretInputStdin { get; set; }

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (SecretInputFile is null && !SecretInputStdin ? "secret-input-required" : null)
        ?? (SecretInputFile is not null && SecretInputStdin ? "secret-input-conflict" : null)
        ?? (SecretInputFile is "" or "-" ? "secret-input-file-invalid" : null)
        ?? (Input == "-" && SecretInputStdin ? "stdin-conflict" : null);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { SecretInput = SecretInputStdin ? "-" : SecretInputFile };
}
