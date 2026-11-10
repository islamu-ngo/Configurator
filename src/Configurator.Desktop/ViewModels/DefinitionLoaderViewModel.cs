namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation.Intake;
using Configurator.Presentation.Store;

/// <summary>Admits bounded local, remote HTTPS, and embedded store definitions before creating a workspace.</summary>
public sealed partial class DefinitionLoaderViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, Stream> _openRead;
    private readonly IDefinitionRemoteFetcher _remoteFetcher;
    private readonly IDefinitionStoreProvider _storeProvider;
    private readonly bool _ownsRemoteFetcher;
    private readonly bool _ownsStoreProvider;
    private Definition? _definition;
    private string? _locale;
    private long _generation;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadDefinitionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectDefinitionCommand))]
    [NotifyCanExecuteChangedFor(nameof(FetchDefinitionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfigureStoreProjectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _definitionPath = string.Empty;

    [ObservableProperty]
    private string _definitionUrl = string.Empty;

    public DefinitionLoaderViewModel(
        IMessenger messenger,
        IStorageDialogService dialogs,
        IDefinitionRemoteFetcher? remoteFetcher = null,
        IDefinitionStoreProvider? storeProvider = null)
        : this(messenger, dialogs, static path => new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous), remoteFetcher, storeProvider)
    {
    }

    internal DefinitionLoaderViewModel(
        IMessenger messenger,
        IStorageDialogService dialogs,
        Func<string, Stream> openRead,
        IDefinitionRemoteFetcher? remoteFetcher = null,
        IDefinitionStoreProvider? storeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(openRead);

        _messenger = messenger;
        _dialogs = dialogs;
        _openRead = openRead;

        if (remoteFetcher is null)
        {
            _remoteFetcher = new DefinitionRemoteFetcher();
            _ownsRemoteFetcher = true;
        }
        else
        {
            _remoteFetcher = remoteFetcher;
        }

        if (storeProvider is null)
        {
            _storeProvider = new EmbeddedDefinitionStoreProvider();
            _ownsStoreProvider = true;
        }
        else
        {
            _storeProvider = storeProvider;
        }

        StoreCatalog = new StoreCatalogViewModel(_storeProvider);
        StoreCatalog.PropertyChanged += OnStoreCatalogPropertyChanged;

        messenger.Register<DefinitionLoaderViewModel, OperationStateMessage>(this,
            static (loader, message) => loader.IsBusy = message.IsBusy);
        messenger.Register<DefinitionLoaderViewModel, WorkspaceInvalidatedMessage>(this,
            static (loader, message) => loader._generation = message.Generation);
    }

    public bool CanSelectPath => !_disposed && !IsBusy;
    public StoreCatalogViewModel StoreCatalog { get; }

    public string StoreSearchQuery
    {
        get => StoreCatalog.SearchQuery;
        set => StoreCatalog.SearchQuery = value ?? string.Empty;
    }

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

    private void OnStoreCatalogPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StoreCatalogViewModel.SearchQuery))
            OnPropertyChanged(nameof(StoreSearchQuery));
    }

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
        return ParseAndAdmit(source, yaml, _generation);
    }

    public async Task<bool> LoadStoreAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool loaded = await StoreCatalog.LoadAsync(cancellationToken);
        if (!loaded && !_disposed && StoreCatalog.LoadFailed)
            _messenger.Send(new StatusChangedMessage("store-unavailable", _generation, DesktopStatusGuidance.StoreOpenFailed));
        return loaded;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public async Task<bool> FetchDefinitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSelectPath) return false;

        string url = (DefinitionUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            _messenger.Send(new StatusChangedMessage("remote-uri-invalid", _generation, DesktopStatusGuidance.RemoteUriInvalid));
            _messenger.Send(new FocusRequestedMessage("DefinitionUrl"));
            return false;
        }

        ClearDefinition();
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "remote-fetch", cancellationToken);
        if (operation is null) return false;

        bool accepted = false;
        long focusGeneration = operation.Generation;
        try
        {
            DefinitionRemoteResult result = await _remoteFetcher.FetchAsync(url, operation.Token);
            if (!operation.IsCurrent) return false;
            accepted = ApplyRemoteResult(result, operation.Generation);
            return accepted;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            operation.Dispose();
            if (!accepted && !_disposed && focusGeneration == _generation)
                _messenger.Send(new FocusRequestedMessage("DefinitionUrl"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfigureStoreProject))]
    public async Task<bool> ConfigureStoreProjectAsync(
        StoreProjectCardViewModel? card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSelectPath) return false;
        if (card.LatestRevision < 1)
        {
            _messenger.Send(new StatusChangedMessage("store-revision-not-found", _generation, DesktopStatusGuidance.StoreOpenFailed));
            return false;
        }

        ClearDefinition();
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "store-open", cancellationToken);
        if (operation is null) return false;

        try
        {
            DefinitionStoreResult result = await _storeProvider.OpenAsync(card.ProjectId, card.LatestRevision, operation.Token);
            if (!operation.IsCurrent) return false;
            return ApplyStoreResult(result, operation.Generation);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private bool CanConfigureStoreProject(StoreProjectCardViewModel? card) => CanSelectPath && card is not null;

    [RelayCommand(CanExecute = nameof(CanSelectPath))]
    public async Task<bool> LoadDefinitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanSelectPath) return false;
        string path = DefinitionPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _messenger.Send(new StatusChangedMessage("path-empty", _generation, DesktopStatusGuidance.PathEmpty));
            return false;
        }
        ClearDefinition();
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "load", cancellationToken);
        if (operation is null) return false;
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
            return ParseAndAdmit(source.Bytes, extension is ".yaml" or ".yml", operation.Generation);
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

    private bool ParseAndAdmit(ReadOnlyMemory<byte> source, bool yaml, long generation)
    {
        try
        {
            Definition definition = yaml
                ? DefinitionSourceCodec.ParseYaml(source)
                : DefinitionSourceCodec.ParseJson(source);
            return Admit(definition, generation);
        }
        catch (DefinitionException exception)
        {
            _messenger.Send(new StatusChangedMessage($"definition-invalid:{exception.Code}", generation, DesktopStatusGuidance.DefinitionInvalid));
            return false;
        }
    }

    private bool ApplyRemoteResult(DefinitionRemoteResult result, long generation) => result switch
    {
        DefinitionRemoteResult.Success(var definition) => Admit(definition, generation),
        DefinitionRemoteResult.InvalidUri => Fail("remote-uri-invalid", generation, DesktopStatusGuidance.RemoteUriInvalid),
        DefinitionRemoteResult.BlockedHost => Fail("remote-host-blocked", generation, DesktopStatusGuidance.RemoteHostBlocked),
        DefinitionRemoteResult.PayloadTooLarge => Fail("remote-payload-limit", generation, DesktopStatusGuidance.RemotePayloadLimit),
        DefinitionRemoteResult.NetworkError => Fail("remote-fetch-failed", generation, DesktopStatusGuidance.RemoteFetchFailed),
        DefinitionRemoteResult.ParseError => Fail("remote-definition-invalid", generation, DesktopStatusGuidance.DefinitionInvalid),
        _ => Fail("remote-fetch-failed", generation, DesktopStatusGuidance.RemoteFetchFailed)
    };

    private bool ApplyStoreResult(DefinitionStoreResult result, long generation) => result switch
    {
        DefinitionStoreResult.Success(var definition) => Admit(definition, generation),
        DefinitionStoreResult.ProjectNotFound => Fail("store-project-not-found", generation, DesktopStatusGuidance.StoreOpenFailed),
        DefinitionStoreResult.RevisionNotFound => Fail("store-revision-not-found", generation, DesktopStatusGuidance.StoreOpenFailed),
        DefinitionStoreResult.SnapshotMismatch => Fail("store-snapshot-mismatch", generation, DesktopStatusGuidance.StoreOpenFailed),
        DefinitionStoreResult.CorruptAsset => Fail("store-asset-corrupt", generation, DesktopStatusGuidance.StoreOpenFailed),
        _ => Fail("store-open-failed", generation, DesktopStatusGuidance.StoreOpenFailed)
    };

    private bool Fail(string status, long generation, DesktopStatusGuidance guidance)
    {
        _messenger.Send(new StatusChangedMessage(status, generation, guidance));
        return false;
    }

    private bool Admit(Definition definition, long generation)
    {
        _definition = definition;
        _messenger.Send(new DefinitionLoadedMessage(definition));
        _messenger.Send(new StatusChangedMessage("definition-loaded", generation));
        OnPropertyChanged(nameof(DefinitionPin));
        _messenger.Send(new FormFocusRequestedMessage());
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _definition = null;
        StoreCatalog.PropertyChanged -= OnStoreCatalogPropertyChanged;
        StoreCatalog.Dispose();
        if (_ownsRemoteFetcher) _remoteFetcher.Dispose();
        if (_ownsStoreProvider) (_storeProvider as IDisposable)?.Dispose();
        _messenger.UnregisterAll(this);
        LoadDefinitionCommand.NotifyCanExecuteChanged();
        SelectDefinitionCommand.NotifyCanExecuteChanged();
        FetchDefinitionCommand.NotifyCanExecuteChanged();
        ConfigureStoreProjectCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DefinitionLoaderViewModel);
}
