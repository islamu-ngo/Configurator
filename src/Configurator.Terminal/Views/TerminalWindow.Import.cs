namespace Configurator.Terminal;

using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Text;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

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
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.NativeImportComparisonProbe.Checkpoint?.Invoke();
#endif
        await Task.Yield();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        Configurator.Engineering.NativeImportComparisonProbe.Checkpoint?.Invoke();
#endif
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
        string compatibility = TerminalStrings.Get("Comparison" + report.Compatibility);
        string coverage = TerminalStrings.Get(report.CoverageComplete ? "CoverageComplete" : "CoverageIncomplete");
        string inventory = TerminalStrings.Get(report.InventoryComplete ? "InventoryComplete" : "InventoryIncomplete");
        _help.Text = Safe($"{compatibility} | {coverage} | {inventory} | {report.RetainedFindingCount}");
        if (_comparisonIndex >= 0 && report.Findings.Count > 0)
        {
            _comparisonIndex %= report.Findings.Count;
            DefinitionImportFinding finding = report.Findings[_comparisonIndex];
            DefinitionImportSchemaExplanation? explanation = form.ExplainImportFinding(finding);
            string severity = TerminalStrings.Get(finding.Kind is DefinitionImportFindingKind.Unassessed
                or DefinitionImportFindingKind.ContextDerivedValue ? "FindingInformation" : "FindingError");
            _help.Text += "\n" + Safe($"{_comparisonIndex + 1}/{report.RetainedFindingCount} [{severity}] " +
                TerminalStrings.Get("Finding" + finding.Kind) + " " + finding.Code +
                (explanation is null ? "" : " | " + explanation.Title));
        }
        string eligibility = TerminalStrings.Get(form.CanAdoptImportComparison ? "AutofillEligible" : "AutofillRefused");
        _status.Text = Safe(eligibility + " " + form.ImportAutofillCode +
            (form.CanAdoptImportComparison ? "" : " " + TerminalStrings.Get("ComparisonRecovery")));
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
            else _status.Text = TerminalStrings.Get("ImportFailed") + " " + result.Code;
        }
        _status.ClearHistoryChanges();
        RefreshCommands();
    }

}
