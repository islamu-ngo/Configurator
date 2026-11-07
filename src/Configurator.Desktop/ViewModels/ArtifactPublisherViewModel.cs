namespace Configurator.Desktop.ViewModels;

using System.Security.Cryptography;
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
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> _save;
    private readonly Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
        Task<ProtectedArtifactBundleResult>> _publishBundle;
    private DefinitionFormModel? _form;
    private PresentationOutcome? _preparedOutcome;
    private string[]? _preparedBundleSelection;
    private long _preparedBundleInputRevision;
    private readonly HashSet<string> _selectedArtifactIds = new(StringComparer.Ordinal);
    private ArtifactSelectionViewModel[] _artifactSelections = [];
    private readonly object _receiptGate = new();
    private readonly Queue<ProtectedArtifactPublicationReceipt> _publicationHistory = new();
    private DesktopPublicationOperation? _publication;
    private long _generation;
    private bool _disposed;
    private bool _replacingForm;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectionCommand))]
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
    [NotifyPropertyChangedFor(nameof(SelectedArtifact))]
    [NotifyPropertyChangedFor(nameof(ArtifactConsequences))]
    private string? _artifactId;

    public ArtifactPublisherViewModel(IMessenger messenger, IStorageDialogService dialogs,
        IProtectedArtifactWriter writer)
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
                writer, artifacts, directory, observe, token))
    {
    }

    internal ArtifactPublisherViewModel(IMessenger messenger, IStorageDialogService dialogs,
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>> save,
        string outputDirectory, bool protectedOutputAvailable,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null)
    {
        _messenger = messenger;
        _dialogs = dialogs;
        _save = save;
        _publishBundle = publishBundle ?? ((artifacts, directory, observe, token) =>
            ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(), artifacts, directory, observe, token));
        _outputDirectory = outputDirectory;
        ProtectedSaveAvailable = protectedOutputAvailable;
        messenger.Register<ArtifactPublisherViewModel, FormWorkspaceChangedMessage>(this,
            static (publisher, message) => publisher.ReplaceForm(message.Form));
        messenger.Register<ArtifactPublisherViewModel, WorkspaceInvalidatedMessage>(this,
            static (publisher, message) =>
            {
                publisher._generation = message.Generation;
                publisher.ClearPreparation();
                publisher.RefreshEligibleArtifacts();
            });
        messenger.Register<ArtifactPublisherViewModel, OperationStateMessage>(this,
            static (publisher, message) =>
            {
                publisher.IsBusy = message.IsBusy;
                publisher.IsSaving = message.IsBusy && message.Kind == "save";
                publisher.IsSelecting = message.IsBusy && message.Kind == "selection";
                publisher.NotifyCommands();
            });
    }

    public IReadOnlyList<ArtifactDefinition> Artifacts => _form?.EligibleArtifacts ?? [];
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
    public string ArtifactConsequences => string.Join("\n", Artifacts.Where(artifact => _selectedArtifactIds.Contains(artifact.Id))
        .Select(artifact => artifact.Title + ": " + Strings.Format("ArtifactConsequences",
            artifact.Format.ToToken(), artifact.ApplySemantics.ToToken(), artifact.Restart.ToToken())));
    public DefinitionPreparationMetadata? Preparation => _preparedOutcome?.CoreResult as DefinitionPreparationMetadata;
    public bool CanSelectPath => !_disposed && !IsBusy;
    public bool CanPrepare => !_disposed && !IsBusy && _form is not null && SelectedArtifactIds.Count > 0;
    public bool IsSingleSelection => SelectedArtifactIds.Count == 1;
    public bool CanSaveSelection => CanSave || CanSaveBundle;
    public bool CanSave => !_disposed && !IsBusy && ProtectedSaveAvailable &&
        Preparation is { Succeeded: true } && _preparedOutcome is { Output.IsEmpty: false };
    public bool CanSaveBundle => !_disposed && !IsBusy && ProtectedSaveAvailable && _preparedBundleSelection is not null;
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

    private DesktopPublicationOperation BeginPublication(IEnumerable<string> artifactIds, byte[]? bytes = null)
    {
        SynchronizationContext? owner = SynchronizationContext.Current;
        var publication = new DesktopPublicationOperation(artifactIds, receipt => PublicationChanged(receipt, owner), bytes);
        lock (_receiptGate) _publication = publication;
        NotifyPublication(owner);
        return publication;
    }

    private void PublicationChanged(ProtectedArtifactPublicationReceipt receipt, SynchronizationContext? owner)
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
        NotifyPublication(owner);
    }

    private void NotifyPublication(SynchronizationContext? owner)
    {
        if (_disposed) return;
        if (owner is not null && !ReferenceEquals(SynchronizationContext.Current, owner))
        {
            owner.Post(_ => RaisePublicationChanged(), null);
            return;
        }
        RaisePublicationChanged();
    }

    private void RaisePublicationChanged()
    {
        if (_disposed) return;
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
    partial void OnArtifactIdChanged(string? value)
    {
        NotifyCommands();
    }

    /// <summary>Replaces choices before selecting their default, so two-way picker reset cannot erase the new selection.</summary>
    private void ReplaceForm(DefinitionFormModel? form)
    {
        if (_form is not null)
        {
            _form.ViewChanged -= LocaleChanged;
        }
        _form = form;
        ClearPreparation();
        if (form is not null)
        {
            form.ViewChanged += LocaleChanged;
        }
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
        _artifactSelections = Artifacts.Select(item => new ArtifactSelectionViewModel(item, _form?.Locale ?? "en",
            _selectedArtifactIds.Contains(item.Id), selected => SelectArtifact(item.Id, selected),
            () => OutputPreparationStatus(item.Id))).ToArray();
        OnPropertyChanged(nameof(ArtifactSelections));
        OnPropertyChanged(nameof(SelectedArtifactIds));
        OnPropertyChanged(nameof(ArtifactId));
        OnPropertyChanged(nameof(SelectedArtifact));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(ArtifactConsequences));
        LocaleChanged(form, EventArgs.Empty);
        NotifyCommands();
    }

    private void LocaleChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        foreach (ArtifactSelectionViewModel selection in _artifactSelections) selection.SetLocale(_form?.Locale ?? "en");
        foreach (string property in new[] { nameof(ProtectedCapability), nameof(ArtifactConsequences),
            nameof(PublicationSummary), nameof(PublicationHistoryText) })
            OnPropertyChanged(property);
    }

    private void RefreshEligibleArtifacts()
    {
        IReadOnlyList<ArtifactDefinition> artifacts = Artifacts;
        if (_artifactSelections.Select(item => item.Id).SequenceEqual(artifacts.Select(item => item.Id))) return;
        string? previous = ArtifactId;
        string previousFileName = FileName;
        bool hadSelection = _selectedArtifactIds.Count > 0;
        _selectedArtifactIds.IntersectWith(artifacts.Select(item => item.Id));
        _replacingForm = true;
        try
        {
            OnPropertyChanged(nameof(Artifacts));
            ArtifactId = artifacts.FirstOrDefault(item => item.Id == previous)?.Id
                ?? artifacts.FirstOrDefault(item => item.Id == _form?.ConfigurationContext?.PrimaryArtifactId)?.Id
                ?? (artifacts.Count > 0 ? artifacts[0].Id : null);
            if (ArtifactId == previous) FileName = previousFileName;
        }
        finally { _replacingForm = false; }
        if ((hadSelection || _artifactSelections.Length == 0) && _selectedArtifactIds.Count == 0 && ArtifactId is not null)
        {
            _selectedArtifactIds.Add(ArtifactId);
            FileName = artifacts.Single(item => item.Id == ArtifactId).FileName;
        }
        _artifactSelections = artifacts.Select(item => new ArtifactSelectionViewModel(item, _form?.Locale ?? "en",
            _selectedArtifactIds.Contains(item.Id), selected => SelectArtifact(item.Id, selected),
            () => OutputPreparationStatus(item.Id))).ToArray();
        OnPropertyChanged(nameof(ArtifactSelections));
        OnPropertyChanged(nameof(SelectedArtifactIds));
        OnPropertyChanged(nameof(SelectedArtifact));
        NotifyCommands();
    }

    public void SelectArtifact(string artifactId, bool selected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Artifacts.All(item => item.Id != artifactId)) return;
        if (!(selected ? _selectedArtifactIds.Add(artifactId) : _selectedArtifactIds.Remove(artifactId))) return;
        _artifactSelections.First(item => item.Id == artifactId).SetSelected(selected);
        _messenger.Send(new InputsInvalidatedMessage());
        if (IsSingleSelection) FileName = Artifacts.Single(item => item.Id == SelectedArtifactIds[0]).FileName;
        OnPropertyChanged(nameof(SelectedArtifactIds));
        NotifyCommands();
    }

    private bool Validate()
    {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("owner-validation-start");
#endif
        bool ready = false;
        _messenger.Send(new FormValidationRequestedMessage(value => ready = value));
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("owner-validation-end");
#endif
        return ready;
    }

    internal Task<DesktopPreparationOutcome> PrepareAsync(CancellationToken cancellationToken = default) =>
        PrepareOutputAsync(bundle: false, cancellationToken);

    internal Task<DesktopPreparationOutcome> PrepareBundleAsync(CancellationToken cancellationToken = default) =>
        PrepareOutputAsync(bundle: true, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanPrepare))]
    public Task<DesktopPreparationOutcome> PrepareSelectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string[] selection = SelectedArtifactIds.ToArray();
        if (selection.Length == 0) return Task.FromResult(DesktopPreparationOutcome.Invalid);
        return PrepareOutputAsync(selection.Length > 1, cancellationToken, selection);
    }

    [RelayCommand(CanExecute = nameof(CanSaveSelection))]
    public async Task<ProtectedArtifactPublicationReceipt?> SaveSelectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy || !CanSaveSelection) return null;
        DesktopPublicationOperation? publication = null;
        Task saving = _preparedBundleSelection is not null
            ? SaveBundleAsync(started => publication = started, cancellationToken)
            : SaveAsync(started => publication = started, cancellationToken);
        await saving.ConfigureAwait(false);
        return publication?.Receipt;
    }

    private Task<DesktopPreparationOutcome> PrepareOutputAsync(bool bundle, CancellationToken cancellationToken,
        string[]? selected = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy) return Task.FromResult(DesktopPreparationOutcome.Busy);
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(DesktopPreparationOutcome.Cancelled);
        _messenger.Send(new InputsInvalidatedMessage());
        if (!Validate() || _form is null || (!bundle && selected is null && ArtifactId is null))
            return Task.FromResult(DesktopPreparationOutcome.Invalid);
        DefinitionFormModel form = _form;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-start");
