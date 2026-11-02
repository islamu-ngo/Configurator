using Configurator.Core.Definitions;
using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal class SecretSettings : CliCommandSettings
{
    [CommandOption("--definition <PATH>")]
    public string? Definition { get; set; }
    [CommandOption("--pointer <POINTER>")]
    public string? Pointer { get; set; }
    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Definition) ? "definition-required" : null)
        ?? (string.IsNullOrEmpty(Pointer) ? "pointer-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null)
        ?? (string.IsNullOrEmpty(Output) ? "output-required" : null);

    protected override string? ValidateValues()
    {
        string? error = base.ValidateValues();
        if (error is not null) return error;
        if (Definition == "-") return "definition-local-file-required";
        if (Output == "-") return "restricted-stdout";
        try { _ = DefinitionPath.Parse(Pointer!); }
        catch (DefinitionException) { return "pointer-invalid"; }
        return null;
    }

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Definition = Definition, Pointer = Pointer, Input = Input, Output = Output };
}

