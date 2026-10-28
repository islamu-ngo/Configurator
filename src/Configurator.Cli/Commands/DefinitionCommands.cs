using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal static class DefinitionCommands
{
    internal static SetupCliFamilyDescriptor Descriptor { get; } = SetupCliCommandRegistry.Family(
        "definition",
        SetupCliCommandRegistry.Operation<DefinitionInitSettings>("init"),
        SetupCliCommandRegistry.Operation<DefinitionSourceSettings>("check"),
        SetupCliCommandRegistry.Operation<DefinitionSourceSettings>("lint"),
        SetupCliCommandRegistry.Operation<DefinitionSchemaSettings>("schema"),
        SetupCliCommandRegistry.Operation<DefinitionBuildSettings>("build"),
        SetupCliCommandRegistry.Operation<DefinitionCandidateSettings>("revise"));
}

internal sealed class DefinitionSchemaSettings : SetupCliCommandSettings
{
    [CommandOption("--kind <KIND>")]
    public string Kind { get; set; } = "authoring";

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Kind is not ("authoring" or "runtime" or "answers" or "command") ? "definition-schema-kind" : null);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { SchemaKind = Kind };
}

internal class DefinitionSourceSettings : SetupCliCommandSettings
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

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
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

internal sealed class DefinitionBuildSettings : DefinitionSourceSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }
    [CommandOption("--id-map <PATH>")]
    public string? IdMap { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, IdMap = IdMap };
}

internal sealed class DefinitionInitSettings : SetupCliCommandSettings
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

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
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
