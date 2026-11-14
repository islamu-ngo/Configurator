namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

/// <summary>Coordinates operation lifetime and status without owning field or artifact behavior.</summary>
public sealed partial class DesktopShellViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly DesktopTheme? _theme;
    private int _appearanceIndex;
    private WorkspaceDensity _density = WorkspaceDensity.Comfortable;
    private WorkspaceNamingMode _defaultNamingMode = WorkspaceNamingMode.Humanized;
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
    [ObservableProperty]
    private bool _shortcutsDialogOpen;

    public DesktopShellViewModel(IMessenger messenger, DefinitionLoaderViewModel loader,
        FormWorkspaceViewModel workspace, ArtifactPublisherViewModel publisher,
        DesktopImportReviewViewModel importReview, TimeProvider? timeProvider = null, DesktopTheme? theme = null,
        ISecretClipboardService? clipboard = null)
    {
        _messenger = messenger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _theme = theme;
        _appearanceIndex = (int)(theme?.Preference ?? DesktopAppearance.System);
        if (_theme is not null) _theme.HighContrastChanged += OnHighContrastChanged;
        Loader = loader;
        Workspace = workspace;
        Navigation = new DesktopNavigationViewModel(workspace);
        Inspector = new DesktopInspectorViewModel(workspace);
        CompactTable = new CompactSettingsTableViewModel(workspace, Navigation, messenger, clipboard,
            path => Inspector.InspectPath(path));
        DocumentEditor = new DocumentEditorViewModel(workspace, Navigation, messenger);
        Publisher = publisher;
        ImportReview = importReview;
        Profiles = new ProfileManagerViewModel(workspace, messenger);
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
            if (message.Target is "DefinitionUrl" or "DefinitionPath" or "SelectDefinition")
                root.Navigation.Stage = DesktopWorkspaceStage.Open;
            else if (message.Target is "SelectImport" or "ExistingFilePath" or "SelectOutput" or
                "OutputDirectory" or "FileName" or "StayOpen" or "ForceClose")
                root.Navigation.Stage = DesktopWorkspaceStage.Review;
            else if (message.Target is "ConfigurationSearch" && root.Navigation.HasWorkspace)
            {
                root.Navigation.Stage = DesktopWorkspaceStage.Configure;
                root.Navigation.ConfigureMode = DesktopConfigureViewMode.Guided;
            }
            root.FocusTarget = message.Target;
            root.FocusRequestGeneration = checked(root.FocusRequestGeneration + 1);
        });
    }

    public DefinitionLoaderViewModel Loader { get; }
    public FormWorkspaceViewModel Workspace { get; }
    public DesktopNavigationViewModel Navigation { get; }
    public CompactSettingsTableViewModel CompactTable { get; }
    public DocumentEditorViewModel DocumentEditor { get; }
    public DesktopInspectorViewModel Inspector { get; }
    public ArtifactPublisherViewModel Publisher { get; }
    public DesktopImportReviewViewModel ImportReview { get; }
    public ProfileManagerViewModel Profiles { get; }
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
    public WorkspaceDensity Density
    {
        get => _density;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || value == _density) return;
            _density = value;
            if (_theme is not null) _theme.Density = value;
            else DesktopDensity.Apply(Avalonia.Application.Current, value);
            Workspace.SetDensity(value);
            OnPropertyChanged(nameof(Density));
            OnPropertyChanged(nameof(DensityIndex));
        }
    }
    public int DensityIndex
    {
        get => (int)_density;
        set
        {
            if (value is >= 0 and <= 3) Density = (WorkspaceDensity)value;
        }
    }
    public WorkspaceNamingMode DefaultNamingMode
    {
        get => _defaultNamingMode;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || value == _defaultNamingMode) return;
            _defaultNamingMode = value;
            Workspace.DefaultNamingMode = value;
            OnPropertyChanged(nameof(DefaultNamingMode));
            OnPropertyChanged(nameof(DefaultNamingModeIndex));
        }
    }
    public int DefaultNamingModeIndex
    {
        get => (int)_defaultNamingMode;
        set
        {
            if (value is >= 0 and <= 1) DefaultNamingMode = (WorkspaceNamingMode)value;
        }
    }

    [RelayCommand]
    public void OpenShortcutsHelp()
    {
        if (_disposed) return;
        Navigation.Stage = DesktopWorkspaceStage.Settings;
        Navigation.SelectedSettingsSection = Navigation.SettingsSections
            .FirstOrDefault(section => section.Id == DesktopSettingsSectionId.KeyboardAndDialogs);
        ShortcutsDialogOpen = true;
    }

    [RelayCommand]
    public void CloseShortcutsHelp() => ShortcutsDialogOpen = false;
    /// <summary>Physical settlement, including consumer-owned cleanup; cancellation alone never completes this task.</summary>
    public Task ActiveCompletion => _activeOperation?.Completion ?? Task.CompletedTask;
    public bool IsCloseAuthorized { get; private set; }
    public string CloseMessage => Workspace.Strings.Read(CloseDecisionRequired ? "CloseUnknown" : "ClosePending");
    public bool CanForceClose => !_disposed && CloseDecisionRequired;
    public bool CanStayOpen => !_disposed && IsClosing;
    public string StatusHelp => Workspace.Strings.Read("Status" + _guidance);
    public bool IsHighContrast => _theme?.IsHighContrast ?? false;
    public string HighContrastStatus => Workspace.Strings.Read(IsHighContrast ? "SettingsContrastActive" : "SettingsContrastInactive");

    private void OnHighContrastChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(IsHighContrast));
        OnPropertyChanged(nameof(HighContrastStatus));
    }

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
        OnPropertyChanged(nameof(HighContrastStatus));
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

    [RelayCommand]
    public void ToggleInspector()
    {
        if (_disposed || Workspace.Form is null) return;
        Inspector.Toggle();
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.Stage = DesktopWorkspaceStage.Configure;
        Navigation.ConfigureMode = DesktopConfigureViewMode.Guided;
        Navigation.IsCommandPaletteOpen = true;
        _messenger.Send(new FocusRequestedMessage("ConfigurationSearch"));
    }

    [RelayCommand]
    public void OpenDocumentMode()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.Stage = DesktopWorkspaceStage.Configure;
        Navigation.ConfigureMode = DesktopConfigureViewMode.Document;
    }

    [RelayCommand]
    public void ExecuteWorkspaceCommand(WorkspaceCommandDescriptor? command)
    {
        if (_disposed || command is null || !command.IsEnabled) return;
        switch (command.Id)
        {
            case WorkspaceCommandId.Undo:
                if (Workspace.UndoCommand.CanExecute(null)) Workspace.UndoCommand.Execute(null);
                break;
            case WorkspaceCommandId.Redo:
                if (Workspace.RedoCommand.CanExecute(null)) Workspace.RedoCommand.Execute(null);
                break;
            case WorkspaceCommandId.SwitchToGuided:
                Navigation.Stage = DesktopWorkspaceStage.Configure;
                Navigation.ConfigureMode = DesktopConfigureViewMode.Guided;
                break;
            case WorkspaceCommandId.SwitchToCompact:
                if (Navigation.HasWorkspace)
                {
                    Navigation.Stage = DesktopWorkspaceStage.Configure;
                    Navigation.ConfigureMode = DesktopConfigureViewMode.Compact;
                }
                break;
            case WorkspaceCommandId.SwitchToReview:
                if (Navigation.HasWorkspace) Navigation.Stage = DesktopWorkspaceStage.Review;
                break;
            case WorkspaceCommandId.SwitchToFiles:
                Navigation.Stage = DesktopWorkspaceStage.Open;
                break;
            case WorkspaceCommandId.ToggleNamingMode:
                if (Workspace.ToggleNamingModeCommand.CanExecute(null)) Workspace.ToggleNamingModeCommand.Execute(null);
                break;
            case WorkspaceCommandId.ToggleInspector:
                Inspector.Toggle();
                break;
            case WorkspaceCommandId.OpenDocumentEditor:
                OpenDocumentMode();
                break;
            case WorkspaceCommandId.OpenShortcutsHelp:
                OpenShortcutsHelp();
                break;
            case WorkspaceCommandId.JumpToNextError:
                if (Navigation.JumpNextErrorCommand.CanExecute(null))
                {
                    Navigation.Stage = DesktopWorkspaceStage.Configure;
                    Navigation.JumpNextErrorCommand.Execute(null);
                }
                break;
            case WorkspaceCommandId.JumpToNextRequired:
                if (Navigation.JumpNextRequiredCommand.CanExecute(null))
                {
                    Navigation.Stage = DesktopWorkspaceStage.Configure;
                    Navigation.JumpNextRequiredCommand.Execute(null);
                }
                break;
            case WorkspaceCommandId.JumpToNextUnvisited:
                if (Navigation.JumpNextUnvisitedCommand.CanExecute(null))
                {
                    Navigation.Stage = DesktopWorkspaceStage.Configure;
                    Navigation.JumpNextUnvisitedCommand.Execute(null);
                }
                break;
        }
        Navigation.IsCommandPaletteOpen = false;
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
        if (_theme is not null) _theme.HighContrastChanged -= OnHighContrastChanged;
        if (_disposed) return;
        _disposed = true;
        _closeDecision?.TrySetResult(false);
        _closeDecision = null;
        CloseDecisionRequired = false;
        IsClosing = false;
        Invalidate();
        Loader.Dispose();
        ImportReview.Dispose();
        Profiles.Dispose();
        Publisher.Dispose();
        DocumentEditor.Dispose();
        CompactTable.Dispose();
        Inspector.Dispose();
        Navigation.Dispose();
        Workspace.Dispose();
        _messenger.UnregisterAll(this);
        ForceCloseCommand.NotifyCanExecuteChanged();
        StayOpenCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DesktopShellViewModel);
}
