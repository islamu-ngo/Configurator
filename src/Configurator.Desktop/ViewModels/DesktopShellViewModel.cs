namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;

/// <summary>Coordinates operation lifetime and status without owning field or artifact behavior.</summary>
public sealed partial class DesktopShellViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _operationCancellation;
    private DesktopOperation? _activeOperation;
    private TaskCompletionSource<bool>? _closeDecision;
    private Task<bool>? _closeTask;
    private long _generation;
    private bool _disposed;
    private DesktopStatusGuidance _guidance = DesktopStatusGuidance.DefinitionRequired;

    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private bool _isSaving;
    [ObservableProperty]
    private bool _isSelecting;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusHelp))]
    private string _status = "definition-required";
    [ObservableProperty]
    private string _focusTarget = "DefinitionPath";
    [ObservableProperty]
    private int _focusRequestGeneration = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloseMessage))]
    [NotifyCanExecuteChangedFor(nameof(StayOpenCommand))]
    private bool _isClosing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloseMessage))]
    [NotifyCanExecuteChangedFor(nameof(ForceCloseCommand))]
    private bool _closeDecisionRequired;

    public DesktopShellViewModel(IMessenger messenger, DefinitionLoaderViewModel loader,
        FormWorkspaceViewModel workspace, ArtifactPublisherViewModel publisher, TimeProvider? timeProvider = null)
    {
        _messenger = messenger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Loader = loader;
        Workspace = workspace;
        Publisher = publisher;
        messenger.Register<DesktopShellViewModel, OperationRequestedMessage>(this,
            static (root, message) => root.Begin(message));
        messenger.Register<DesktopShellViewModel, InputsInvalidatedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, FormInputsChangedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, DefinitionClearedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, StatusChangedMessage>(this, static (root, message) =>
        {
            if (!root._disposed && (message.Generation is null || message.Generation == root._generation))
                root.SetStatus(message.Status, message.Guidance);
        });
        messenger.Register<DesktopShellViewModel, FocusRequestedMessage>(this, static (root, message) =>
        {
            if (root._disposed) return;
            root.FocusTarget = message.Target;
            root.FocusRequestGeneration = checked(root.FocusRequestGeneration + 1);
        });
    }

    public DefinitionLoaderViewModel Loader { get; }
    public FormWorkspaceViewModel Workspace { get; }
    public ArtifactPublisherViewModel Publisher { get; }
    /// <summary>Physical settlement, including consumer-owned cleanup; cancellation alone never completes this task.</summary>
    public Task ActiveCompletion => _activeOperation?.Completion ?? Task.CompletedTask;
    public bool IsCloseAuthorized { get; private set; }
    public string CloseMessage => DesktopActionText.Read(CloseDecisionRequired ? "CloseUnknown" : "ClosePending");
    public bool CanForceClose => !_disposed && CloseDecisionRequired;
    public bool CanStayOpen => !_disposed && IsClosing;
    public string StatusHelp => DesktopActionText.Read("Status" + _guidance);

    private void SetStatus(string status, DesktopStatusGuidance guidance)
    {
        _guidance = guidance;
        Status = status;
        OnPropertyChanged(nameof(StatusHelp));
    }

    private void Begin(OperationRequestedMessage message)
    {
        if (_disposed || IsBusy || IsClosing || IsCloseAuthorized)
        {
            message.Reply(null);
            return;
        }
        long generation = _generation;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        CancellationTokenSource cancellation = _operationCancellation;
        SetOperationState(true, message.Kind);
        _activeOperation = new DesktopOperation(generation,
            () => !_disposed && generation == _generation && !cancellation.IsCancellationRequested,
            () =>
            {
                if (ReferenceEquals(_operationCancellation, cancellation))
                {
                    _operationCancellation = null;
                    _activeOperation = null;
                    SetOperationState(false, null);
                }
                cancellation.Dispose();
            }, cancellation.Token);
        message.Reply(_activeOperation);
    }

    private void SetOperationState(bool busy, string? kind)
    {
        IsBusy = busy;
        IsSaving = busy && kind == "save";
        IsSelecting = busy && kind == "selection";
        if (_disposed)
        {
            Loader.IsBusy = busy;
            Workspace.IsBusy = busy;
            Publisher.IsBusy = busy;
            Publisher.IsSaving = IsSaving;
            Publisher.IsSelecting = IsSelecting;
        }
        else _messenger.Send(new OperationStateMessage(busy, kind));
    }

    private void Invalidate()
    {
        checked { _generation++; }
        _operationCancellation?.Cancel();
        _messenger.Send(new WorkspaceInvalidatedMessage(_generation));
        if (!_disposed) SetStatus(Workspace.Form is null ? "definition-required" : "inputs-changed",
            Workspace.Form is null ? DesktopStatusGuidance.DefinitionRequired : DesktopStatusGuidance.InputsChanged);
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_disposed) return;
        Invalidate();
        SetStatus("operation-cancelled", DesktopStatusGuidance.Cancelled);
    }

    [RelayCommand]
    private void FocusDefinitionPath() => _messenger.Send(new FocusRequestedMessage("DefinitionPath"));

    /// <summary>Shares one close intent while retaining the exact physical settlement task.</summary>
    public Task<bool> RequestCloseAsync()
    {
        if (_disposed || IsCloseAuthorized) return Task.FromResult(true);
        if (_closeDecision is not null) return _closeTask!;
        Task settlement = ActiveCompletion;
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _closeDecision = decision;
        IsClosing = true;
        Invalidate();
        _closeTask = DrainForCloseAsync(settlement, decision);
        return _closeTask;
    }

    private async Task<bool> DrainForCloseAsync(Task settlement, TaskCompletionSource<bool> decision)
    {
        using var deadlineCancellation = new CancellationTokenSource();
        try
        {
            if (!settlement.IsCompleted)
            {
                Task deadline = Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, deadlineCancellation.Token);
                await Task.WhenAny(settlement, deadline, decision.Task);
                if (!ReferenceEquals(_closeDecision, decision)) return false;
                if (!settlement.IsCompleted && !decision.Task.IsCompleted)
                {
                    Publisher.MarkPublicationUnknown();
                    CloseDecisionRequired = true;
                    _messenger.Send(new FocusRequestedMessage("StayOpen"));
                    await Task.WhenAny(settlement, decision.Task);
                }
            }
            if (!ReferenceEquals(_closeDecision, decision)) return false;
            if (settlement.IsCompleted) await settlement;
            IsCloseAuthorized = !decision.Task.IsCompleted || await decision.Task;
            CloseDecisionRequired = false;
            return IsCloseAuthorized;
        }
        finally
        {
            await deadlineCancellation.CancelAsync();
            if (ReferenceEquals(_closeDecision, decision))
            {
                _closeDecision = null;
                CloseDecisionRequired = false;
                if (!IsCloseAuthorized) IsClosing = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanStayOpen))]
    public void StayOpen()
    {
        if (!CanStayOpen || _closeDecision is not { } decision) return;
        _closeDecision = null;
        decision.TrySetResult(false);
        CloseDecisionRequired = false;
        IsClosing = false;
        _messenger.Send(new FocusRequestedMessage("Cancel"));
    }

    [RelayCommand(CanExecute = nameof(CanForceClose))]
    public void ForceClose()
    {
        if (CanForceClose) _closeDecision?.TrySetResult(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _closeDecision?.TrySetResult(false);
        _closeDecision = null;
        CloseDecisionRequired = false;
        IsClosing = false;
        Invalidate();
        Loader.Dispose();
        Publisher.Dispose();
        Workspace.Dispose();
        _messenger.UnregisterAll(this);
        ForceCloseCommand.NotifyCanExecuteChanged();
        StayOpenCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DesktopShellViewModel);
}
