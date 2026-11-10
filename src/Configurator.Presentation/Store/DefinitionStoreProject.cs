namespace Configurator.Presentation.Store;

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
    public override string ToString() => $"{nameof(DefinitionStoreProject)}:{ProjectId}";
}
