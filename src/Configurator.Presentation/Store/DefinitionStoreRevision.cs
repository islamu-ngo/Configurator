namespace Configurator.Presentation.Store;

/// <summary>Canonical identity and discovery metadata for one store revision.</summary>
public sealed record DefinitionStoreRevision
{
    public long Revision { get; init; }
    public string Digest { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> ApplicationVersions { get; init; } = [];
    public IReadOnlyList<string> EntryPoints { get; init; } = [];
    public IReadOnlyList<string> Locales { get; init; } = [];
    public IReadOnlyList<string> OutputNames { get; init; } = [];
    public string SnapshotPath { get; init; } = string.Empty;
    public override string ToString() => $"{nameof(DefinitionStoreRevision)}:{Revision}:{Digest}";
}
