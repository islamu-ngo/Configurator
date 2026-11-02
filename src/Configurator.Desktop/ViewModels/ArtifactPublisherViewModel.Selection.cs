namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Services;
using Configurator.Presentation;

public sealed partial class ArtifactPublisherViewModel
{
    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public Task<bool> SelectOutputDirectoryAsync(CancellationToken cancellationToken = default) =>
        SelectPathAsync(DesktopPathSelectionKind.OutputDirectory, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanImport))]
    public async Task<bool> SelectImportAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanImport || _form is null || ArtifactId is null) return false;
        DefinitionFormModel form = _form;
        string artifact = ArtifactId;
        bool compare = ImportPurposeIndex != 1;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "selection", cancellationToken);
        if (operation is null) return false;
        long generation = _reviewGeneration;
        _comparingInput = compare;
        BoundedFileInput? input = null;
        try
        {
            StorageSelection<BoundedFileInput> selected = await _dialogs.ReadImportAsync(operation.Token);
            input = selected.Value;
            if (operation.IsCurrent && selected.Status is StorageSelectionStatus.Nonlocal or StorageSelectionStatus.Unavailable)
            {
                _messenger.Send(new StatusChangedMessage(selected.Status == StorageSelectionStatus.Nonlocal
                    ? "selection-nonlocal" : "selection-unavailable", operation.Generation,
                    selected.Status == StorageSelectionStatus.Nonlocal
                        ? DesktopStatusGuidance.SelectionNonlocal : DesktopStatusGuidance.SelectionUnavailable));
                return false;
            }
            if (!operation.IsCurrent || selected.Status != StorageSelectionStatus.Selected || input is null || generation != _reviewGeneration ||
                !ReferenceEquals(form, _form)) return false;
            return await ReviewInputAsync(form, artifact, _ => Task.FromResult(input.Bytes), compare, operation);
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("import-unreadable", operation.Generation, DesktopStatusGuidance.ImportUnreadable));
            return false;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            _messenger.Send(new StatusChangedMessage("selection-unavailable", operation.Generation, DesktopStatusGuidance.SelectionUnavailable));
            return false;
        }
        finally
        {
            _comparingInput = false;
            input?.Dispose();
        }
    }

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
