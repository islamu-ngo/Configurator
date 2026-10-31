namespace Configurator.Desktop.ViewModels;

using System.Globalization;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

/// <summary>Prepares pinned snapshots and consumes restricted output through create-only publication.</summary>
public sealed partial class ArtifactPublisherViewModel : ObservableObject, IDisposable
{
    private static readonly ResourceManager ImportText = new(typeof(DesktopActionText));
    private readonly CompositeFormat _importFactsFormat = CompositeFormat.Parse(Text("ImportFactsFormat"));
    private readonly CompositeFormat _importAdoptionFormat = CompositeFormat.Parse(Text("ImportAdoptionFormat"));
    private readonly CompositeFormat _importPageFormat = CompositeFormat.Parse(Text("ImportPageFormat"));
    private const int ImportPageSize = 32;
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, Stream> _openImport;
    private readonly Func<CancellationToken, ValueTask> _cooperativeYield;
    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> _save;
    private readonly Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
        Task<ProtectedArtifactBundleResult>> _publishBundle;
    private readonly SetupPresentationSession _session;
    private DefinitionFormModel? _form;
    private SetupPresentationWorkspace? _preparationWorkspace;
    private string[]? _preparedBundleSelection;
    private long _preparedBundleInputRevision;
    private readonly HashSet<string> _selectedArtifactIds = new(StringComparer.Ordinal);
    private IReadOnlyList<ArtifactSelectionViewModel> _artifactSelections = [];
    private readonly object _receiptGate = new();
    private readonly Queue<ProtectedArtifactPublicationReceipt> _publicationHistory = new();
    private DesktopPublicationOperation? _publication;
    private long _generation;
    private bool _disposed;
    private bool _replacingForm;
    private bool _comparingInput;
    private long _reviewGeneration;
    private int _importPage;
    private ImportFindingViewModel[] _importFindings = [];

