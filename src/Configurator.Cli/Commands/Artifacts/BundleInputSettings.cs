using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class BundleInputSettings : DefinitionSettings
{
    [CommandOption("--artifact <ID>")]
    public string[] Artifacts { get; set; } = [];

    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }

    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (Artifacts.Length == 0 ? "artifact-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Artifacts.Any(artifact => !IsIdentifier(artifact)) ? "identifier-invalid" : null)
        ?? (Artifacts.Length > 64 || Artifacts.Distinct(StringComparer.Ordinal).Count() != Artifacts.Length
            ? "artifact-selection-invalid" : null)
        ?? ValidateInputRevision(InputRevision);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifacts = Artifacts, Input = Input, InputRevision = InputRevision };
}

