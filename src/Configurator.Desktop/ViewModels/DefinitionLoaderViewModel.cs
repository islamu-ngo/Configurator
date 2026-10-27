namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Services;

/// <summary>Admits bounded local definition bytes before creating an editable workspace.</summary>
public sealed partial class DefinitionLoaderViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, Stream> _openRead;
    private SetupDefinition? _definition;
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
    public string DefinitionPin => _definition is null ? "No definition loaded" :
        $"{_definition.Title} | {_definition.ApplicationId} | publisher: {_definition.Publisher}\n" +
        $"Family: {_definition.FamilyId:D} | revision: {_definition.Revision} | SHA-256: {_definition.Digest.Value}\n" +
        "Publisher metadata is not a trust assertion. Local preparation does not apply configuration.";

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
            SetupDefinition definition = yaml ? SetupDefinitionSourceCodec.ParseYaml(source)
                : SetupDefinitionSourceCodec.ParseJson(source);
            _definition = definition;
            _messenger.Send(new DefinitionLoadedMessage(definition));
            _messenger.Send(new StatusChangedMessage("definition-loaded", _generation));
            OnPropertyChanged(nameof(DefinitionPin));
            _messenger.Send(new FormFocusRequestedMessage());
            return true;
        }
        catch (SetupDefinitionException exception)
        {
            _messenger.Send(new StatusChangedMessage($"definition-invalid:{exception.Code}", _generation));
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
                _messenger.Send(new StatusChangedMessage("definition-invalid:source-format-invalid", operation.Generation));
                return false;
            }
            using BoundedFileInput source = await BoundedFileInput.ReadAsync(() => _openRead(path),
                ProtectedArtifactWriter.MaximumBytes, operation.Token);
            if (!operation.IsCurrent) return false;
            return Admit(source.Bytes, extension is ".yaml" or ".yml");
        }
        catch (InvalidDataException)
        {
            _messenger.Send(new StatusChangedMessage("definition-invalid:source-limit", operation.Generation));
            return false;
        }
        catch (OperationCanceledException)
        {
            _messenger.Send(new StatusChangedMessage("load-cancelled", operation.Generation));
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("definition-unreadable", operation.Generation));
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
            Uri? selected = await _dialogs.SelectAsync(DesktopPathSelectionKind.Definition, operation.Token);
            if (!operation.IsCurrent || selected is null) return false;
            if (!AvaloniaStorageDialogService.TryGetLocalPath(selected, out string path))
            {
                _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation));
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
            _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation));
            return false;
        }
        catch (InvalidOperationException exception) when (exception.Message == "selection-nonlocal")
        {
            _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation));
            return false;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or
            UnauthorizedAccessException or InvalidOperationException)
        {
            _messenger.Send(new StatusChangedMessage("selection-unavailable", operation.Generation));
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
