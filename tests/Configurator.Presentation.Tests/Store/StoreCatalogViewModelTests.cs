namespace Configurator.Presentation.Tests.Store;

using Configurator.Presentation.Store;

public sealed class StoreCatalogViewModelTests
{
    [Test]
    public async Task LoadsProjectCardsWithLatestRevision()
    {
        var projects = SampleProjects();
        using var catalog = new StoreCatalogViewModel(new FakeProvider(projects));

        await Assert.That(await catalog.LoadAsync(CancellationToken.None)).IsTrue();

        await Assert.That(catalog.Projects.Count).IsEqualTo(2);
        await Assert.That(catalog.Projects[0].Name).IsEqualTo("Zulu");
        await Assert.That(catalog.Projects[1].Name).IsEqualTo("Alpha");
        await Assert.That(catalog.Projects[0].LatestRevision).IsEqualTo(2);
        await Assert.That(catalog.Projects[0].ProjectId).IsEqualTo("zulu");
        await Assert.That(catalog.Projects[0].Project).IsSameReferenceAs(projects[0]);
        await Assert.That(catalog.HasNoMatches).IsFalse();
        await Assert.That(catalog.LoadFailed).IsFalse();
    }

    [Test]
    public async Task SearchMatchesEveryTermAcrossDiscoveryMetadata()
    {
        using var catalog = new StoreCatalogViewModel(new FakeProvider(SampleProjects()));
        await catalog.LoadAsync(CancellationToken.None);

        catalog.SearchQuery = "OFFLINE islamu.core";

        await Assert.That(catalog.Projects.Single().ProjectId).IsEqualTo("zulu");
        catalog.SearchQuery = "alpha";
        await Assert.That(catalog.Projects.Single().ProjectId).IsEqualTo("alpha");
        catalog.SearchQuery = "workflow";
        await Assert.That(catalog.Projects.Single().ProjectId).IsEqualTo("alpha");
        catalog.SearchQuery = "missing";
        await Assert.That(catalog.Projects).IsEmpty();
        await Assert.That(catalog.HasNoMatches).IsTrue();
        catalog.SearchQuery = string.Empty;
        await Assert.That(catalog.Projects.Count).IsEqualTo(2);
        await Assert.That(catalog.HasNoMatches).IsFalse();
    }

    [Test]
    public async Task ProviderFailureProducesValueSafeEmptyState()
    {
        using var catalog = new StoreCatalogViewModel(new ThrowingProvider());

        await Assert.That(await catalog.LoadAsync(CancellationToken.None)).IsFalse();

        await Assert.That(catalog.Projects).IsEmpty();
        await Assert.That(catalog.LoadFailed).IsTrue();
        await Assert.That(catalog.HasNoMatches).IsFalse();
    }

    [Test]
    public async Task DisposalFencesLateCatalogueResults()
    {
        var provider = new PendingProvider();
        var catalog = new StoreCatalogViewModel(provider);
        var loading = catalog.LoadAsync(CancellationToken.None);
        catalog.Dispose();
        provider.Complete(SampleProjects());

        await Assert.That(await loading).IsFalse();
        await Assert.That(catalog.Projects).IsEmpty();
    }

    [Test]
    public async Task NullSearchQueryAndSynchronizationContextArePreservedSafe()
    {
        var capturedContext = new SingleThreadSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(capturedContext);
        try
        {
            var provider = new PendingProvider();
            using var catalog = new StoreCatalogViewModel(provider);
            int collectionUpdatesOnContext = 0;
            catalog.Projects.CollectionChanged += (_, _) =>
            {
                if (ReferenceEquals(SynchronizationContext.Current, capturedContext))
                    collectionUpdatesOnContext++;
            };

            Task<bool> loading = catalog.LoadAsync(CancellationToken.None);
            await Assert.That(catalog.IsLoading).IsTrue();
            provider.Complete(SampleProjects());

            await Assert.That(await loading).IsTrue();
            await Assert.That(catalog.IsLoading).IsFalse();
            await Assert.That(collectionUpdatesOnContext).IsGreaterThan(0);

            catalog.SearchQuery = null!;
            await Assert.That(catalog.Projects.Count).IsEqualTo(2);
            await Assert.That(catalog.Projects[0].TagsDisplay).IsEqualTo("server");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            ThreadPool.QueueUserWorkItem(_ =>
            {
                SynchronizationContext? previous = Current;
                SetSynchronizationContext(this);
                try { d(state); }
                finally { SetSynchronizationContext(previous); }
            });
    }

    private static DefinitionStoreProject[] SampleProjects() =>
    [
        Project("zulu", "Zulu", "Offline setup", "islamu.core", ["server"], 2),
        Project("alpha", "Alpha", "Local preparation", "islamu.workflow", ["workflow"], 1),
    ];

    private static DefinitionStoreProject Project(
        string id, string name, string description, string applicationId, string[] tags, long latestRevision) => new()
        {
            ProjectId = id,
            Name = name,
            Description = description,
            ApplicationId = applicationId,
            FamilyId = Guid.NewGuid(),
            Tags = tags,
            Revisions = [new DefinitionStoreRevision { Revision = latestRevision, Digest = "sha256:test" }],
        };

    private sealed class FakeProvider(IReadOnlyList<DefinitionStoreProject> projects) : IDefinitionStoreProvider
    {
        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(projects);

        public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingProvider : IDefinitionStoreProvider
    {
        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken) =>
            throw new DefinitionStoreException("private canary must not surface");

        public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class PendingProvider : IDefinitionStoreProvider
    {
        private readonly TaskCompletionSource<IReadOnlyList<DefinitionStoreProject>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken) => _completion.Task;

        public void Complete(IReadOnlyList<DefinitionStoreProject> projects) => _completion.SetResult(projects);

        public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