#endif
        Definition definition = form.Definition;
        AnswerSnapshot answers = form.Snapshot();
        string[] selection = selected ?? (bundle ? SelectedArtifactIds.ToArray() : [ArtifactId!]);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-end");
#endif
        DesktopOperation? operation = DesktopOperation.Begin(_messenger, "prepare", cancellationToken);
        if (operation is null) return Task.FromResult(DesktopPreparationOutcome.Busy);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("dispatch");
#endif
        return Task.Run(async () =>
        {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            Configurator.Engineering.MeasureNativePreparation.Mark("worker-start");
#endif
            PresentationOutcome? prepared = null;
            DefinitionBundleResult? composed = null;
            Action? failure = null;
            try
            {
                bool succeeded;
                IReadOnlyList<string> diagnostics;
                if (bundle)
                {
                    composed = DefinitionBundleComposer.Compose(definition, selection, answers, operation.Token);
                    succeeded = composed.Succeeded;
                    diagnostics = composed.Diagnostics.Select(item => item.Code).ToArray();
                }
                else
                {
                    prepared = await new DefinitionPresentationOperation(definition, selection[0], answers)
                        .ExecuteAsync(operation.Token).ConfigureAwait(false);
                    var metadata = (DefinitionPreparationMetadata)prepared.CoreResult;
                    succeeded = metadata.Succeeded;
                    diagnostics = metadata.DiagnosticCodes;
                }
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                Configurator.Engineering.MeasureNativePreparation.Mark("composition-end");
#endif
                bool accepted = false;
                bool current = await operation.TryApplyAsync(() =>
                {
                    if (_disposed || !ReferenceEquals(_form, form) || form.InputRevision != answers.InputRevision) return;
                    try
                    {
                        accepted = true;
                        if (bundle && succeeded)
                        {
                            _preparedBundleSelection = selection;
                            _preparedBundleInputRevision = answers.InputRevision;
                        }
                        else if (!bundle)
                        {
                            _preparedOutcome = prepared;
                            prepared = null;
                        }
                        _messenger.Send(new StatusChangedMessage(succeeded
                            ? bundle ? $"bundle-selected:{selection.Length}"
                                : $"artifact-ready:{Preparation!.ArtifactId}:{Preparation.Digest?.Value}"
                            : (bundle ? "bundle-invalid:" : "preparation-invalid:") + string.Join(",", diagnostics),
                            operation.Generation, succeeded ? DesktopStatusGuidance.General : DesktopStatusGuidance.PreparationInvalid));
                        NotifyCommands();
                    }
                    catch (Exception)
                    {
                        ClearPreparation();
                        throw;
                    }
                }).ConfigureAwait(false);
                return current && accepted
                    ? succeeded ? DesktopPreparationOutcome.Prepared : DesktopPreparationOutcome.Invalid
                    : operation.WasCancelled ? DesktopPreparationOutcome.Cancelled : DesktopPreparationOutcome.Retired;
            }
            catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
            {
                return operation.WasCancelled ? DesktopPreparationOutcome.Cancelled : DesktopPreparationOutcome.Retired;
            }
            catch (Exception)
            {
                failure = () => _messenger.Send(new StatusChangedMessage("preparation-fault", operation.Generation));
                return DesktopPreparationOutcome.Faulted;
            }
            finally
            {
                prepared?.Dispose();
                composed?.Dispose();
                operation.Finish(failure);
            }
        }, CancellationToken.None);
    }

    internal Task<ProtectedArtifactCommitResult> SaveAsync(Action<DesktopPublicationOperation>? started = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy || Preparation is not { Succeeded: true } || _preparedOutcome is null)
            return Task.FromResult(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest));
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-start");
#endif
        byte[] bytes = _preparedOutcome.Output.ToArray();
        string artifactId = Preparation.ArtifactId;
        string fileName = FileName;
        string directory = OutputDirectory;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-end");
