namespace Configurator.Desktop.ViewModels;

using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Presentation;

public sealed partial class ArtifactPublisherViewModel
{
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
        foreach (ArtifactSelectionViewModel selection in _artifactSelections) selection.SetLocale(_form?.Locale ?? "en");
        foreach (string property in new[] { nameof(ImportPurposes), nameof(ImportPurposeLabel), nameof(ReviewFileLabel),
            nameof(ImportLimitHelp), nameof(ImportComparisonTitle), nameof(FillFieldsLabel), nameof(DismissImportLabel),
            nameof(PreviousImportPageLabel), nameof(NextImportPageLabel), nameof(ImportSchemaHelp), nameof(ImportFacts), nameof(ImportAdoption),
            nameof(ProtectedCapability), nameof(ArtifactConsequences), nameof(PublicationSummary), nameof(PublicationHistoryText) })
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

}
