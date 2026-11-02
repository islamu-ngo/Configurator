using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal sealed class DefinitionInitSettings : CliCommandSettings
{
    [CommandOption("--root <DIRECTORY>")]
    public string? Root { get; set; }
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }
    [CommandOption("--format <FORMAT>")]
    public string? Format { get; set; }
    [CommandOption("--project-id <ID>")]
    public string? ProjectId { get; set; }
    [CommandOption("--name <NAME>")]
    public string? Name { get; set; }
    [CommandOption("--application-version <VERSION>")]
    public string? ApplicationVersion { get; set; }
    [CommandOption("--fork-from <PATH>")]
    public string? ForkFrom { get; set; }
    [CommandOption("--from-env-example <PATH>")]
    public string? FromEnvExample { get; set; }
    [CommandOption("--from-schema <PATH>")]
    public string? FromSchema { get; set; }
    [CommandOption("--accept-bounded-semantics")]
    public bool AcceptBoundedSemantics { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (ForkFrom is not null
            ? string.IsNullOrEmpty(Output) || FromSchema is not null || FromEnvExample is not null ? "definition-fork-options" : null
            : string.IsNullOrEmpty(ProjectId) || string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(ApplicationVersion)
                ? "definition-project-metadata-required" : null);

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output)
        ?? (FromSchema is not null && FromEnvExample is not null ? "definition-scaffold-conflict" : null)
        ?? (AcceptBoundedSemantics && FromSchema is null ? "definition-schema-required" : null)
        ?? (Format is not null && Format is not ("yaml" or "json") ? "definition-source-format" : null)
        ?? (Format is not null && Output is not null && IsYaml(Output) != (Format == "yaml") ? "definition-source-format" : null)
        ?? (ForkFrom is not null && Output is not null && IsYaml(ForkFrom) != IsYaml(Output) ? "definition-source-format" : null)
        ?? (ForkFrom is not null && Format is not null && IsYaml(ForkFrom) != (Format == "yaml") ? "definition-source-format" : null);

    private static bool IsYaml(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Root = Root,
            Output = Output,
            Format = Format,
            ProjectId = ProjectId,
            ProjectName = Name,
            ApplicationVersion = ApplicationVersion,
            ForkFrom = ForkFrom,
            FromEnvExample = FromEnvExample,
            FromSchema = FromSchema,
            AcceptBoundedSemantics = AcceptBoundedSemantics
        };
}