#endif
        ClearPreparation();
        DesktopOperation? operation = DesktopOperation.Begin(_messenger, "save", cancellationToken);
        if (operation is null)
        {
            CryptographicOperations.ZeroMemory(bytes);
            return Task.FromResult(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest));
        }
        DesktopPublicationOperation publication = BeginPublication([artifactId], bytes);
        started?.Invoke(publication);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("dispatch");
#endif
        return Task.Run(async () =>
        {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            Configurator.Engineering.MeasureNativePreparation.Mark("worker-start");
            Configurator.Engineering.MeasureNativePreparation.Mark("consumer-start");
#endif
            Action? completion = null;
            try
            {
                ProtectedArtifactCommitResult result = !ProtectedSaveAvailable
                    ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsupportedPlatform)
                    : !SafeFileName(fileName) || string.IsNullOrWhiteSpace(directory)
                        ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsafeTarget)
                    : operation.Token.IsCancellationRequested
                        ? ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest)
                    : await _save(Path.Combine(directory, fileName), bytes, operation.Token).ConfigureAwait(false);
                publication.Complete([new(artifactId, result)], operation.Token.IsCancellationRequested
                    ? ProtectedArtifactBundleCompletionCode.Cancelled : ProtectedArtifactBundleCompletionCode.Completed);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                Configurator.Engineering.MeasureNativePreparation.Mark("consumer-end");
