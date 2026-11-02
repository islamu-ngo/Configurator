using Spectre.Console.Cli;

namespace Configurator.Cli.Commands;

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

    public override CliCommand Bind(string family, string operation, CliMode invocationMode) =>
        base.Bind(family, operation, invocationMode) with
        { ExpectedRevision = ExpectedRevision, RevisionField = RevisionField };
}

