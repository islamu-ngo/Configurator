namespace Configurator.Desktop.ViewModels;

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
    private DesktopStrings Strings => new(_form?.Locale);
    private const int ImportPageSize = 32;
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, Stream> _openImport;
    private readonly Func<CancellationToken, ValueTask> _cooperativeYield;
    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> _save;
    private readonly Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
        Task<ProtectedArtifactBundleResult>> _publishBundle;
    private readonly PresentationSession _session;
    private DefinitionFormModel? _form;
    private PresentationWorkspace? _preparationWorkspace;
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
                ArtifactKind.Unknown, path, bytes, token);
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
        _session = new PresentationSession(messenger);
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

    public IReadOnlyList<ArtifactDefinition> Artifacts => _form?.Definition.Artifacts ?? [];
    public IReadOnlyList<ArtifactSelectionViewModel> ArtifactSelections => _artifactSelections;
    public ArtifactDefinition? SelectedArtifact
    {
        get => Artifacts.FirstOrDefault(item => item.Id == ArtifactId);
        set => ArtifactId = value?.Id;
    }
    public IReadOnlyList<string> SelectedArtifactIds => Artifacts.Where(item => _selectedArtifactIds.Contains(item.Id))
        .Select(item => item.Id).ToArray();
    public bool ProtectedSaveAvailable { get; }
    public string ProtectedCapability => Strings[ProtectedSaveAvailable ? "ProtectedSaveAvailable" : "ProtectedSaveUnavailable"];
    public string ArtifactConsequences => SelectedArtifact is { } artifact
        ? Strings.Format("ArtifactConsequences", artifact.Format.ToToken(), artifact.ApplySemantics.ToToken(), artifact.Restart.ToToken())
        : Strings["SelectArtifact"];
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
        ? Strings.Format("ImportFactsFormat", report.ArtifactId,
            PresentationStrings.ImportCompatibility(report.Compatibility, _form.Locale),
            Strings[report.ExecutionCompleted ? "Yes" : "No"], Strings[report.CoverageComplete ? "Yes" : "No"],
            Strings[report.InventoryComplete ? "Yes" : "No"], Strings[report.AutofillEligible ? "Yes" : "No"]) : string.Empty;
    public string ImportAdoption => Strings.Format("ImportAdoptionFormat",
        PresentationStrings.ImportAutofill(_form?.ImportAutofillCode, _form?.Locale ?? "en"));
    public IReadOnlyList<ImportFindingViewModel> ImportFindings => _importFindings;
    public string ImportPageText => Strings.Format("ImportPageFormat",
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
    public string PublicationSummary => LatestPublication is { } receipt ? FormatReceipt(receipt) : Strings["NoPublication"];
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

    private string FormatReceipt(ProtectedArtifactPublicationReceipt receipt) =>
        Strings.Format("PublicationAttempt", receipt.AttemptId.ToString("N")) + ": " +
        Strings[receipt.Failed ? "PublicationFailedUnknown" : !receipt.IsSettled || receipt.IsUnknown ? "PublicationUnknown" : "PublicationSettled"] +
        (receipt.CompletionCode is { } code ? $"; {code}" : string.Empty) +
        (receipt.CleanupFailed ? "; " + Strings["CleanupFailed"] : string.Empty) + "\n" +
        string.Join("\n", receipt.Outcomes.Select(outcome => outcome.ArtifactId + ": " +
            (outcome.Result is { } result ? FormatCommitDisplay(result)
                : Strings[!receipt.IsSettled || receipt.IsUnknown ? "PublicationUnknown" : "PublicationNotAttempted"])));

    private string FormatCommitDisplay(ProtectedArtifactCommitResult result) =>
        Strings[result.InstallationEstablished ? result.VerificationSucceeded ? "PublicationVerified" : "PublicationUnverified" : "PublicationFailed"] +
        ": " + result.Status + (result.InstallationEstablished ? "; " + Strings["PublicationReview"] : string.Empty) +
        (result.CleanupFailed ? "; " + Strings["CleanupFailed"] : string.Empty);

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
            _form.ViewChanged -= LocaleChanged;
            _form.ClearImportComparison();
        }
        _form = form;
        if (form is not null)
        {
            form.ImportComparisonChanged += ImportComparisonChanged;
            form.ViewChanged += LocaleChanged;
        }
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
        _artifactSelections = Artifacts.Select(item => new ArtifactSelectionViewModel(item.Id, item.FileName, _form?.Locale ?? "en",
            _selectedArtifactIds.Contains(item.Id), selected => SelectArtifact(item.Id, selected))).ToArray();
        OnPropertyChanged(nameof(ArtifactSelections));
        OnPropertyChanged(nameof(SelectedArtifactIds));
        OnPropertyChanged(nameof(ArtifactId));
        OnPropertyChanged(nameof(SelectedArtifact));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(ArtifactConsequences));
        LocaleChanged(form, EventArgs.Empty);
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
        if (!WorkspaceId.TryCreate("desktop-definition", out var id))
            throw new InvalidOperationException("desktop-workspace-invalid");
        PresentationWorkspace workspace = _session.CreateWorkspace(id,
            new DefinitionPresentationOperation(_form.Definition, ArtifactId, _form.Snapshot()));
        _preparationWorkspace = workspace;
        workspace.Activate();
        await workspace.ExecuteAsync(Guid.CreateVersion7());
        if (!operation.IsCurrent || !ReferenceEquals(_preparationWorkspace, workspace)) return false;
        _messenger.Send(new StatusChangedMessage(Preparation is { Succeeded: true } metadata
            ? $"artifact-ready:{metadata.ArtifactId}:{metadata.Digest?.Value}"
            : "preparation-invalid:" + string.Join(",", Preparation?.DiagnosticCodes ?? []), operation.Generation,
            Preparation?.Succeeded == true ? DesktopStatusGuidance.General : DesktopStatusGuidance.PreparationInvalid));
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
                string.Join(",", bundle.Diagnostics.Select(item => item.Code)), operation.Generation, DesktopStatusGuidance.PreparationInvalid));
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
                    : "save-" + FormatCommitResult(result), operation.Generation,
                    result.Succeeded ? DesktopStatusGuidance.General : !result.InstallationEstablished
                        ? DesktopStatusGuidance.SaveFailed : result.VerificationSucceeded
                            ? DesktopStatusGuidance.SaveInstalledVerified : DesktopStatusGuidance.SaveInstalledUnverified));
            return result;
        }
        catch (OperationCanceledException)
        {
            publication.MarkUnknown(cancelled: true);
            _messenger.Send(new StatusChangedMessage("save-cancelled", operation.Generation, DesktopStatusGuidance.Cancelled));
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            publication.MarkUnknown(failed: true);
            _messenger.Send(new StatusChangedMessage("save-failed:IoFailure", operation.Generation, DesktopStatusGuidance.SaveFailed));
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
                _messenger.Send(new StatusChangedMessage("bundle-save-blocked:UnsupportedPlatform", operation.Generation, DesktopStatusGuidance.SaveFailed));
                return null;
            }
            if (!operation.IsCurrent || _form.InputRevision != inputRevision) return null;
            DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(_form.Definition, selection, _form.Snapshot(), operation.Token);
            if (!bundle.Succeeded)
            {
                _messenger.Send(new StatusChangedMessage("bundle-invalid:" +
                    string.Join(",", bundle.Diagnostics.Select(item => item.Code)), operation.Generation, DesktopStatusGuidance.PreparationInvalid));
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
                        + (result.CleanupFailed ? ";cleanup-failed" : string.Empty), operation.Generation,
                        result.Succeeded ? DesktopStatusGuidance.General
                            : result.Outcomes.Any(item => item.Result is { InstallationEstablished: true })
                                ? DesktopStatusGuidance.BundleSavePartial : DesktopStatusGuidance.SaveFailed));
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
            _messenger.Send(new StatusChangedMessage("bundle-save-cancelled", operation.Generation, DesktopStatusGuidance.Cancelled));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("bundle-save-blocked:IoFailure", operation.Generation, DesktopStatusGuidance.SaveFailed));
            return null;
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
        if (_form is not null)
        {
            _form.ImportComparisonChanged -= ImportComparisonChanged;
            _form.ViewChanged -= LocaleChanged;
        }
        ClearPreparation();
        _form = null;
        _session.Dispose();
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(ArtifactPublisherViewModel);
}
