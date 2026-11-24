namespace Configurator.Presentation.Store;

/// <summary>Auditable bounded conformance evidence attached to a store project's latest admitted revision.</summary>
public sealed record DefinitionStoreConformance
{
    public string EvidenceKind { get; init; } = "format-only";
    public string DefinitionDigest { get; init; } = string.Empty;
    public string? FixtureDigest { get; init; }
    public string EngineVersion { get; init; } = string.Empty;
    public string RecordedAt { get; init; } = string.Empty;
    public string ClaimBoundary { get; init; } = string.Empty;
    public override string ToString() => $"{nameof(DefinitionStoreConformance)}:{EvidenceKind}";
}

/// <summary>Public metadata for one curated definition family.</summary>
public sealed record DefinitionStoreProject
{
    public string ProjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SourceRepository { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string ApplicationId { get; init; } = string.Empty;
    public Guid FamilyId { get; init; }
    public IReadOnlyList<DefinitionStoreRevision> Revisions { get; init; } = [];
    public string? IconDataUri { get; init; }
    public string? IconLicense { get; init; }
    public string? IconAttribution { get; init; }
    public DefinitionStoreConformance? Conformance { get; init; }
    public override string ToString() => $"{nameof(DefinitionStoreProject)}:{ProjectId}";
}