#endif
                completion = () => _messenger.Send(new StatusChangedMessage(result.Succeeded ? "save-complete"
                    : "save-" + FormatCommitResult(result), operation.Generation,
                    result.Succeeded ? DesktopStatusGuidance.General : !result.InstallationEstablished
                        ? DesktopStatusGuidance.SaveFailed : result.VerificationSucceeded
                            ? DesktopStatusGuidance.SaveInstalledVerified : DesktopStatusGuidance.SaveInstalledUnverified));
                return result;
            }
            catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
            {
                publication.MarkUnknown(cancelled: true);
                return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                publication.MarkUnknown(failed: true);
                completion = () => _messenger.Send(new StatusChangedMessage("save-failed:IoFailure",
                    operation.Generation, DesktopStatusGuidance.SaveFailed));
                return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure);
            }
            catch (Exception)
            {
                publication.MarkUnknown(failed: true);
                throw;
            }
            finally
            {
                try { publication.Dispose(); }
                finally { operation.Finish(completion); }
            }
        }, CancellationToken.None);
    }

    internal Task<ProtectedArtifactBundleResult?> SaveBundleAsync(Action<DesktopPublicationOperation>? started = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy || _preparedBundleSelection is null || _form is null)
            return Task.FromResult<ProtectedArtifactBundleResult?>(null);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-start");
#endif
        string[] selection = _preparedBundleSelection;
        long inputRevision = _preparedBundleInputRevision;
        string directory = OutputDirectory;
        Definition definition = _form.Definition;
        AnswerSnapshot answers = _form.Snapshot();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("capture-end");
#endif
        _preparedBundleSelection = null;
        DesktopOperation? operation = DesktopOperation.Begin(_messenger, "save", cancellationToken);
        if (operation is null) return Task.FromResult<ProtectedArtifactBundleResult?>(null);
        DesktopPublicationOperation publication = BeginPublication(selection);
        started?.Invoke(publication);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.MeasureNativePreparation.Mark("dispatch");
