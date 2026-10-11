namespace Configurator.Desktop.Tests;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;

internal static class DesktopTestContext
{
    internal static DesktopShellViewModel Root(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactStatus>>? save = null,
        string? outputDirectory = null,
        bool? protectedOutputAvailable = null,
        Func<IReadOnlyList<DefinitionArtifact>, string, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null,
        Func<DesktopPathSelectionKind, CancellationToken, Task<Uri?>>? select = null)
    {
        var messenger = new WeakReferenceMessenger();
        var dialogs = new Dialogs(select ?? ((_, _) => Task.FromResult<Uri?>(null)));
        DefinitionLoaderViewModel? loader = null;
        FormWorkspaceViewModel? workspace = null;
        ArtifactPublisherViewModel? publisher = null;
        try
        {
            loader = new DefinitionLoaderViewModel(messenger, dialogs);
            workspace = new FormWorkspaceViewModel(messenger);
            if (save is null && protectedOutputAvailable is null && publishBundle is null)
            {
                publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                if (outputDirectory is not null) publisher.OutputDirectory = outputDirectory;
            }
            else
            {
                publisher = new ArtifactPublisherViewModel(messenger, dialogs,
                    save ?? ((_, _, _) => Task.FromResult(ProtectedArtifactStatus.Written)),
                    outputDirectory ?? Path.GetTempPath(), protectedOutputAvailable ?? true, publishBundle);
            }
            var root = new DesktopShellViewModel(messenger, loader, workspace, publisher);
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
