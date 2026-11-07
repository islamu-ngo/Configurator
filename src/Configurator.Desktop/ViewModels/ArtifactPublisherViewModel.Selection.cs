namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Services;

public sealed partial class ArtifactPublisherViewModel
{
    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public Task<bool> SelectOutputDirectoryAsync(CancellationToken cancellationToken = default) =>
        SelectPathAsync(DesktopPathSelectionKind.OutputDirectory, cancellationToken);

    private async Task<bool> SelectPathAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "selection", cancellationToken);
        if (operation is null) return false;
        bool accepted = false;
        long focusGeneration = operation.Generation;
        try
        {
            StorageSelection<Uri> selected = await _dialogs.SelectAsync(kind, operation.Token);
            if (!operation.IsCurrent || selected.Status == StorageSelectionStatus.Cancelled) return false;
            if (selected.Status == StorageSelectionStatus.Unavailable)
            {
                _messenger.Send(new StatusChangedMessage("selection-unavailable", operation.Generation, DesktopStatusGuidance.SelectionUnavailable));
                return false;
            }
            if (selected.Status != StorageSelectionStatus.Selected || selected.Value is null ||
                !AvaloniaStorageDialogService.TryGetLocalPath(selected.Value, out string path))
            {
                _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation, DesktopStatusGuidance.SelectionNonlocal));
                return false;
            }
            operation.Dispose();
            OutputDirectory = path;
            accepted = true;
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation, DesktopStatusGuidance.SelectionNonlocal));
            return false;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or
            UnauthorizedAccessException or InvalidOperationException)
        {
            _messenger.Send(new StatusChangedMessage("selection-unavailable", operation.Generation, DesktopStatusGuidance.SelectionUnavailable));
            return false;
        }
        finally
        {
            operation.Dispose();
            if (!accepted && !_disposed && focusGeneration == _generation)
                _messenger.Send(new FocusRequestedMessage("SelectOutput"));
        }
    }

}
