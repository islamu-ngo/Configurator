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
    internal void Prepare()
    {
        if (_importOperation.IsBusy || _operation.IsPublishing || Volatile.Read(ref _closeRequested) != 0) return;
        if (!CommitField()) return;
        try
        {
            bool ready = _operation.PrepareAsync().GetAwaiter().GetResult();
            _status.Text = ready ? TerminalStrings.Get("PreviewReady") + " "
                + _operation.Preparation!.ArtifactCount + " " + TerminalStrings.Get("Selected")
                : TerminalStrings.Get("DraftInvalid");
        }
        catch (DefinitionException) { Status("DraftInvalid"); }
        RefreshCommands();
    }

    internal void Save()
    {
        if (_importOperation.IsBusy || _operation.IsPublishing || Volatile.Read(ref _closeRequested) != 0) return;
        if (!_operation.IsPrepared) { Status("InvalidOutput"); return; }
        ExitCode = 4;
        _receipt = null;
        Status("Writing");
        Task execution = _workspace.ExecuteAsync(Guid.CreateVersion7());
        _saveObservation = ObserveSaveAsync(execution, _dispatch, _owner, _callbackGeneration);
        Observe(_saveObservation);
        UpdatePublication();
        RefreshCommands();
    }

    private static async Task ObserveSaveAsync(Task execution, Action<Action> dispatch,
        WeakReference<TerminalWindow> owner, int generation)
    {
        try { await execution.ConfigureAwait(false); }
        catch (Exception) when (execution.IsFaulted || execution.IsCanceled)
        {
            TryDispatch(dispatch, () =>
            {
                if (!owner.TryGetTarget(out TerminalWindow? window) || window._disposed
                    || window._callbackGeneration != generation || Volatile.Read(ref window._closeRequested) != 0) return;
                window.UpdatePublication();
                if (window._receipt is null)
                    window._status.Text = TerminalStrings.Get("InvalidOutput") + " terminal-operation-failed";
                window.RefreshCommands();
            });
        }
    }

    private void PublicationChanged(object? sender, EventArgs args)
    {
        if (Interlocked.Exchange(ref _publicationRefreshQueued, 1) != 0) return;
        DispatchToOwner(static window =>
        {
            Interlocked.Exchange(ref window._publicationRefreshQueued, 0);
            if (Volatile.Read(ref window._closeRequested) != 0) return;
            window.UpdatePublication();
            window.RefreshCommands();
        });
    }

    private IReadOnlyList<ProtectedArtifactPublicationReceipt> PublicationReceipts()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> history = _operation.PublicationHistory;
        return _operation.CurrentPublication is { } current ? [.. history, current] : history;
    }

    private void UpdatePublication()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> receipts = PublicationReceipts();
        ProtectedArtifactPublicationReceipt? receipt = receipts.FirstOrDefault(item => item.AttemptId == _receipt?.AttemptId)
            ?? (receipts.Count == 0 ? null : receipts[^1]);
        if (receipt is null) return;
        if (_receipt?.AttemptId != receipt.AttemptId) _outcomeIndex = 0;
        _receipt = receipt;
        ShowOutcome();
    }

    private void UpdateResult()
    {
        if (_workspace.Result is TerminalArtifactResult result)
        {
            ExitCode = result.Succeeded ? 0 : 4;
            UpdatePublication();
            _loading = true;
            _masked.ClearSensitiveState();
            _importPath.Text = string.Empty;
            _loading = false;
            _operation.ClearPrivateState();
            ShowField();
        }
        RefreshCommands();
    }

    internal void PresentReceipt(ProtectedArtifactPublicationReceipt receipt)
    {
        _receipt = receipt;
        _outcomeIndex = 0;
        ShowOutcome();
    }

    private void NextAttempt()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> receipts = PublicationReceipts();
        if (receipts.Count == 0) return;
        int index = receipts.ToList().FindIndex(item => item.AttemptId == _receipt?.AttemptId);
        PresentReceipt(receipts[(index + receipts.Count - 1) % receipts.Count]);
    }

    private void NextOutcome()
    {
        if (_receipt is null || _receipt.Outcomes.Count == 0) return;
        _outcomeIndex = (_outcomeIndex + 1) % _receipt.Outcomes.Count;
        ShowOutcome();
    }

    private void ShowOutcome()
    {
        if (_receipt is null) return;
        _status.Text = TerminalStrings.FormatReceipt(_receipt, _outcomeIndex);
        _nextOutcome.Visible = !_tooSmall && _receipt.Outcomes.Count > 1;
        _nextAttempt.Visible = !_tooSmall;
        _status.SetNeedsDraw();
    }

}
