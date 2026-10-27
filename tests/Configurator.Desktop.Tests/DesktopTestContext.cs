namespace Configurator.Desktop.Tests;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;

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
        TimeProvider? timeProvider = null)
    {
        var messenger = new WeakReferenceMessenger();
        var dialogs = new Dialogs(select ?? ((_, _) => Task.FromResult<Uri?>(null)));
        DefinitionLoaderViewModel? loader = null;
        FormWorkspaceViewModel? workspace = null;
        ArtifactPublisherViewModel? publisher = null;
        try
        {
            loader = openDefinition is null ? new DefinitionLoaderViewModel(messenger, dialogs)
                : new DefinitionLoaderViewModel(messenger, dialogs, openDefinition);
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
                    outputDirectory ?? Path.GetTempPath(), protectedOutputAvailable ?? true, publishBundle, openImport);
            }
            var root = new DesktopShellViewModel(messenger, loader, workspace, publisher, timeProvider);
            loader = null;
            workspace = null;
            publisher = null;
            return root;
        }
        finally
        {
            loader?.Dispose();
            workspace?.Dispose();
            publisher?.Dispose();
        }
    }

    private sealed class Dialogs(
        Func<DesktopPathSelectionKind, CancellationToken, Task<Uri?>> select) : IStorageDialogService
    {
        public Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            select(kind, cancellationToken);
    }
}
