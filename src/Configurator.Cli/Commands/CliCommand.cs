namespace Configurator.Cli;


internal sealed record CliCommand(
    string Family, string Operation, bool Machine, bool DryRun, bool Help,
    string? Definition, string? Artifact, string? Input, string? Baseline,
    string? Output, string? Key)
{
    internal string? Format { get; init; }
    internal string InputRevision { get; init; } = "0";
    internal string? ExpectedRevision { get; init; }
    internal string? RevisionField { get; init; }
    internal string? Answers { get; init; }
    internal string[]? Artifacts { get; init; }
    internal string? Detail { get; init; }
    internal int Offset { get; init; }
    internal int Limit { get; init; } = 16;
    internal string? ExpectedDetailDigest { get; init; }
    internal string? Source { get; init; }
    internal string? Root { get; init; }
    internal string? Against { get; init; }
    internal string? IdMap { get; init; }
    internal string? ProjectId { get; init; }
    internal string? ProjectName { get; init; }
    internal string? ApplicationVersion { get; init; }
    internal string? ForkFrom { get; init; }
    internal string? FromEnvExample { get; init; }
    internal string? FromSchema { get; init; }
    internal bool AcceptBoundedSemantics { get; init; }
    internal bool Quality { get; init; }
    internal string SchemaKind { get; init; } = "authoring";
    internal string? Pointer { get; init; }
    internal string? SecretInput { get; init; }
    internal string? GeneratedPasswordOutput { get; init; }
}

