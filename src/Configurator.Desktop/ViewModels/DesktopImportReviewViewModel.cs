namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

/// <summary>Owns Desktop import acquisition and review display; the shared form retains the only private proposal.</summary>
public sealed partial class DesktopImportReviewViewModel : ObservableObject, IDisposable
{
    private const int ImportPageSize = 32;
    private readonly IMessenger _messenger;
    private readonly IStorageDialogService _dialogs;
    private readonly ArtifactPublisherViewModel _publisher;
    private readonly Func<string, Stream> _openImport;
    private readonly Func<CancellationToken, ValueTask> _cooperativeYield;
    private DefinitionFormModel? _form;
    private long _generation;
    private long _reviewGeneration;
    private bool _disposed;
    private bool _comparingInput;
    private int _importPage;
    private ImportFindingViewModel[] _importFindings = [];
    private DesktopStrings Strings => new(_form?.Locale);

    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private int _importPurposeIndex;
    [ObservableProperty]
    private string _existingFilePath = string.Empty;
    [ObservableProperty]
    private string _importExplanation = string.Empty;

    public DesktopImportReviewViewModel(IMessenger messenger, IStorageDialogService dialogs,
        ArtifactPublisherViewModel publisher, Func<CancellationToken, ValueTask>? cooperativeYield = null)
        : this(messenger, dialogs, publisher,
            path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous),
            cooperativeYield)
    {
    }

    internal DesktopImportReviewViewModel(IMessenger messenger, IStorageDialogService dialogs,
        ArtifactPublisherViewModel publisher, Func<string, Stream> openImport,
        Func<CancellationToken, ValueTask>? cooperativeYield = null)
    {
        _messenger = messenger;
        _dialogs = dialogs;
        _publisher = publisher;
        _openImport = openImport;
        _cooperativeYield = cooperativeYield ?? YieldDispatcherAsync;
        FillImportFieldsCommand = new AsyncRelayCommand(
            async token => { await FillImportFieldsAsync(token); }, () => CanFillImportFields);
        publisher.PropertyChanged += PublisherChanged;
        messenger.Register<DesktopImportReviewViewModel, FormWorkspaceChangedMessage>(this,
            static (review, message) => review.ReplaceForm(message.Form));
        messenger.Register<DesktopImportReviewViewModel, WorkspaceInvalidatedMessage>(this,
            static (review, message) =>
            {
                review._generation = message.Generation;
                review.ClearImportComparison();
                review.NotifyCommands();
            });
        messenger.Register<DesktopImportReviewViewModel, OperationStateMessage>(this,
            static (review, message) =>
            {
                if (message.IsBusy && message.Kind != "fill-import") review.ClearImportComparison();
                review.IsBusy = message.IsBusy;
            });
    }

    public bool CanImport => !_disposed && !IsBusy && _form is not null &&
        _publisher.Artifacts.Any(artifact => artifact.Id == _publisher.ArtifactId);
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
    public IAsyncRelayCommand FillImportFieldsCommand { get; }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnExistingFilePathChanged(string value)
    {
        ClearImportComparison();
        if (IsBusy && !_comparingInput) _messenger.Send(new InputsInvalidatedMessage());
    }
    partial void OnImportPurposeIndexChanged(int value) => ClearImportComparison();

    private void PublisherChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ArtifactPublisherViewModel.ArtifactId) or nameof(ArtifactPublisherViewModel.Artifacts))
        {
            ClearImportComparison();
            NotifyCommands();
        }
    }

    private void ReplaceForm(DefinitionFormModel? form)
    {
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
        LocaleChanged(form, EventArgs.Empty);
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanFillImportFields));
        ImportFileCommand.NotifyCanExecuteChanged();
        SelectImportCommand.NotifyCanExecuteChanged();
        FillImportFieldsCommand.NotifyCanExecuteChanged();
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
        if (!CanImport || _form is null || _publisher.ArtifactId is null) return false;
        DefinitionFormModel form = _form;
        string artifactId = _publisher.ArtifactId;
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
                    ? "import-unreadable" : comparison.Code, operation.Generation, ImportGuidance(comparison.Code)));
                return comparison.Succeeded;
            }
            if (!comparison.Succeeded)
            {
                _messenger.Send(new StatusChangedMessage(comparison.Code == "import-read-failed"
                    ? "import-unreadable" : comparison.Code, operation.Generation, ImportGuidance(comparison.Code)));
                return false;
            }
            return await AdoptImportComparisonAsync(form, operation);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _messenger.Send(new StatusChangedMessage("import-unreadable", operation.Generation, DesktopStatusGuidance.ImportUnreadable));
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

    private void LocaleChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        foreach (string property in new[] { nameof(ImportPurposes), nameof(ImportPurposeLabel), nameof(ReviewFileLabel),
            nameof(ImportLimitHelp), nameof(ImportComparisonTitle), nameof(FillFieldsLabel), nameof(DismissImportLabel),
            nameof(PreviousImportPageLabel), nameof(NextImportPageLabel), nameof(ImportSchemaHelp), nameof(ImportFacts), nameof(ImportAdoption),
            nameof(ImportPageText) })
            OnPropertyChanged(property);
        RefreshImportPage();
    }

    private void RefreshImportPage()
    {
        _importFindings = _form?.ImportComparison?.Findings.Skip(_importPage * ImportPageSize)
            .Take(ImportPageSize).Select(finding => new ImportFindingViewModel(finding, _form.Locale,
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
            _messenger.Send(new StatusChangedMessage(result.Code, generation, ImportGuidance(result.Code)));
            if (!_disposed && _generation == generation && ReferenceEquals(_form, form))
                _messenger.Send(new FormFocusRequestedMessage());
        }
        else if (!result.Succeeded && operation.IsCurrent)
            _messenger.Send(new StatusChangedMessage(result.Code, operation.Generation, ImportGuidance(result.Code)));
        return result.Succeeded;
    }

    private static DesktopStatusGuidance ImportGuidance(string code) => code switch
    {
        "import-read-failed" => DesktopStatusGuidance.ImportUnreadable,
        "import-unsupported" or "inverse-unsupported" or "inverse-ambiguous" => DesktopStatusGuidance.ImportUnsupported,
        "answer-conflict" => DesktopStatusGuidance.AnswerConflict,
        "input-invalid" or "input-ambiguous" or "import-field-invalid" => DesktopStatusGuidance.InputInvalid,
        _ => DesktopStatusGuidance.General
    };

    internal static async ValueTask YieldDispatcherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.NativeImportComparisonProbe.Checkpoint?.Invoke();
#endif
        await Dispatcher.Yield(DispatcherPriority.Background);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.NativeImportComparisonProbe.Checkpoint?.Invoke();
#endif
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

    private string Text(string key) => Strings.Read(key);

    [RelayCommand(CanExecute = nameof(CanImport))]
    public async Task<bool> SelectImportAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanImport || _form is null || _publisher.ArtifactId is null) return false;
        DefinitionFormModel form = _form;
        string artifact = _publisher.ArtifactId;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _publisher.PropertyChanged -= PublisherChanged;
        ReplaceForm(null);
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(DesktopImportReviewViewModel);
}
