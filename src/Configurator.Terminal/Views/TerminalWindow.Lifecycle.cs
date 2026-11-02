namespace Configurator.Terminal;

using System.ComponentModel;
using Configurator.Artifacts;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;

internal sealed partial class TerminalWindow
{
    internal void ClearPrivateState()
    {
        ClearSecretDialog();
        _importOperation.RequestCancellationAsync();
        _loading = true;
        _masked.ClearSensitiveState();
        _importPath.Text = string.Empty;
        _importPath.ClearHistoryChanges();
        _operation.ClearPrivateState();
        _loading = false;
        ShowField();
        _value.ClearHistoryChanges();
        Status("PrivateCleared");
    }

    private void CloseOrCancel()
    {
        RequestStopFromSignal();
    }

    internal void RequestStopFromSignal()
    {
        _secret.Clear();
        if (Volatile.Read(ref _disposed) || Interlocked.Exchange(ref _closeRequested, 1) != 0) return;
        _secretCancellation.Cancel();
        _preparationCancellation.Cancel();
        Observe(_importOperation.RequestCancellationAsync());
        Observe(_workspace.RequestCancellationAsync());
        DispatchToOwner(static window => window.BeginClose());
    }

    private void BeginClose()
    {
        ClearPrivateState();
        foreach (View view in _normal) view.Enabled = false;
        Status("ShutdownWaiting");
        Task pending = Task.WhenAll(_importOperation.Completion, _workspace.WaitForPendingOperationsAsync(), _secretCompletion, _preparationCompletion);
        Observe(pending);
        _shutdown = FinishCloseAsync(pending, _timeProvider, _dispatch, _owner, _callbackGeneration);
        Observe(_shutdown);
    }

    private static async Task FinishCloseAsync(Task pending, TimeProvider timeProvider, Action<Action> dispatch,
        WeakReference<TerminalWindow> owner, int generation)
    {
        bool unknown = false;
        try { await pending.WaitAsync(TimeSpan.FromSeconds(2), timeProvider).ConfigureAwait(false); }
        catch (TimeoutException) { unknown = true; }
        catch (Exception) when (pending.IsFaulted || pending.IsCanceled) { unknown = true; }
        TryDispatch(dispatch, () =>
        {
            if (!owner.TryGetTarget(out TerminalWindow? window) || window._disposed
                || window._callbackGeneration != generation) return;
            if (unknown)
            {
                window._operation.MarkPublicationUnknown();
                window.Status("ShutdownUnknown");
            }
            ProtectedArtifactPublicationReceipt? receipt = window._operation.LatestPublication;
            if (receipt is not null)
                window._status.Text = (unknown ? window.LocalizedText("ShutdownUnknown") + " " : string.Empty)
                    + TerminalStrings.FormatClosingReceipt(receipt, window._operation.Form.Locale);
            window.DetachObservers();
            window._allowStop = true;
            window._application.RequestStop(window);
        });
    }

    private void DispatchToOwner(Action<TerminalWindow> callback)
    {
        WeakReference<TerminalWindow> owner = _owner;
        int generation = Volatile.Read(ref _callbackGeneration);
        TryDispatch(_dispatch, () =>
        {
            if (owner.TryGetTarget(out TerminalWindow? window) && !window._disposed
                && window._callbackGeneration == generation) callback(window);
        });
    }

    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (!newIsRunning && !_allowStop && !_disposed)
        {
            RequestStopFromSignal();
            return !_allowStop;
        }
        return base.OnIsRunningChanging(oldIsRunning, newIsRunning);
    }

    private static void TryDispatch(Action<Action> dispatch, Action callback)
    {
        try { dispatch(callback); }
        catch (Exception exception) when (exception is NotInitializedException or ObjectDisposedException) { }
    }

    private static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    private void DetachObservers()
    {
        if (_observersDetached) return;
        _observersDetached = true;
        Interlocked.Increment(ref _callbackGeneration);
        _importOperation.Settled -= ImportSettled;
        _importOperation.Dispose();
        _workspace.PropertyChanged -= WorkspaceChanged;
        _operation.Invalidated -= OperationInvalidated;
        _operation.PublicationChanged -= PublicationChanged;
        _operation.Form.ViewChanged -= FormViewChanged;
        _operation.Form.ImportComparisonChanged -= ImportComparisonChanged;
        ViewportChanged -= ViewportUpdated;
    }

    private void OperationInvalidated(object? sender, EventArgs args)
    {
        _workspace.Deactivate();
        _workspace.Activate();
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PresentationWorkspace.IsBusy))
            DispatchToOwner(static window =>
            {
                if (Volatile.Read(ref window._closeRequested) == 0) window.UpdateResult();
            });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            ClearSecretDialog();
            _secretCancellation.Cancel();
            _secretCancellation.Dispose();
            _preparationCancellation.Cancel();
            _preparationCancellation.Dispose();
            DetachObservers();
            _masked.ClearSensitiveState();
            _importPath.Text = string.Empty;
            _workspace.Deactivate();
            _operation.ClearPrivateState();
        }
        base.Dispose(disposing);
    }
}
