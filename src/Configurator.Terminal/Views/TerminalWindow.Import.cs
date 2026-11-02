namespace Configurator.Terminal;

using Configurator.Core.Definitions;
using Configurator.Presentation;

internal sealed partial class TerminalWindow
{
    private void ImportSelected() => StartImport(compare: true);

    private void FillFields()
    {
        if (_importOperation.IsBusy || _workspace.IsBusy || _operation.IsPublishing ||
            Volatile.Read(ref _closeRequested) != 0) return;
        if (_draftEdited) { Status("DraftInvalid"); return; }
        if (_operation.Form.ImportComparison is null)
        {
            StartImport(compare: false);
            return;
        }
        Status("ImportAdopting");
        if (!_importOperation.StartAdoption(_operation.ArtifactId)) return;
        RefreshCommands();
    }

    internal static async ValueTask YieldToApplicationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Suspend the current slice without waiting for the terminal's timer cadence.
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void StartImport(bool compare)
    {
        if (_importOperation.IsBusy || _workspace.IsBusy || _operation.IsPublishing ||
            Volatile.Read(ref _closeRequested) != 0) return;
        if (!compare && _draftEdited) { Status("DraftInvalid"); return; }
        string path = _importPath.Text;
        _loading = true;
        _importPath.Text = string.Empty;
        _importPath.ClearHistoryChanges();
        _loading = false;
        _comparisonIndex = -1;
        if (!_importOperation.Start(path, _operation.ArtifactId, compare)) return;
        Status("ImportReading");
        RefreshCommands();
    }

    private void ImportComparisonChanged(object? sender, EventArgs args) =>
        DispatchToOwner(static window =>
        {
            if (window._operation.Form.ImportComparison is not null) window.ShowComparison();
            window.RefreshCommands();
        });

    private void NextComparisonFinding()
    {
        if (_operation.Form.ImportComparison is not { Findings.Count: > 0 } report) return;
        _comparisonIndex = (_comparisonIndex + 1) % report.Findings.Count;
        ShowComparison();
    }

    private void ShowComparison()
    {
        var form = _operation.Form;
        if (form.ImportComparison is not { } report) return;
        string compatibility = PresentationStrings.ImportCompatibility(report.Compatibility, form.Locale);
        string coverage = LocalizedText(report.CoverageComplete ? "CoverageComplete" : "CoverageIncomplete");
        string inventory = LocalizedText(report.InventoryComplete ? "InventoryComplete" : "InventoryIncomplete");
        _help.Text = Safe($"{compatibility} | {coverage} | {inventory} | {LocalizedNumber(report.RetainedFindingCount)}");
        if (_comparisonIndex >= 0 && report.Findings.Count > 0)
        {
            _comparisonIndex %= report.Findings.Count;
            DefinitionImportFinding finding = report.Findings[_comparisonIndex];
            DefinitionImportSchemaExplanation? explanation = form.ExplainImportFinding(finding);
            string severity = LocalizedText(finding.Kind is DefinitionImportFindingKind.Unassessed
                or DefinitionImportFindingKind.ContextDerivedValue ? "FindingInformation" : "FindingError");
            _help.Text += "\n" + Safe($"{LocalizedNumber(_comparisonIndex + 1)}/{LocalizedNumber(report.RetainedFindingCount)} [{severity}] " +
                PresentationStrings.ImportFinding(finding.Kind, form.Locale) +
                (explanation is null ? "" : " | " + explanation.Title));
        }
        string eligibility = LocalizedText(form.CanAdoptImportComparison ? "AutofillEligible" : "AutofillRefused");
        StatusCode = form.ImportAutofillCode;
        _status.Text = Safe(eligibility + " " + PresentationStrings.ImportAutofill(form.ImportAutofillCode, form.Locale) +
            (form.CanAdoptImportComparison ? "" : " " + LocalizedText("ComparisonRecovery")));
        _status.ClearHistoryChanges();
        _help.SetNeedsDraw();
        _status.SetNeedsDraw();
    }

    private void CancelImport()
    {
        bool pending = _importOperation.IsBusy;
        _importOperation.RequestCancellationAsync();
        if (pending) Status("ImportCancelled");
    }

    private void ImportSettled(object? sender, TerminalImportResult result)
    {
        if (_disposed || Volatile.Read(ref _closeRequested) != 0) return;
        if (result.IsCurrent)
        {
            if (result.Succeeded)
            {
                if (result.IsComparison) ShowComparison();
                else
                {
                    ShowField();
                    FocusInput();
                    Status("ImportReady");
                }
            }
            else
            {
                Status("ImportFailed");
                StatusCode = result.Code;
            }
        }
        _status.ClearHistoryChanges();
        RefreshCommands();
    }

}
