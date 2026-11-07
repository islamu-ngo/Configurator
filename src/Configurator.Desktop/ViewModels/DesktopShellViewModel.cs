namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
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
    private readonly DesktopTheme? _theme;
    private int _appearanceIndex;
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
        FormWorkspaceViewModel workspace, ArtifactPublisherViewModel publisher,
        DesktopImportReviewViewModel importReview, TimeProvider? timeProvider = null, DesktopTheme? theme = null)
    {
        _messenger = messenger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _theme = theme;
        _appearanceIndex = (int)(theme?.Preference ?? DesktopAppearance.System);
        Loader = loader;
        Workspace = workspace;
        Navigation = new DesktopNavigationViewModel(workspace);
        Publisher = publisher;
        ImportReview = importReview;
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
        workspace.PropertyChanged += WorkspaceChanged;
        messenger.Register<DesktopShellViewModel, FocusRequestedMessage>(this, static (root, message) =>
        {
            if (root._disposed) return;
            if (message.Target is "DefinitionPath" or "SelectDefinition")
                root.Navigation.Stage = DesktopWorkspaceStage.Open;
            else if (message.Target is "SelectImport" or "ExistingFilePath" or "SelectOutput" or
                "OutputDirectory" or "FileName" or "StayOpen" or "ForceClose")
                root.Navigation.Stage = DesktopWorkspaceStage.Review;
            root.FocusTarget = message.Target;
            root.FocusRequestGeneration = checked(root.FocusRequestGeneration + 1);
        });
    }

    public DefinitionLoaderViewModel Loader { get; }
    public FormWorkspaceViewModel Workspace { get; }
    public DesktopNavigationViewModel Navigation { get; }
    public ArtifactPublisherViewModel Publisher { get; }
    public DesktopImportReviewViewModel ImportReview { get; }
    public int AppearanceIndex
    {
        get => _appearanceIndex;
        set
        {
            if (_disposed || value is < 0 or > 2 || value == _appearanceIndex) return;
            if (_theme is not null) _theme.Preference = (DesktopAppearance)value;
            SetProperty(ref _appearanceIndex, value);
        }
    }
    /// <summary>Physical settlement, including consumer-owned cleanup; cancellation alone never completes this task.</summary>
    public Task ActiveCompletion => _activeOperation?.Completion ?? Task.CompletedTask;
    public bool IsCloseAuthorized { get; private set; }
    public string CloseMessage => Workspace.Strings.Read(CloseDecisionRequired ? "CloseUnknown" : "ClosePending");
    public bool CanForceClose => !_disposed && CloseDecisionRequired;
    public bool CanStayOpen => !_disposed && IsClosing;
    public string StatusHelp => Workspace.Strings.Read("Status" + _guidance);

    private void SetStatus(string status, DesktopStatusGuidance guidance)
    {
        _guidance = guidance;
        Status = status;
        OnPropertyChanged(nameof(StatusHelp));
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FormWorkspaceViewModel.Strings)) return;
        Loader.SetLocale(Workspace.SelectedLocale);
        OnPropertyChanged(nameof(StatusHelp));
        OnPropertyChanged(nameof(CloseMessage));
    }

    private void Begin(OperationRequestedMessage message)
    {
        if (_disposed || IsBusy || IsClosing || IsCloseAuthorized)
        {
            message.Reply(null);
            return;
        }
        long generation = _generation;
        SetOperationState(true, message.Kind);
        DesktopOperation? operation = null;
        _activeOperation = new DesktopOperation(generation,
            () => !_disposed && generation == _generation,
            () =>
            {
                if (ReferenceEquals(_activeOperation, operation))
                {
                    _activeOperation = null;
                    SetOperationState(false, null);
                }
            }, message.CancellationToken);
        operation = _activeOperation;
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
            ImportReview.IsBusy = busy;
        }
        else _messenger.Send(new OperationStateMessage(busy, kind));
    }

    private void Invalidate(bool explicitlyCancelled = false)
    {
        checked { _generation++; }
        _activeOperation?.Cancel(explicitlyCancelled);
        _messenger.Send(new WorkspaceInvalidatedMessage(_generation));
        if (!_disposed) SetStatus(Workspace.Form is null ? "definition-required" : "inputs-changed",
            Workspace.Form is null ? DesktopStatusGuidance.DefinitionRequired : DesktopStatusGuidance.InputsChanged);
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_disposed) return;
        Invalidate(explicitlyCancelled: true);
        SetStatus("operation-cancelled", DesktopStatusGuidance.Cancelled);
    }

    [RelayCommand]
    private void FocusDefinitionPath() => _messenger.Send(new FocusRequestedMessage("DefinitionPath"));

    [RelayCommand]
    private async Task ReviewAndPrepareAsync()
    {
        if (_disposed) return;
        Navigation.Stage = DesktopWorkspaceStage.Review;
        await Publisher.PrepareSelectionAsync();
    }

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
        Workspace.PropertyChanged -= WorkspaceChanged;
        if (_disposed) return;
        _disposed = true;
        _closeDecision?.TrySetResult(false);
        _closeDecision = null;
        CloseDecisionRequired = false;
        IsClosing = false;
        Invalidate();
        Loader.Dispose();
        ImportReview.Dispose();
        Publisher.Dispose();
        Navigation.Dispose();
        Workspace.Dispose();
        _messenger.UnregisterAll(this);
        ForceCloseCommand.NotifyCanExecuteChanged();
        StayOpenCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DesktopShellViewModel);
}