    [ObservableProperty]
    private int _importPurposeIndex;
    [ObservableProperty]
    private string _importExplanation = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrepareBundleCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveBundleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectOutputDirectoryCommand))]
    private bool _isBusy;
    [ObservableProperty]
    private bool _isSaving;
    [ObservableProperty]
    private bool _isSelecting;
    [ObservableProperty]
    private string _outputDirectory;
    [ObservableProperty]
    private string _fileName = string.Empty;
    [ObservableProperty]
    private string _existingFilePath = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedArtifact))]
    [NotifyPropertyChangedFor(nameof(ArtifactConsequences))]
    private string? _artifactId;

    public ArtifactPublisherViewModel(IMessenger messenger, IStorageDialogService dialogs,
        IProtectedArtifactWriter writer, Func<CancellationToken, ValueTask>? cooperativeYield = null)
        : this(messenger, dialogs, async (path, bytes, token) =>
        {
            using ProtectedArtifactPreparation preparation = await writer.PrepareAsync(
                SetupArtifactKind.Unknown, path, bytes, token);
            try { return await preparation.CommitAsync(token); }
            catch (Exception) when (preparation.EstablishedResult is not null)
            {
                return preparation.EstablishedResult;
            }
        }, Directory.GetCurrentDirectory(), writer.IsAvailable,
            (artifacts, directory, observe, token) => ProtectedArtifactBundlePublisher.PublishAsync(
                writer, artifacts, directory, observe, token), cooperativeYield: cooperativeYield)
    {
    }

    internal ArtifactPublisherViewModel(IMessenger messenger, IStorageDialogService dialogs,
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> save,
        string outputDirectory, bool protectedOutputAvailable,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null,
        Func<string, Stream>? openImport = null,
        Func<CancellationToken, ValueTask>? cooperativeYield = null)
    {
        _messenger = messenger;
        _dialogs = dialogs;
        _cooperativeYield = cooperativeYield ?? YieldDispatcherAsync;
        _openImport = openImport ?? (path => new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous));
        _save = save;
        _publishBundle = publishBundle ?? ((artifacts, directory, observe, token) =>
            ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(), artifacts, directory, observe, token));
        _outputDirectory = outputDirectory;
        ProtectedSaveAvailable = protectedOutputAvailable;
        _session = new SetupPresentationSession(messenger);
        PrepareBundleCommand = new RelayCommand(() => PrepareBundle(), () => CanPrepare);
        FillImportFieldsCommand = new AsyncRelayCommand(
            async token => { await FillImportFieldsAsync(token); }, () => CanFillImportFields);
        messenger.Register<ArtifactPublisherViewModel, FormWorkspaceChangedMessage>(this,
            static (publisher, message) => publisher.ReplaceForm(message.Form));
        messenger.Register<ArtifactPublisherViewModel, WorkspaceInvalidatedMessage>(this,
            static (publisher, message) =>
            {
                publisher._generation = message.Generation;
                publisher.ClearImportComparison();
                publisher.ClearPreparation();
            });
        messenger.Register<ArtifactPublisherViewModel, OperationStateMessage>(this,
            static (publisher, message) =>
            {
                if (message.IsBusy && message.Kind != "fill-import") publisher.ClearImportComparison();
                publisher.IsBusy = message.IsBusy;
                publisher.IsSaving = message.IsBusy && message.Kind == "save";
                publisher.IsSelecting = message.IsBusy && message.Kind == "selection";
                publisher.NotifyCommands();
            });
    }

    public IReadOnlyList<SetupArtifactDefinition> Artifacts => _form?.Definition.Artifacts ?? [];
    public IReadOnlyList<ArtifactSelectionViewModel> ArtifactSelections => _artifactSelections;
    public SetupArtifactDefinition? SelectedArtifact
    {
        get => Artifacts.FirstOrDefault(item => item.Id == ArtifactId);
        set => ArtifactId = value?.Id;
    }
    public IReadOnlyList<string> SelectedArtifactIds => Artifacts.Where(item => _selectedArtifactIds.Contains(item.Id))
        .Select(item => item.Id).ToArray();
    public bool ProtectedSaveAvailable { get; }
    public string ProtectedCapability => ProtectedSaveAvailable
        ? "Protected create-only save is available on this host."
        : "Protected save is unavailable on this host; prepared output cannot be saved here.";
    public string ArtifactConsequences => SelectedArtifact is { } artifact
        ? $"Format: {artifact.Format} | apply: {artifact.ApplySemantics} | restart: {artifact.Restart}\n" +
          "Restricted output. Save creates a new owner-only file; it never overwrites or applies it."
        : "Select a declared artifact.";
    public DefinitionPreparationMetadata? Preparation => _preparationWorkspace?.Result as DefinitionPreparationMetadata;
    public bool CanSelectPath => !_disposed && !IsBusy;
    public bool CanPrepare => !_disposed && !IsBusy && _form is not null;
    public bool CanImport => CanPrepare && ArtifactId is not null;
    public IReadOnlyList<string> ImportPurposes => [Text("CompareLabel"), Text("FillFieldsLabel")];
    public string ImportPurposeLabel => Text(nameof(ImportPurposeLabel));
    public string ReviewFileLabel => Text(nameof(ReviewFileLabel));
    public string ImportLimitHelp => Text(nameof(ImportLimitHelp));
    public string ImportComparisonTitle => Text(nameof(ImportComparisonTitle));
    public string FillFieldsLabel => Text(nameof(FillFieldsLabel));
    public string DismissImportLabel => Text(nameof(DismissImportLabel));
    public string PreviousImportPageLabel => Text(nameof(PreviousImportPageLabel));
    public string NextImportPageLabel => Text(nameof(NextImportPageLabel));
    public string ImportSchemaHelp => Text(nameof(ImportSchemaHelp));
    public bool HasImportComparison => !_disposed && _form?.ImportComparison is not null;
    public bool CanFillImportFields => CanImport && _form?.CanAdoptImportComparison == true;
    public string ImportFacts => _form?.ImportComparison is { } report
        ? string.Format(CultureInfo.CurrentCulture, _importFactsFormat, report.ArtifactId,
            report.Compatibility, report.ExecutionCompleted, report.CoverageComplete,
            report.InventoryComplete, report.AutofillEligible) : string.Empty;
    public string ImportAdoption => string.Format(CultureInfo.CurrentCulture, _importAdoptionFormat,
        _form?.ImportAutofillCode ?? "import-stale");
    public IReadOnlyList<ImportFindingViewModel> ImportFindings => _importFindings;
    public string ImportPageText => string.Format(CultureInfo.CurrentCulture, _importPageFormat,
        _importFindings.Length == 0 ? 0 : _importPage * ImportPageSize + 1,
        _importPage * ImportPageSize + _importFindings.Length, _form?.ImportComparison?.RetainedFindingCount ?? 0);
    public bool CanPreviousImportPage => HasImportComparison && _importPage > 0;
    public bool CanNextImportPage => HasImportComparison &&
        (_importPage + 1) * ImportPageSize < _form!.ImportComparison!.RetainedFindingCount;
    public bool CanSave => !_disposed && !IsBusy && ProtectedSaveAvailable &&
        Preparation is { Succeeded: true } && _preparationWorkspace is { Output.IsEmpty: false };
    public bool CanSaveBundle => !_disposed && !IsBusy && ProtectedSaveAvailable && _preparedBundleSelection is not null;
    public IRelayCommand PrepareBundleCommand { get; }
    public IAsyncRelayCommand FillImportFieldsCommand { get; }
    public IReadOnlyList<ProtectedArtifactPublicationReceipt> PublicationHistory
    {
        get { lock (_receiptGate) return Array.AsReadOnly(_publicationHistory.ToArray()); }
    }
    public ProtectedArtifactPublicationReceipt? CurrentPublication
    {
        get { lock (_receiptGate) return _publication?.Receipt; }
    }
    public ProtectedArtifactPublicationReceipt? LatestPublication
    {
        get { lock (_receiptGate) return _publication?.Receipt ?? _publicationHistory.LastOrDefault(); }
    }
    public string PublicationSummary => LatestPublication is { } receipt ? FormatReceipt(receipt) : "No publication attempted.";
    public IReadOnlyList<string> PublicationHistoryText => PublicationHistory.Select(FormatReceipt).ToArray();

    private DesktopPublicationOperation BeginPublication(DesktopOperation operation, IEnumerable<string> artifactIds,
        byte[]? bytes = null, DefinitionBundleResult? bundle = null)
    {
        var publication = new DesktopPublicationOperation(operation, artifactIds, PublicationChanged, bytes, bundle);
        lock (_receiptGate) _publication = publication;
        NotifyPublication();
        return publication;
    }

    private void PublicationChanged(ProtectedArtifactPublicationReceipt receipt)
    {
        if (receipt.IsSettled)
        {
            lock (_receiptGate)
            {
                _publicationHistory.Enqueue(receipt);
                while (_publicationHistory.Count > 16) _publicationHistory.Dequeue();
                _publication = null;
            }
        }
        NotifyPublication();
    }

    private void NotifyPublication()
    {
        if (_disposed) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(NotifyPublication);
            return;
        }
        OnPropertyChanged(nameof(PublicationHistory));
        OnPropertyChanged(nameof(CurrentPublication));
        OnPropertyChanged(nameof(LatestPublication));
        OnPropertyChanged(nameof(PublicationSummary));
        OnPropertyChanged(nameof(PublicationHistoryText));
    }

    internal void MarkPublicationUnknown()
    {
        DesktopPublicationOperation? publication;
        lock (_receiptGate) publication = _publication;
        publication?.MarkUnknown();
    }

    private static string FormatReceipt(ProtectedArtifactPublicationReceipt receipt) =>
        $"Attempt {receipt.AttemptId:N}: " +
        (receipt.Failed ? "failed; unknown" : !receipt.IsSettled || receipt.IsUnknown ? "unknown" : "settled") +
        (receipt.CompletionCode is { } code ? $"; {code}" : string.Empty) +
        (receipt.CleanupFailed ? "; cleanup-failed" : string.Empty) + "\n" +
        string.Join("\n", receipt.Outcomes.Select(outcome => outcome.ArtifactId + ": " +
            (outcome.Result is { } result ? FormatCommitResult(result)
                : !receipt.IsSettled || receipt.IsUnknown ? "unknown" : "not-attempted")));

    partial void OnOutputDirectoryChanged(string value) => _messenger.Send(new InputsInvalidatedMessage());
    partial void OnFileNameChanged(string value)
    {
        if (!_replacingForm) _messenger.Send(new InputsInvalidatedMessage());
    }
    partial void OnExistingFilePathChanged(string value)
    {
        ClearImportComparison();
        if (IsBusy && !_comparingInput) _messenger.Send(new InputsInvalidatedMessage());
    }
    partial void OnImportPurposeIndexChanged(int value) => ClearImportComparison();
    partial void OnArtifactIdChanged(string? value)
    {
        if (!_replacingForm) _messenger.Send(new InputsInvalidatedMessage());
        FileName = Artifacts.FirstOrDefault(item => item.Id == value)?.FileName ?? string.Empty;
        NotifyCommands();
    }

    /// <summary>Replaces choices before selecting their default, so two-way picker reset cannot erase the new selection.</summary>
    private void ReplaceForm(DefinitionFormModel? form)
    {
        ClearPreparation();
        if (_form is not null)
        {
            _form.ImportComparisonChanged -= ImportComparisonChanged;
            _form.ClearImportComparison();
        }
        _form = form;
        if (form is not null) form.ImportComparisonChanged += ImportComparisonChanged;
        ImportPurposeIndex = 0;
        RefreshImportComparison();
        _selectedArtifactIds.Clear();
        _replacingForm = true;
        try
        {
            OnPropertyChanged(nameof(Artifacts));
            ArtifactId = Artifacts.Count > 0 ? Artifacts[0].Id : null;
            FileName = Artifacts.Count > 0 ? Artifacts[0].FileName : string.Empty;
        }
        finally { _replacingForm = false; }
        if (ArtifactId is not null) _selectedArtifactIds.Add(ArtifactId);
        _artifactSelections = Artifacts.Select(item => new ArtifactSelectionViewModel(item.Id, item.FileName,
            _selectedArtifactIds.Contains(item.Id), selected => SelectArtifact(item.Id, selected))).ToArray();
        OnPropertyChanged(nameof(ArtifactSelections));
        OnPropertyChanged(nameof(SelectedArtifactIds));
        OnPropertyChanged(nameof(ArtifactId));
        OnPropertyChanged(nameof(SelectedArtifact));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(ArtifactConsequences));
        NotifyCommands();
    }

    public void SelectArtifact(string artifactId, bool selected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Artifacts.All(item => item.Id != artifactId)) return;
        if (!(selected ? _selectedArtifactIds.Add(artifactId) : _selectedArtifactIds.Remove(artifactId))) return;
        _artifactSelections.First(item => item.Id == artifactId).SetSelected(selected);
        _messenger.Send(new InputsInvalidatedMessage());
        OnPropertyChanged(nameof(SelectedArtifactIds));
    }

    private bool Validate()
    {
        bool ready = false;
        _messenger.Send(new FormValidationRequestedMessage(value => ready = value));
        return ready;
    }

    [RelayCommand(CanExecute = nameof(CanPrepare))]
    public async Task<bool> PrepareAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy) return false;
        _messenger.Send(new InputsInvalidatedMessage());
        if (!Validate() || _form is null || ArtifactId is null) return false;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "prepare");
        if (operation is null) return false;
        if (!SetupWorkspaceId.TryCreate("desktop-definition", out var id))
            throw new InvalidOperationException("desktop-workspace-invalid");
        SetupPresentationWorkspace workspace = _session.CreateWorkspace(id,
            new DefinitionPresentationOperation(_form.Definition, ArtifactId, _form.Snapshot()));
        _preparationWorkspace = workspace;
        workspace.Activate();
        await workspace.ExecuteAsync(Guid.CreateVersion7());
        if (!operation.IsCurrent || !ReferenceEquals(_preparationWorkspace, workspace)) return false;
        _messenger.Send(new StatusChangedMessage(Preparation is { Succeeded: true } metadata
            ? $"artifact-ready:{metadata.ArtifactId}:{metadata.Digest?.Value}"
            : "preparation-invalid:" + string.Join(",", Preparation?.DiagnosticCodes ?? []), operation.Generation));
        NotifyCommands();
        return Preparation?.Succeeded == true;
    }

    public bool PrepareBundle()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy) return false;
        _messenger.Send(new InputsInvalidatedMessage());
        if (!Validate() || _form is null) return false;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "prepare");
        if (operation is null) return false;
        string[] selection = SelectedArtifactIds.ToArray();
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(_form.Definition, selection, _form.Snapshot());
        if (!bundle.Succeeded)
        {
            _messenger.Send(new StatusChangedMessage("bundle-invalid:" +
                string.Join(",", bundle.Diagnostics.Select(item => item.Code)), operation.Generation));
            return false;
        }
        _preparedBundleSelection = selection;
        _preparedBundleInputRevision = _form.InputRevision;
        _messenger.Send(new StatusChangedMessage($"bundle-selected:{selection.Length}", operation.Generation));
        NotifyCommands();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task<ProtectedArtifactCommitResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy || Preparation is not { Succeeded: true } || _preparationWorkspace is null)
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
        byte[] bytes = _preparationWorkspace.Output.ToArray();
        string artifactId = Preparation.ArtifactId;
        string fileName = FileName;
        string directory = OutputDirectory;
        ClearPreparation();
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "save", cancellationToken);
        if (operation is null)
        {
            CryptographicOperations.ZeroMemory(bytes);
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
        }
        using DesktopPublicationOperation publication = BeginPublication(operation, [artifactId], bytes);
        try
        {
            ProtectedArtifactCommitResult result = !ProtectedSaveAvailable
                ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsupportedPlatform)
                : !SafeFileName(fileName) || string.IsNullOrWhiteSpace(directory)
                    ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsafeTarget)
                : !operation.IsCurrent ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest)
                : await _save(Path.Combine(directory, fileName), bytes, operation.Token);
            publication.Complete([new(artifactId, result)], operation.Token.IsCancellationRequested
                ? ProtectedArtifactBundleCompletionCode.Cancelled : ProtectedArtifactBundleCompletionCode.Completed);
            if (operation.IsCurrent)
                _messenger.Send(new StatusChangedMessage(result.Succeeded ? "save-complete"
                    : "save-" + FormatCommitResult(result), operation.Generation));
            return result;
        }
        catch (OperationCanceledException)
        {
            publication.MarkUnknown(cancelled: true);
            _messenger.Send(new StatusChangedMessage("save-cancelled", operation.Generation));
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            publication.MarkUnknown(failed: true);
            _messenger.Send(new StatusChangedMessage("save-failed:IoFailure", operation.Generation));
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure);
        }
        catch (Exception)
        {
            publication.MarkUnknown(failed: true);
            throw;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveBundle))]
    public async Task<ProtectedArtifactBundleResult?> SaveBundleAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy || _preparedBundleSelection is null || _form is null) return null;
        string[] selection = _preparedBundleSelection;
        long inputRevision = _preparedBundleInputRevision;
        string directory = OutputDirectory;
        _preparedBundleSelection = null;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "save", cancellationToken);
        if (operation is null) return null;
        try
        {
            if (!ProtectedSaveAvailable)
            {
                _messenger.Send(new StatusChangedMessage("bundle-save-blocked:UnsupportedPlatform", operation.Generation));
                return null;
            }
            if (!operation.IsCurrent || _form.InputRevision != inputRevision) return null;
            DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(_form.Definition, selection, _form.Snapshot(), operation.Token);
            if (!bundle.Succeeded)
            {
                _messenger.Send(new StatusChangedMessage("bundle-invalid:" +
                    string.Join(",", bundle.Diagnostics.Select(item => item.Code)), operation.Generation));
                bundle.Dispose();
                return null;
            }
            if (!operation.IsCurrent)
            {
                bundle.Dispose();
                return null;
            }
            using DesktopPublicationOperation publication = BeginPublication(operation,
                bundle.Artifacts.Select(item => item.ArtifactId), bundle: bundle);
            try
            {
                ProtectedArtifactBundleResult result = await _publishBundle(bundle.Artifacts, directory,
                    publication.Observe, operation.Token);
                publication.Complete(result.Outcomes, result.CompletionCode, result.CleanupFailed);
                if (operation.IsCurrent)
                    _messenger.Send(new StatusChangedMessage(result.Succeeded ? "bundle-save-complete" :
                        (result.Outcomes.Any(item => item.Result is { InstallationEstablished: true })
                            ? "bundle-save-partial:" : "bundle-save-blocked:") +
                        string.Join(",", result.Outcomes.Select(item =>
                            $"{item.ArtifactId}:{(item.Result is { } commit ? FormatCommitResult(commit) : "not-attempted")}"))
                        + (result.CleanupFailed ? ";cleanup-failed" : string.Empty), operation.Generation));
                return result;
            }
            catch (OperationCanceledException)
            {
                publication.MarkUnknown(cancelled: true);
                throw;
            }
            catch (Exception)
            {
                publication.MarkUnknown(failed: true);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _messenger.Send(new StatusChangedMessage("bundle-save-cancelled", operation.Generation));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("bundle-save-blocked:IoFailure", operation.Generation));
            return null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    public Task<bool> ImportFileAsync(CancellationToken cancellationToken = default)
    {
        string path = ExistingFilePath;
        BoundedFileInput? input = null;
        return RunImportAsync(async token =>
        {
            input = await BoundedFileInput.ReadAsync(() => _openImport(path),
                ProtectedArtifactWriter.MaximumBytes, token);
            return input.Bytes;
        }, ImportPurposeIndex != 1, cancellationToken, () => input?.Dispose());
    }

    public Task<bool> CompareAsync(Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default) => RunImportAsync(read, true, cancellationToken);

    public Task<bool> ImportAsync(Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default) => RunImportAsync(read, false, cancellationToken);

    private async Task<bool> RunImportAsync(Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        bool compare, CancellationToken cancellationToken, Action? cleanup = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanImport || _form is null || ArtifactId is null) return false;
        DefinitionFormModel form = _form;
        string artifactId = ArtifactId;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, compare ? "compare" : "import", cancellationToken);
        if (operation is null) return false;
        _comparingInput = compare;
        try
        {
            return await ReviewInputAsync(form, artifactId, read, compare, operation);
        }
        finally
        {
            _comparingInput = false;
            cleanup?.Invoke();
        }
    }

    private async Task<bool> ReviewInputAsync(DefinitionFormModel form, string artifactId,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read, bool compare, DesktopOperation operation)
    {
        try
        {
            DefinitionImportComparisonResult comparison = await form.CompareAsync(
                artifactId, read, _cooperativeYield, operation.Token);
            if (_disposed || !operation.IsCurrent || !ReferenceEquals(_form, form)) return false;
            if (compare)
            {
                _messenger.Send(new StatusChangedMessage(comparison.Code == "import-read-failed"
                    ? "import-unreadable" : comparison.Code, operation.Generation));
                return comparison.Succeeded;
            }
            if (!comparison.Succeeded)
            {
                _messenger.Send(new StatusChangedMessage(comparison.Code == "import-read-failed"
                    ? "import-unreadable" : comparison.Code, operation.Generation));
                return false;
            }
            return await AdoptImportComparisonAsync(form, operation);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("import-unreadable", operation.Generation));
            return false;
        }
    }

    private void ImportComparisonChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ImportComparisonChanged(sender, args));
            return;
        }
        RefreshImportComparison();
    }

    private void RefreshImportComparison()
    {
        _importPage = 0;
        ImportExplanation = string.Empty;
        RefreshImportPage();
        OnPropertyChanged(nameof(HasImportComparison));
        OnPropertyChanged(nameof(ImportFacts));
        OnPropertyChanged(nameof(ImportAdoption));
        OnPropertyChanged(nameof(CanFillImportFields));
        FillImportFieldsCommand.NotifyCanExecuteChanged();
    }

    private void RefreshImportPage()
    {
        _importFindings = _form?.ImportComparison?.Findings.Skip(_importPage * ImportPageSize)
            .Take(ImportPageSize).Select(finding => new ImportFindingViewModel(finding,
                new RelayCommand(() => ExplainImportFinding(finding)))).ToArray() ?? [];
        OnPropertyChanged(nameof(ImportFindings));
        OnPropertyChanged(nameof(ImportPageText));
        OnPropertyChanged(nameof(CanPreviousImportPage));
        OnPropertyChanged(nameof(CanNextImportPage));
        PreviousImportPageCommand.NotifyCanExecuteChanged();
        NextImportPageCommand.NotifyCanExecuteChanged();
    }

    private void ExplainImportFinding(DefinitionImportFinding finding)
    {
        if (_form?.ExplainImportFinding(finding) is not { } explanation) return;
        ImportExplanation = explanation.Title + " / " + string.Join(" / ", explanation.Path) +
            (explanation.Help is { Length: > 0 } help ? "\n" + help : string.Empty);
    }

    public async Task<bool> FillImportFieldsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || IsBusy || _form is null) return false;
        DefinitionFormModel form = _form;
        using DesktopOperation? operation = DesktopOperation.Begin(_messenger, "fill-import", cancellationToken);
        if (operation is null) return false;
        return await AdoptImportComparisonAsync(form, operation);
    }

    private async Task<bool> AdoptImportComparisonAsync(DefinitionFormModel form, DesktopOperation operation)
    {
        DefinitionImportResult result = await form.AdoptImportComparisonAsync(_cooperativeYield, operation.Token);
        if (_disposed || !ReferenceEquals(_form, form)) return false;
        // Adoption itself changes input once and cancels this lease; later edits must keep their own status and focus.
        if (result.Succeeded && _generation == operation.Generation + 1)
        {
            long generation = _generation;
            _messenger.Send(new StatusChangedMessage(result.Code, generation));
            if (!_disposed && _generation == generation && ReferenceEquals(_form, form))
                _messenger.Send(new FormFocusRequestedMessage());
        }
        else if (!result.Succeeded && operation.IsCurrent)
            _messenger.Send(new StatusChangedMessage(result.Code, operation.Generation));
        return result.Succeeded;
    }

    internal static async ValueTask YieldDispatcherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Dispatcher.Yield(DispatcherPriority.Background);
        cancellationToken.ThrowIfCancellationRequested();
    }

    [RelayCommand]
    public void ClearImportComparison()
    {
        _reviewGeneration = checked(_reviewGeneration + 1);
        _form?.ClearImportComparison();
        RefreshImportComparison();
    }

    [RelayCommand(CanExecute = nameof(CanPreviousImportPage))]
    private void PreviousImportPage() { _importPage--; RefreshImportPage(); }

    [RelayCommand(CanExecute = nameof(CanNextImportPage))]
    private void NextImportPage() { _importPage++; RefreshImportPage(); }

    private static string Text(string key) => ImportText.GetString(key, CultureInfo.CurrentUICulture)!;

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
            input = await _dialogs.ReadImportAsync(operation.Token);
            if (!operation.IsCurrent || input is null || generation != _reviewGeneration ||
                !ReferenceEquals(form, _form)) return false;
            return await ReviewInputAsync(form, artifact, _ => Task.FromResult(input.Bytes), compare, operation);
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("import-unreadable", operation.Generation));
            return false;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            _messenger.Send(new StatusChangedMessage(exception.Message == "selection-nonlocal"
                ? "selection-nonlocal" : "selection-unavailable", operation.Generation));
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
            Uri? selected = await _dialogs.SelectAsync(kind, operation.Token);
            if (!operation.IsCurrent || selected is null) return false;
            if (!AvaloniaStorageDialogService.TryGetLocalPath(selected, out string path))
            {
                _messenger.Send(new StatusChangedMessage("selection-nonlocal", operation.Generation));
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
                _messenger.Send(new FocusRequestedMessage("SelectOutput"));
        }
    }

    private void ClearPreparation()
    {
        _preparedBundleSelection = null;
        if (_preparationWorkspace is not null)
        {
            if (MemoryMarshal.TryGetArray(_preparationWorkspace.Output, out ArraySegment<byte> segment))
                CryptographicOperations.ZeroMemory(segment.AsSpan());
            _preparationWorkspace.Dispose();
            _preparationWorkspace = null;
        }
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanPrepare));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveBundle));
        OnPropertyChanged(nameof(CanSelectPath));
        OnPropertyChanged(nameof(Preparation));
        OnPropertyChanged(nameof(CanFillImportFields));
        FillImportFieldsCommand.NotifyCanExecuteChanged();
        PrepareCommand.NotifyCanExecuteChanged();
        PrepareBundleCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveBundleCommand.NotifyCanExecuteChanged();
        ImportFileCommand.NotifyCanExecuteChanged();
        SelectImportCommand.NotifyCanExecuteChanged();
        SelectOutputDirectoryCommand.NotifyCanExecuteChanged();
    }

    private static string FormatCommitResult(ProtectedArtifactCommitResult result) =>
        (result.InstallationEstablished
            ? (result.VerificationSucceeded ? "installed-verified:" : "installed-unverified:") + result.Status
                + ";current-integrity-not-guaranteed;manual-review-required"
            : "failed:" + result.Status)
        + (result.CleanupFailed ? ";cleanup-failed" : string.Empty);

    private static bool SafeFileName(string value) =>
        value.Length is > 0 and <= 128 && value is not "-" and not "." and not ".." &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearImportComparison();
        if (_form is not null) _form.ImportComparisonChanged -= ImportComparisonChanged;
        ClearPreparation();
        _form = null;
        _session.Dispose();
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(ArtifactPublisherViewModel);
}

