using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal interface ISetupCliCommandSettings
{
    SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode);
}

internal abstract class SetupCliCommandSettings : CommandSettings, ISetupCliCommandSettings
{
    [CommandOption("--machine")]
    public bool Machine { get; set; }

    [CommandOption("--text")]
    public bool Text { get; set; }

    public virtual SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        new(family, operation, Machine || invocationMode == SetupCliMode.Machine,
            false, false, null, null, null, null, null, null);

    protected static string? Path(string? value) =>
        value == SetupCliArgumentPreflight.StandardIoSentinel ? "-" : value;
}

internal class DefinitionSettings : SetupCliCommandSettings
{
    [CommandOption("--definition <PATH>")]
    public string? Definition { get; set; }

    [CommandOption("--detail <KIND>")]
    public string? Detail { get; set; }
    [CommandOption("--offset <NUMBER>")]
    public int Offset { get; set; }
    [CommandOption("--limit <NUMBER>")]
    public int Limit { get; set; } = 16;
    [CommandOption("--expected-detail-digest <DIGEST>")]
    public string? ExpectedDetailDigest { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Definition = Path(Definition), Detail = Detail,
            Offset = Offset, Limit = Limit, ExpectedDetailDigest = ExpectedDetailDigest };
}

internal class CatalogueListSettings : DefinitionSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Path(Output), DryRun = DryRun };
}

internal sealed class CatalogueItemSettings : CatalogueListSettings
{
    [CommandOption("--key <ID>")]
    public string? Key { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Key = Key };
}

internal class ArtifactInputSettings : DefinitionSettings
{
    [CommandOption("--artifact <ID>")]
    public string? Artifact { get; set; }

    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }

    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    [CommandOption("--answers <PATH>")]
    public string? Answers { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifact = Artifact, Input = Path(Input), InputRevision = InputRevision, Answers = Path(Answers) };
}

internal class ArtifactOutputSettings : ArtifactInputSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    [CommandOption("--format <FORMAT>")]
    public string? Format { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Output = Path(Output), DryRun = DryRun, Format = Format };
}

internal sealed class ArtifactDiffSettings : ArtifactInputSettings
{
    [CommandOption("--baseline <PATH>")]
    public string? Baseline { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Baseline = Path(Baseline) };
}

internal sealed class ArtifactImportSettings : ArtifactOutputSettings
{
    [CommandOption("--expected-revision <REVISION>")]
    public string? ExpectedRevision { get; set; }

    [CommandOption("--revision-field <ID>")]
    public string? RevisionField { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { ExpectedRevision = ExpectedRevision, RevisionField = RevisionField };
}

internal class BundleInputSettings : DefinitionSettings
{
    [CommandOption("--artifact <ID>")]
    public string[] Artifacts { get; set; } = [];

    [CommandOption("--input <PATH>")]
    public string? Input { get; set; }

    [CommandOption("--input-revision <NUMBER>")]
    public string InputRevision { get; set; } = "0";

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifacts = Artifacts, Input = Path(Input), InputRevision = InputRevision };
}

internal sealed class BundleOutputSettings : BundleInputSettings
{
    [CommandOption("--output <DIRECTORY>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Path(Output), DryRun = DryRun };
}

internal sealed class DoctorSettings : SetupCliCommandSettings;
