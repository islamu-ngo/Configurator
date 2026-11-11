namespace Configurator.Browser.Tests;

using Configurator.Core.Definitions;
using Configurator.Presentation.Store;

internal sealed class TestDefinitionStoreProvider : IDefinitionStoreProvider
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<DefinitionStoreProject>>> _load;
    private readonly Func<string, long, CancellationToken, Task<DefinitionStoreResult>> _open;

    public TestDefinitionStoreProvider(
        Func<CancellationToken, Task<IReadOnlyList<DefinitionStoreProject>>>? load = null,
        Func<string, long, CancellationToken, Task<DefinitionStoreResult>>? open = null)
    {
        _load = load ?? (_ => Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([]));
        _open = open ?? ((_, _, _) => Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.ProjectNotFound()));
    }

    public static TestDefinitionStoreProvider FromIndex(
        IReadOnlyList<DefinitionStoreProject> projects,
        params (string ProjectId, long Revision, Definition Definition)[] definitions)
    {
        Dictionary<(string ProjectId, long Revision), Definition> lookup = [];
        foreach ((string projectId, long revision, Definition definition) in definitions)
            lookup[(projectId, revision)] = definition;

        return new TestDefinitionStoreProvider(
            _ => Task.FromResult(projects),
            (projectId, revision, _) =>
            {
                DefinitionStoreProject? project = projects.SingleOrDefault(item => item.ProjectId == projectId);
                if (project is null)
                    return Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.ProjectNotFound());

                DefinitionStoreRevision? selected = project.Revisions.SingleOrDefault(item => item.Revision == revision);
                if (selected is null)
                    return Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.RevisionNotFound());

                if (!lookup.TryGetValue((projectId, revision), out Definition? definition))
                    return Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.CorruptAsset());

                return Task.FromResult<DefinitionStoreResult>(
                    DefinitionStoreIndexParser.MatchesDefinition(project, selected, definition)
                        ? new DefinitionStoreResult.Success(definition)
                        : new DefinitionStoreResult.SnapshotMismatch());
            });
    }

    public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default) =>
        _load(cancellationToken);

    public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default) =>
        _open(projectId, revision, cancellationToken);
}