/// <summary>One detached, value-free imported-file issue; it never holds a proposal or workspace location.</summary>
public sealed class ImportFindingViewModel(DefinitionImportFinding finding, IRelayCommand explain)
{
    private static readonly ResourceManager Text = new(typeof(DesktopActionText));
    private readonly CompositeFormat _summaryFormat = CompositeFormat.Parse(
        Text.GetString("ImportFindingFormat", CultureInfo.CurrentUICulture)!);
    public string Summary => string.Format(CultureInfo.CurrentCulture,
        _summaryFormat, finding.Kind, finding.Code);
    public bool CanExplain => finding.Schema is not null;
    public bool IsBlocking => finding.Kind is DefinitionImportFindingKind.MissingRequired or
        DefinitionImportFindingKind.InvalidSuppliedValue or DefinitionImportFindingKind.UnrecognizedInput or
        DefinitionImportFindingKind.StructuralOutputChange;
    public string ExplainLabel => Text.GetString("ExplainImportLabel", CultureInfo.CurrentUICulture)!;
    public IRelayCommand ExplainCommand { get; } = explain;
}

/// <summary>Represents one declared bundle selection; the publisher owns its membership.</summary>
public sealed class ArtifactSelectionViewModel(string id, string title, bool selected, Action<bool> select)
    : ObservableObject
{
    private bool _isSelected = selected;
    public string Id { get; } = id;
    public string Title { get; } = title;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) select(value); }
    }
    internal void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
}
