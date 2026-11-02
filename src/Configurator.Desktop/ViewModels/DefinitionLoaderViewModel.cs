namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;

/// <summary>Admits bounded local definition bytes before creating an editable workspace.</summary>
public sealed partial class DefinitionLoaderViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, Stream> _openRead;
    private Definition? _definition;
    private string? _locale;
    private long _generation;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadDefinitionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectDefinitionCommand))]
    private bool _isBusy;
    [ObservableProperty]
    private string _definitionPath = string.Empty;

    public DefinitionLoaderViewModel(IMessenger messenger, IStorageDialogService dialogs)
        : this(messenger, dialogs, path => new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous))
    {
    }

    internal DefinitionLoaderViewModel(IMessenger messenger, IStorageDialogService dialogs,
        Func<string, Stream> openRead)
    {
        _messenger = messenger;
        _dialogs = dialogs;
        _openRead = openRead;
        messenger.Register<DefinitionLoaderViewModel, OperationStateMessage>(this,
            static (loader, message) => loader.IsBusy = message.IsBusy);
        messenger.Register<DefinitionLoaderViewModel, WorkspaceInvalidatedMessage>(this,
            static (loader, message) => loader._generation = message.Generation);
    }

    public bool CanSelectPath => !_disposed && !IsBusy;
    public string DefinitionPin => _definition is null ? new DesktopStrings(_locale).Read("NoDefinition") :
        new DesktopStrings(_locale).Format("DefinitionPinFormat", _definition.Title, _definition.ApplicationId,
            _definition.Publisher, _definition.FamilyId.ToString("D"), _definition.Revision, _definition.Digest.Value);

    internal void SetLocale(string? locale)
    {
        if (_disposed || _locale == locale) return;
        _locale = locale;
        OnPropertyChanged(nameof(DefinitionPin));
    }

    partial void OnDefinitionPathChanged(string value) => _messenger.Send(new InputsInvalidatedMessage());

    private void ClearDefinition()
    {
        _definition = null;
        _messenger.Send(new DefinitionClearedMessage());
        OnPropertyChanged(nameof(DefinitionPin));
    }

    public bool LoadDefinition(ReadOnlyMemory<byte> source, bool yaml = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClearDefinition();
        return Admit(source, yaml);
    }

    private bool Admit(ReadOnlyMemory<byte> source, bool yaml)
    {
        try
        {
            Definition definition = yaml ? DefinitionSourceCodec.ParseYaml(source)
                : DefinitionSourceCodec.ParseJson(source);
            _definition = definition;
            _messenger.Send(new DefinitionLoadedMessage(definition));
            _messenger.Send(new StatusChangedMessage("definition-loaded", _generation));
            OnPropertyChanged(nameof(DefinitionPin));
            _messenger.Send(new FormFocusRequestedMessage());
            return true;
        }
        catch (DefinitionException exception)
        {
            _messenger.Send(new StatusChangedMessage($"definition-invalid:{exception.Code}", _generation, DesktopStatusGuidance.DefinitionInvalid));
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public async Task<bool> LoadDefinitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSelectPath) return false;
        ClearDefinition();
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "load", cancellationToken);
        if (operation is null) return false;
        string path = DefinitionPath;
        try
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is not (".json" or ".yaml" or ".yml"))
            {
                _messenger.Send(new StatusChangedMessage("definition-invalid:source-format-invalid", operation.Generation, DesktopStatusGuidance.DefinitionInvalid));
                return false;
            }
            using BoundedFileInput source = await BoundedFileInput.ReadAsync(() => _openRead(path),
                ProtectedArtifactWriter.MaximumBytes, operation.Token);
            if (!operation.IsCurrent) return false;
            return Admit(source.Bytes, extension is ".yaml" or ".yml");
        }
        catch (InvalidDataException)
        {
            _messenger.Send(new StatusChangedMessage("definition-invalid:source-limit", operation.Generation, DesktopStatusGuidance.DefinitionInvalid));
            return false;
        }
        catch (OperationCanceledException)
        {
            _messenger.Send(new StatusChangedMessage("load-cancelled", operation.Generation));
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("definition-unreadable", operation.Generation, DesktopStatusGuidance.DefinitionInvalid));
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public async Task<bool> SelectDefinitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "selection", cancellationToken);
        if (operation is null) return false;
        bool accepted = false;
        long focusGeneration = operation.Generation;
        try
        {
            StorageSelection<Uri> selected = await _dialogs.SelectAsync(DesktopPathSelectionKind.Definition, operation.Token);
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
            DefinitionPath = path;
            Task<bool> loading = LoadDefinitionAsync(cancellationToken);
            focusGeneration = _generation;
            accepted = await loading;
            return accepted;
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
                _messenger.Send(new FocusRequestedMessage("SelectDefinition"));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _definition = null;
        _messenger.UnregisterAll(this);
        LoadDefinitionCommand.NotifyCanExecuteChanged();
        SelectDefinitionCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DefinitionLoaderViewModel);
}