#endif
        return Task.Run<ProtectedArtifactBundleResult?>(async () =>
        {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            Configurator.Engineering.MeasureNativePreparation.Mark("worker-start");
#endif
            Action? completion = null;
            bool consumerStarted = false;
            try
            {
                if (!ProtectedSaveAvailable)
                {
                    publication.Complete(selection.Select(id => new ProtectedArtifactBundleOutcome(id, null)),
                        ProtectedArtifactBundleCompletionCode.Completed);
                    completion = () => _messenger.Send(new StatusChangedMessage("bundle-save-blocked:UnsupportedPlatform",
                        operation.Generation, DesktopStatusGuidance.SaveFailed));
                    return null;
                }
                operation.Token.ThrowIfCancellationRequested();
                if (answers.InputRevision != inputRevision) return null;
                using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(definition, selection, answers, operation.Token);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                Configurator.Engineering.MeasureNativePreparation.Mark("composition-end");
#endif
                if (!bundle.Succeeded)
                {
                    string codes = string.Join(",", bundle.Diagnostics.Select(item => item.Code));
                    publication.Complete(selection.Select(id => new ProtectedArtifactBundleOutcome(id, null)),
                        ProtectedArtifactBundleCompletionCode.Completed);
                    completion = () => _messenger.Send(new StatusChangedMessage("bundle-invalid:" + codes,
                        operation.Generation, DesktopStatusGuidance.PreparationInvalid));
                    return null;
                }
                operation.Token.ThrowIfCancellationRequested();
                consumerStarted = true;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                Configurator.Engineering.MeasureNativePreparation.Mark("consumer-start");
#endif
                ProtectedArtifactBundleResult result = await _publishBundle(bundle.Artifacts, directory,
                    publication.Observe, operation.Token).ConfigureAwait(false);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                Configurator.Engineering.MeasureNativePreparation.Mark("consumer-end");
#endif
                publication.Complete(result.Outcomes, result.CompletionCode, result.CleanupFailed);
                completion = () => _messenger.Send(new StatusChangedMessage(result.Succeeded ? "bundle-save-complete" :
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
            catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
            {
                if (consumerStarted) publication.MarkUnknown(cancelled: true);
                else publication.Complete(selection.Select(id => new ProtectedArtifactBundleOutcome(id, null)),
                    ProtectedArtifactBundleCompletionCode.Cancelled);
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                publication.MarkUnknown(failed: true);
                completion = () => _messenger.Send(new StatusChangedMessage("bundle-save-blocked:IoFailure",
                    operation.Generation, DesktopStatusGuidance.SaveFailed));
                return null;
            }
            catch (Exception)
            {
                publication.MarkUnknown(failed: true);
                throw;
            }
            finally
            {
                try { publication.Dispose(); }
                finally { operation.Finish(completion); }
            }
        }, CancellationToken.None);
    }

    private void ClearPreparation()
    {
        _preparedBundleSelection = null;
        _preparedOutcome?.Dispose();
        _preparedOutcome = null;
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        foreach (ArtifactSelectionViewModel artifact in _artifactSelections) artifact.RefreshPreparation();
        OnPropertyChanged(nameof(CanPrepare));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveBundle));
        OnPropertyChanged(nameof(CanSaveSelection));
        OnPropertyChanged(nameof(IsSingleSelection));
        OnPropertyChanged(nameof(ArtifactConsequences));
        OnPropertyChanged(nameof(CanSelectPath));
        OnPropertyChanged(nameof(Preparation));
        PrepareSelectionCommand.NotifyCanExecuteChanged();
        SaveSelectionCommand.NotifyCanExecuteChanged();
        SelectOutputDirectoryCommand.NotifyCanExecuteChanged();
    }

    private string OutputPreparationStatus(string artifactId)
    {
        if (Preparation is { } metadata && metadata.ArtifactId == artifactId)
            return Strings[!metadata.Succeeded ? "OutputPreparationInvalid"
                : metadata.IsDraft || metadata.Completions.Count > 0 ? "OutputPreparedIncomplete" : "OutputPreparedComplete"];
        return Strings[_preparedBundleSelection?.Contains(artifactId, StringComparer.Ordinal) == true
            ? "OutputPreparedComplete" : "OutputNotEvaluated"];
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
        if (_form is not null)
        {
            _form.ViewChanged -= LocaleChanged;
        }
        ClearPreparation();
        _form = null;
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(ArtifactPublisherViewModel);
}

/// <summary>Distinguishes invalid input from cancellation, retired work, and an implementation fault.</summary>
public enum DesktopPreparationOutcome
{
    Prepared,
    Invalid,
    Cancelled,
    Retired,
    Faulted,
    Busy
}
