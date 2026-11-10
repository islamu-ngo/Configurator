namespace Configurator.Desktop.Tests;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Presentation.Intake;
using Configurator.Presentation.Store;

internal static class DesktopTestContext
{
    internal static DesktopShellViewModel Root(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>>? save = null,
        string? outputDirectory = null,
        bool? protectedOutputAvailable = null,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null,
        Func<DesktopPathSelectionKind, CancellationToken, Task<Uri?>>? select = null,
        Func<string, Stream>? openDefinition = null, Func<string, Stream>? openImport = null,
        TimeProvider? timeProvider = null, Func<CancellationToken, ValueTask>? cooperativeYield = null,
        IMessenger? messenger = null, IDefinitionRemoteFetcher? remoteFetcher = null,
        IDefinitionStoreProvider? storeProvider = null)
    {
        messenger ??= new WeakReferenceMessenger();
        var dialogs = new Dialogs(select ?? ((_, _) => Task.FromResult<Uri?>(null)));
        cooperativeYield ??= _ => ValueTask.CompletedTask;
        DefinitionLoaderViewModel? loader = null;
        FormWorkspaceViewModel? workspace = null;
        ArtifactPublisherViewModel? publisher = null;
        DesktopImportReviewViewModel? importReview = null;
        try
        {
            loader = Loader(messenger, dialogs, openDefinition, remoteFetcher, storeProvider);
            workspace = new FormWorkspaceViewModel(messenger);
            if (save is null && protectedOutputAvailable is null && publishBundle is null && openImport is null)
            {
                publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                if (outputDirectory is not null) publisher.OutputDirectory = outputDirectory;
            }
            else
            {
                publisher = new ArtifactPublisherViewModel(messenger, dialogs,
                    save ?? ((_, _, _) => Task.FromResult(ProtectedArtifactCommitResult.Installed(true))),
                    outputDirectory ?? Path.GetTempPath(), protectedOutputAvailable ?? true, publishBundle);
            }
            importReview = openImport is null
                ? new DesktopImportReviewViewModel(messenger, dialogs, publisher, cooperativeYield)
                : new DesktopImportReviewViewModel(messenger, dialogs, publisher, openImport, cooperativeYield);
            var root = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview, timeProvider);
            loader = null;
            workspace = null;
            publisher = null;
            importReview = null;
            return root;
        }
        finally
        {
            loader?.Dispose();
            workspace?.Dispose();
            importReview?.Dispose();
            publisher?.Dispose();
        }
    }

    internal static DefinitionLoaderViewModel Loader(IMessenger messenger, IStorageDialogService dialogs,
        Func<string, Stream>? openDefinition = null, IDefinitionRemoteFetcher? remoteFetcher = null,
        IDefinitionStoreProvider? storeProvider = null)
    {
        IDefinitionRemoteFetcher fetcher = remoteFetcher ?? EmptyRemoteFetcher.Instance;
        IDefinitionStoreProvider provider = storeProvider ?? EmptyStoreProvider.Instance;
        return openDefinition is null
            ? new DefinitionLoaderViewModel(messenger, dialogs, fetcher, provider)
            : new DefinitionLoaderViewModel(messenger, dialogs, openDefinition, fetcher, provider);
    }

    private sealed class EmptyRemoteFetcher : IDefinitionRemoteFetcher
    {
        internal static EmptyRemoteFetcher Instance { get; } = new();
        public Task<DefinitionRemoteResult> FetchAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<DefinitionRemoteResult>(new DefinitionRemoteResult.InvalidUri());
        public void Dispose() { }
    }

    private sealed class EmptyStoreProvider : IDefinitionStoreProvider
    {
        internal static EmptyStoreProvider Instance { get; } = new();
        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([]);
        public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default) =>
            Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.ProjectNotFound());
    }

    private sealed class Dialogs(
        Func<DesktopPathSelectionKind, CancellationToken, Task<Uri?>> select) : IStorageDialogService
    {
        public async Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default)
        {
            Uri? value = await select(kind, cancellationToken);
            return new(value is null ? StorageSelectionStatus.Cancelled : StorageSelectionStatus.Selected, value);
        }

        public async Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default)
        {
            Uri? selected = await select(DesktopPathSelectionKind.Import, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (selected is null) return new(StorageSelectionStatus.Cancelled);
            if (!AvaloniaStorageDialogService.TryGetLocalPath(selected, out string path))
                return new(StorageSelectionStatus.Nonlocal);
            return new(StorageSelectionStatus.Selected,
                await BoundedFileInput.ReadAsync(path, ProtectedArtifactWriter.MaximumBytes, cancellationToken));
        }
    }
}
