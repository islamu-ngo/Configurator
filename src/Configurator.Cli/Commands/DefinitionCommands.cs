using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal static class DefinitionCommands
{
    internal static SetupCliFamilyDescriptor Descriptor { get; } = SetupCliCommandRegistry.Family(
        "definition",
        SetupCliCommandRegistry.Operation<DefinitionInitSettings>("init"),
        SetupCliCommandRegistry.Operation<DefinitionSourceSettings>("check"),
        SetupCliCommandRegistry.Operation<DefinitionBuildSettings>("build"),
        SetupCliCommandRegistry.Operation<DefinitionReviseSettings>("revise"));
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
    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Source = Path(Source), Root = Path(Root), Against = Path(Against),
            Detail = Detail, Offset = Offset, Limit = Limit, ExpectedDetailDigest = ExpectedDetailDigest };
}

internal sealed class DefinitionBuildSettings : DefinitionSourceSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }
    [CommandOption("--id-map <PATH>")]
    public string? IdMap { get; set; }
    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Path(Output), IdMap = Path(IdMap) };
}

internal sealed class DefinitionReviseSettings : DefinitionSourceSettings;

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
    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Root = Path(Root),
            Output = Path(Output),
            Format = Format,
            ProjectId = ProjectId,
            ProjectName = Name,
            ApplicationVersion = ApplicationVersion,
            ForkFrom = Path(ForkFrom),
            FromEnvExample = Path(FromEnvExample),
            FromSchema = Path(FromSchema),
            AcceptBoundedSemantics = AcceptBoundedSemantics
        };
}
