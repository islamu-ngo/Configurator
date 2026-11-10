namespace Configurator.Presentation.Store;

/// <summary>Discovers curated public projects and opens exact revisions.</summary>
public interface IDefinitionStoreProvider
{
    Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default);
    Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default);
}
