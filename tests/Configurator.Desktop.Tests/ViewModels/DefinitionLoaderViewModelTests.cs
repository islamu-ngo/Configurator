namespace Configurator.Desktop.Tests.ViewModels;

using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;
using Configurator.Presentation.Intake;
using Configurator.Presentation.Store;

public sealed class DefinitionLoaderViewModelTests
{
    [Test]
    [Arguments("")]
    [Arguments(" \t ")]
    public async Task EmptyPathHasSpecificGuidanceAndNeverOpensAFile(string path)
    {
        int reads = 0;
        using var shell = DesktopTestContext.Root(openDefinition: _ =>
        {
            reads++;
            throw new IOException("No file input is expected.");
        });
        shell.Loader.DefinitionPath = path;

        bool accepted = await shell.Loader.LoadDefinitionAsync();

        await Assert.That(accepted).IsFalse();
        await Assert.That(reads).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo("path-empty");
        await Assert.That(shell.Status).DoesNotContain("source-format-invalid");
        await Assert.That(shell.StatusHelp.Contains("path", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task RemoteDefinitionUsesSharedAdmissionAndOpensConfigureStage()
    {
        Definition definition = DefinitionSourceCodec.ParseJson(DesktopApplicationTests.Definition());
        using var remote = new FakeRemoteFetcher((_, _) => Task.FromResult<DefinitionRemoteResult>(
            new DefinitionRemoteResult.Success(definition)));
        using var shell = DesktopTestContext.Root(remoteFetcher: remote);
        shell.Loader.DefinitionUrl = "https://public.example/definition.json";

        bool accepted = await shell.Loader.FetchDefinitionAsync();

        await Assert.That(accepted).IsTrue();
        await Assert.That(remote.RequestedUrl).IsEqualTo("https://public.example/definition.json");
        await Assert.That(shell.Workspace.Form).IsNotNull();
        await Assert.That(shell.Workspace.Form!.Definition).IsSameReferenceAs(definition);
        await Assert.That(shell.Navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Configure);
        await Assert.That(shell.Status).IsEqualTo("definition-loaded");
    }

    [Test]
    public async Task StoreProjectOpensItsAdvertisedLatestRevision()
    {
        Definition definition = DefinitionSourceCodec.ParseJson(DesktopApplicationTests.Definition(7));
        var provider = new FakeStoreProvider(new DefinitionStoreResult.Success(definition));
        using var shell = DesktopTestContext.Root(storeProvider: provider);
        await shell.Loader.LoadStoreAsync();
        StoreProjectCardViewModel card = shell.Loader.StoreCatalog.Projects.Single();

        bool accepted = await shell.Loader.ConfigureStoreProjectAsync(card);

        await Assert.That(accepted).IsTrue();
        await Assert.That(provider.OpenedProjectId).IsEqualTo("example");
        await Assert.That(provider.OpenedRevision).IsEqualTo(7);
        await Assert.That(shell.Workspace.Form!.Definition).IsSameReferenceAs(definition);
        await Assert.That(shell.Navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Configure);
    }

    [Test]
    public async Task DesktopSearchFiltersTheSharedCatalogueImmediately()
    {
        using var shell = DesktopTestContext.Root(storeProvider: new FakeStoreProvider(null));
        await shell.Loader.LoadStoreAsync();

        shell.Loader.StoreSearchQuery = "QUEUE";

        await Assert.That(shell.Loader.StoreCatalog.Projects).IsEmpty();
        await Assert.That(shell.Loader.StoreCatalog.HasNoMatches).IsTrue();
        shell.Loader.StoreSearchQuery = "example";
        await Assert.That(shell.Loader.StoreCatalog.Projects.Single().ProjectId).IsEqualTo("example");
    }

    [Test]
    public async Task SupersededRemoteResultCannotReplaceTheCurrentWorkspace()
    {
        var completion = new TaskCompletionSource<DefinitionRemoteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var remote = new FakeRemoteFetcher((_, _) => completion.Task);
        using var shell = DesktopTestContext.Root(remoteFetcher: remote);
        shell.Loader.DefinitionUrl = "https://public.example/slow.json";
        Task<bool> fetching = shell.Loader.FetchDefinitionAsync();
        await remote.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2));
        completion.SetResult(new DefinitionRemoteResult.Success(
            DefinitionSourceCodec.ParseJson(DesktopApplicationTests.Definition(3))));

        await Assert.That(await fetching).IsFalse();
        await Assert.That(shell.Workspace.Form!.Definition.Revision).IsEqualTo(2);
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task RemoteFailuresExposeStableValueSafeStatuses()
    {
        using var remote = new FakeRemoteFetcher((_, _) => Task.FromResult<DefinitionRemoteResult>(
            new DefinitionRemoteResult.BlockedHost()));
        using var shell = DesktopTestContext.Root(remoteFetcher: remote);
        shell.Loader.DefinitionUrl = "https://private.example/definition.json";

        await Assert.That(await shell.Loader.FetchDefinitionAsync()).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("remote-host-blocked");
        await Assert.That(shell.Status).DoesNotContain("private.example");
        await Assert.That(shell.Workspace.Form).IsNull();
    }

    [Test]
    public async Task EmptyRemoteUrlFocusesInputWithoutNetworkCall()
    {
        int calls = 0;
        using var remote = new FakeRemoteFetcher((_, _) =>
        {
            calls++;
            return Task.FromResult<DefinitionRemoteResult>(new DefinitionRemoteResult.InvalidUri());
        });
        using var shell = DesktopTestContext.Root(remoteFetcher: remote);
        shell.Loader.DefinitionUrl = "   ";

        bool accepted = await shell.Loader.FetchDefinitionAsync();

        await Assert.That(accepted).IsFalse();
        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo("remote-uri-invalid");
        await Assert.That(shell.FocusTarget).IsEqualTo("DefinitionUrl");
    }

    [Test]
    [Arguments("invalid-uri", "remote-uri-invalid")]
    [Arguments("blocked", "remote-host-blocked")]
    [Arguments("too-large", "remote-payload-limit")]
    [Arguments("network", "remote-fetch-failed")]
    [Arguments("parse", "remote-definition-invalid")]
    public async Task EveryRemoteResultVariantMapsToDistinctValueSafeStatus(string kind, string expectedStatus)
    {
        DefinitionRemoteResult outcome = kind switch
        {
            "invalid-uri" => new DefinitionRemoteResult.InvalidUri(),
            "blocked" => new DefinitionRemoteResult.BlockedHost(),
            "too-large" => new DefinitionRemoteResult.PayloadTooLarge(),
            "network" => new DefinitionRemoteResult.NetworkError(),
            "parse" => new DefinitionRemoteResult.ParseError(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        using var remote = new FakeRemoteFetcher((_, _) => Task.FromResult(outcome));
        using var shell = DesktopTestContext.Root(remoteFetcher: remote);
        shell.Loader.DefinitionUrl = "https://public.example/definition.json";

        bool accepted = await shell.Loader.FetchDefinitionAsync();

        await Assert.That(accepted).IsFalse();
        await Assert.That(shell.Status).IsEqualTo(expectedStatus);
        await Assert.That(string.IsNullOrWhiteSpace(shell.StatusHelp)).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNull();
    }

    [Test]
    [Arguments("missing-project", "store-project-not-found")]
    [Arguments("missing-revision", "store-revision-not-found")]
    [Arguments("mismatch", "store-snapshot-mismatch")]
    [Arguments("corrupt", "store-asset-corrupt")]
    public async Task StoreOpenFailuresExposeValueSafeStatuses(string kind, string expectedStatus)
    {
        DefinitionStoreResult outcome = kind switch
        {
            "missing-project" => new DefinitionStoreResult.ProjectNotFound(),
            "missing-revision" => new DefinitionStoreResult.RevisionNotFound(),
            "mismatch" => new DefinitionStoreResult.SnapshotMismatch(),
            "corrupt" => new DefinitionStoreResult.CorruptAsset(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        using var shell = DesktopTestContext.Root(storeProvider: new FakeStoreProvider(outcome));
        await shell.Loader.LoadStoreAsync();
        StoreProjectCardViewModel card = shell.Loader.StoreCatalog.Projects.Single();

        bool accepted = await shell.Loader.ConfigureStoreProjectAsync(card);

        await Assert.That(accepted).IsFalse();
        await Assert.That(shell.Status).IsEqualTo(expectedStatus);
        await Assert.That(shell.Navigation.Stage).IsEqualTo(DesktopWorkspaceStage.Open);
    }

    private static DefinitionStoreProject Project() => new()
    {
        ProjectId = "example",
        Name = "Example project",
        Description = "Offline service setup",
        ApplicationId = "example.service",
        FamilyId = Guid.Parse("69cdb122-41d2-4fab-9b82-7929a889c726"),
        Tags = ["service"],
        Revisions = [new DefinitionStoreRevision { Revision = 7, Digest = "sha256:test" }],
    };

    private sealed class FakeRemoteFetcher(
        Func<string, CancellationToken, Task<DefinitionRemoteResult>> fetch) : IDefinitionRemoteFetcher
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? RequestedUrl { get; private set; }

        public Task<DefinitionRemoteResult> FetchAsync(string url, CancellationToken cancellationToken = default)
        {
            RequestedUrl = url;
            Started.TrySetResult();
            return fetch(url, cancellationToken);
        }

        public void Dispose() { }
    }

    private sealed class FakeStoreProvider(DefinitionStoreResult? openResult) : IDefinitionStoreProvider
    {
        public string? OpenedProjectId { get; private set; }
        public long OpenedRevision { get; private set; }

        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([Project()]);

        public Task<DefinitionStoreResult> OpenAsync(
            string projectId, long revision, CancellationToken cancellationToken = default)
        {
            OpenedProjectId = projectId;
            OpenedRevision = revision;
            return Task.FromResult(openResult ?? new DefinitionStoreResult.RevisionNotFound());
        }
    }
}
