using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

internal interface ISetupCliCommandSettings
{
    string? ValidateInput();
    SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode);
}

internal abstract class SetupCliCommandSettings : CommandSettings, ISetupCliCommandSettings
{
    [CommandOption("--machine")]
    public bool Machine { get; set; }

    [CommandOption("--text")]
    public bool Text { get; set; }

    public string? ValidateInput() => ValidateRequired() ?? ValidateValues();

    protected virtual string? ValidateRequired() => null;

    protected virtual string? ValidateValues() => Machine && Text ? "mode-conflict" : null;

    public virtual SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        new(family, operation, Machine || invocationMode == SetupCliMode.Machine,
            false, false, null, null, null, null, null, null);

    protected string? ValidateOutput(string? output, bool required = false) =>
        required && string.IsNullOrEmpty(output) ? "output-required"
            : Machine && output == "-" ? "machine-artifact-stdout" : null;

    protected string? ValidateDetail(string? detail, int offset, int limit, string? expectedDigest) =>
        detail is not null && (detail is not ("diagnostics" or "coverage") || !Machine) ? "detail-invalid"
            : offset is < 0 or > 2048 ? "detail-offset-invalid"
            : limit is < 1 or > 32 ? "detail-limit-invalid"
            : expectedDigest is not null && !IsDigest(expectedDigest) ? "detail-digest-invalid" : null;

    protected static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_');

    protected static bool IsDigest(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    protected static string? ValidateInputRevision(string revision) =>
        long.TryParse(revision, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
            out long number) && number >= 0 ? null : "input-revision-invalid";

    protected static string? ValidateStdin(params string?[] paths) =>
        paths.Count(path => path == "-") > 1 ? "stdin-conflict" : null;
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

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Definition) ? "definition-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Definition == "-" ? "definition-local-file-required" : null)
        ?? ValidateDetail(Detail, Offset, Limit, ExpectedDetailDigest);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        {
            Definition = Definition,
            Detail = Detail,
            Offset = Offset,
            Limit = Limit,
            ExpectedDetailDigest = ExpectedDetailDigest
        };
}

internal class CatalogueListSettings : DefinitionSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, DryRun = DryRun };
}

internal sealed class CatalogueItemSettings : CatalogueListSettings
{
    [CommandOption("--key <ID>")]
    public string? Key { get; set; }

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Key) ? "key-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Key is not null && !IsIdentifier(Key) ? "catalogue-key-invalid" : null);

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

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(Artifact) ? "artifact-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Artifact is not null && !IsIdentifier(Artifact) ? "identifier-invalid" : null)
        ?? ValidateInputRevision(InputRevision) ?? ValidateStdin(Input, Answers);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifact = Artifact, Input = Input, InputRevision = InputRevision, Answers = Answers };
}

internal class ArtifactOutputSettings : ArtifactInputSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    [CommandOption("--format <FORMAT>")]
    public string? Format { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun)
        ?? (Format is not null && Format is not ("json" or "yaml" or "dotenv" or "markdown") ? "format-not-supported" : null);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Output = Output, DryRun = DryRun, Format = Format };
}

internal sealed class ArtifactDiffSettings : ArtifactInputSettings
{
    [CommandOption("--baseline <PATH>")]
    public string? Baseline { get; set; }

    protected override string? ValidateRequired() =>
        base.ValidateRequired() ?? (string.IsNullOrEmpty(Baseline) ? "baseline-required" : null);

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateStdin(Input, Baseline, Answers);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Baseline = Baseline };
}

internal sealed class ArtifactImportSettings : ArtifactOutputSettings
{
    [CommandOption("--expected-revision <REVISION>")]
    public string? ExpectedRevision { get; set; }

    [CommandOption("--revision-field <ID>")]
    public string? RevisionField { get; set; }

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (string.IsNullOrEmpty(RevisionField) ? "revision-field-required" : null)
        ?? (string.IsNullOrEmpty(ExpectedRevision) ? "revision-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (RevisionField is not null && !IsIdentifier(RevisionField) ? "identifier-invalid" : null)
        ?? (ExpectedRevision is not null && ExpectedRevision != "absent" && !IsDigest(ExpectedRevision) ? "revision-required" : null);

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

    protected override string? ValidateRequired() => base.ValidateRequired()
        ?? (Artifacts.Length == 0 ? "artifact-required" : null)
        ?? (string.IsNullOrEmpty(Input) ? "input-required" : null);

    protected override string? ValidateValues() => base.ValidateValues()
        ?? (Artifacts.Any(artifact => !IsIdentifier(artifact)) ? "identifier-invalid" : null)
        ?? (Artifacts.Length > 64 || Artifacts.Distinct(StringComparer.Ordinal).Count() != Artifacts.Length
            ? "artifact-selection-invalid" : null)
        ?? ValidateInputRevision(InputRevision);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { Artifacts = Artifacts, Input = Input, InputRevision = InputRevision };
}

internal sealed class BundleOutputSettings : BundleInputSettings
{
    [CommandOption("--output <DIRECTORY>")]
    public string? Output { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output, !DryRun);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output, DryRun = DryRun };
}

internal sealed class DoctorSettings : SetupCliCommandSettings;

internal sealed class DefinitionCandidateSettings : DefinitionSourceSettings
{
    [CommandOption("--output <PATH>")]
    public string? Output { get; set; }

    protected override string? ValidateValues() => base.ValidateValues() ?? ValidateOutput(Output);

    public override SetupCliCommand Bind(string family, string operation, SetupCliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with { Output = Output };
}
