namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;
using Configurator.Presentation.Theming;

/// <summary>Selectable Event theme preset chip presented inside Desktop Appearance settings.</summary>
public sealed partial class DesktopThemePresetItem : ObservableObject
{
    public DesktopThemePresetItem(string themeKey, string displayName, string naturalHex, string brandHex, bool isSelected)
    {
        ThemeKey = themeKey;
        DisplayName = displayName;
        NaturalHex = naturalHex;
        BrandHex = brandHex;
        _isSelected = isSelected;
    }

    public string ThemeKey { get; }
    public string DisplayName { get; }
    public string NaturalHex { get; }
    public string BrandHex { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Coordinates operation lifetime and status without owning field or artifact behavior.</summary>
public sealed partial class DesktopShellViewModel : ObservableObject, IDisposable
{
    private enum DesktopEntryTask { Fresh, Import }

    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly DesktopTheme? _theme;
    private readonly DesktopThemePresetItem[] _themePresets;
    private int _appearanceIndex;
    private int _appearanceModeIndex;
    private WorkspaceDensity _density = WorkspaceDensity.Comfortable;
    private WorkspaceNamingMode _defaultNamingMode = WorkspaceNamingMode.Humanized;
    private string _customNaturalHex = ThemePreference.DefaultNaturalHex;
    private string _customBrandHex = ThemePreference.DefaultBrandHex;
    private double _customNaturalHue;
    private double _customNaturalSaturation;
    private double _customNaturalLightness;
    private double _customBrandHue;
    private double _customBrandSaturation;
    private double _customBrandLightness;
    private bool _updatingThemeControls;
    private DesktopOperation? _activeOperation;
    private TaskCompletionSource<bool>? _closeDecision;
    private Task<bool>? _closeTask;
    private long _generation;
    private bool _disposed;
    private DesktopEntryTask? _pendingEntryTask;

    [ObservableProperty]
    private bool _profileTaskExpanded;

    [ObservableProperty]
    private bool _importTaskExpanded;
    private DesktopStatusGuidance _guidance = DesktopStatusGuidance.DefinitionRequired;

    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private bool _isSaving;
    [ObservableProperty]
    private bool _isSelecting;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusHelp))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
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
        DesktopAppearance initialPreference = theme?.Preference ?? DesktopAppearance.System;
        _appearanceModeIndex = (int)initialPreference;
        _appearanceIndex = _appearanceModeIndex is >= 0 and <= 2 ? _appearanceModeIndex : 0;
        string activeKey = theme?.ThemeKey ?? ThemePresetCatalog.DefaultThemeKey;
        _themePresets = ThemePresetCatalog.All
            .Select(preset => new DesktopThemePresetItem(
                preset.ThemeKey,
                preset.DisplayName,
                preset.LightPalette.Background,
                preset.LightPalette.Primary,
                string.Equals(preset.ThemeKey, activeKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        SyncCustomSlidersFromHex(
            theme?.CustomNaturalHex ?? ThemePreference.DefaultNaturalHex,
            theme?.CustomBrandHex ?? ThemePreference.DefaultBrandHex);
        if (_theme is not null)
        {
            _theme.HighContrastChanged += OnHighContrastChanged;
            _theme.ThemeChanged += OnThemeChanged;
        }
        Loader = loader;
        Workspace = workspace;
        Navigation = new DesktopNavigationViewModel(workspace);
        Inspector = new DesktopInspectorViewModel(workspace);
        Inspector.PropertyChanged += OnInspectorPropertyChanged;
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
            if (message.Target is "DefinitionUrl" or "DefinitionPath" or "SelectDefinition" or "ProfileDirectory")
                root.Navigation.Stage = DesktopWorkspaceStage.Open;
            else if (message.Target is "SelectImport" or "ExistingFilePath" or "SelectOutput" or
                "OutputDirectory" or "FileName" or "StayOpen" or "ForceClose")
                root.Navigation.Stage = DesktopWorkspaceStage.Review;
            else if (message.Target is "ConfigurationSearch" && root.Navigation.HasWorkspace)
            {
                root.Navigation.OpenDiscovery(withCommandPalette: true);
            }
            root.FocusTarget = message.Target;
            root.FocusRequestGeneration = checked(root.FocusRequestGeneration + 1);
        });
        messenger.Register<DesktopShellViewModel, FormFocusRequestedMessage>(this, static (root, _) =>
        {
            if (root._disposed) return;
            if (root._pendingEntryTask == DesktopEntryTask.Import)
            {
                root.ImportTaskExpanded = true;
                root._messenger.Send(new FocusRequestedMessage("SelectImport"));
            }
            root._pendingEntryTask = null;
        });
    }

    public DefinitionLoaderViewModel Loader { get; }
    public FormWorkspaceViewModel Workspace { get; }
    public DesktopNavigationViewModel Navigation { get; }
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
            if (_disposed || value is < 0 or > 2) return;
            if (_theme is not null) _theme.Preference = (DesktopAppearance)value;
            SetProperty(ref _appearanceIndex, value);
            if (_appearanceModeIndex != value)
            {
                _appearanceModeIndex = value;
                OnPropertyChanged(nameof(AppearanceModeIndex));
            }
        }
    }

    public int AppearanceModeIndex
    {
        get => _appearanceModeIndex;
        set
        {
            if (_disposed || value is < 0 or > 4 || value == _appearanceModeIndex) return;
            _appearanceModeIndex = value;
            if (_theme is not null) _theme.Preference = (DesktopAppearance)value;
            OnPropertyChanged(nameof(AppearanceModeIndex));
            if (value is >= 0 and <= 2 && _appearanceIndex != value)
            {
                _appearanceIndex = value;
                OnPropertyChanged(nameof(AppearanceIndex));
            }
        }
    }

    public IReadOnlyList<DesktopThemePresetItem> ThemePresets => _themePresets;
    public string ActiveThemeKey => _theme?.ThemeKey ?? ThemePresetCatalog.DefaultThemeKey;
    public bool IsCustomPreset => string.Equals(ActiveThemeKey, ThemePresetCatalog.CustomThemeKey, StringComparison.OrdinalIgnoreCase);

    public string CustomNaturalHex
    {
        get => _customNaturalHex;
        set
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (_disposed || !SetProperty(ref _customNaturalHex, trimmed) || _updatingThemeControls) return;
            if (HslColor.TryNormalizeHex(trimmed, out string normalized))
            {
                SyncCustomSlidersFromHex(normalized, _customBrandHex);
                _theme?.SetCustomColors(normalized, _customBrandHex);
            }
        }
    }

    public string CustomBrandHex
    {
        get => _customBrandHex;
        set
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (_disposed || !SetProperty(ref _customBrandHex, trimmed) || _updatingThemeControls) return;
            if (HslColor.TryNormalizeHex(trimmed, out string normalized))
            {
                SyncCustomSlidersFromHex(_customNaturalHex, normalized);
                _theme?.SetCustomColors(_customNaturalHex, normalized);
            }
        }
    }

    public double CustomNaturalHue
    {
        get => _customNaturalHue;
        set { if (!_disposed && SetProperty(ref _customNaturalHue, Math.Clamp(value, 0, 360)) && !_updatingThemeControls) PushCustomNaturalFromSliders(); }
    }

    public double CustomNaturalSaturation
    {
        get => _customNaturalSaturation;
        set { if (!_disposed && SetProperty(ref _customNaturalSaturation, Math.Clamp(value, 0, 100)) && !_updatingThemeControls) PushCustomNaturalFromSliders(); }
    }

    public double CustomNaturalLightness
    {
        get => _customNaturalLightness;
        set { if (!_disposed && SetProperty(ref _customNaturalLightness, Math.Clamp(value, 0, 100)) && !_updatingThemeControls) PushCustomNaturalFromSliders(); }
    }

    public double CustomBrandHue
    {
        get => _customBrandHue;
        set { if (!_disposed && SetProperty(ref _customBrandHue, Math.Clamp(value, 0, 360)) && !_updatingThemeControls) PushCustomBrandFromSliders(); }
    }

    public double CustomBrandSaturation
    {
        get => _customBrandSaturation;
        set { if (!_disposed && SetProperty(ref _customBrandSaturation, Math.Clamp(value, 0, 100)) && !_updatingThemeControls) PushCustomBrandFromSliders(); }
    }

    public double CustomBrandLightness
    {
        get => _customBrandLightness;
        set { if (!_disposed && SetProperty(ref _customBrandLightness, Math.Clamp(value, 0, 100)) && !_updatingThemeControls) PushCustomBrandFromSliders(); }
    }

    [RelayCommand]
    public void SelectThemePreset(string? themeKey)
    {
        if (_disposed || string.IsNullOrWhiteSpace(themeKey)) return;
        if (_theme is not null)
        {
            _theme.SelectPreset(themeKey);
            SyncThemeStateFromTheme();
        }
        else
        {
            foreach (DesktopThemePresetItem item in _themePresets)
                item.IsSelected = string.Equals(item.ThemeKey, themeKey, StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(ActiveThemeKey));
            OnPropertyChanged(nameof(IsCustomPreset));
        }
    }

    [RelayCommand]
    public void SelectCustomTheme()
    {
        if (_disposed) return;
        if (_theme is not null)
        {
            _theme.SelectPreset(ThemePresetCatalog.CustomThemeKey);
            SyncThemeStateFromTheme();
        }
        else
        {
            foreach (DesktopThemePresetItem item in _themePresets)
                item.IsSelected = false;
            OnPropertyChanged(nameof(ActiveThemeKey));
            OnPropertyChanged(nameof(IsCustomPreset));
        }
    }

    [RelayCommand]
    public void ResetCustomTheme()
    {
        if (_disposed) return;
        if (_theme is not null)
            _theme.ResetCustomColors();
        else
            SyncCustomSlidersFromHex(ThemePreference.DefaultNaturalHex, ThemePreference.DefaultBrandHex);
        SyncThemeStateFromTheme();
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
    public string StatusDisplay => ResolveStatusDisplay(Status, Workspace.Strings);
    public bool IsHighContrast => _theme?.IsHighContrast ?? false;
    public string HighContrastStatus => Workspace.Strings.Read(IsHighContrast ? "SettingsContrastActive" : "SettingsContrastInactive");

    private void OnHighContrastChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(IsHighContrast));
        OnPropertyChanged(nameof(HighContrastStatus));
    }

    private void OnThemeChanged(object? sender, EventArgs args)
    {
        if (!_disposed) SyncThemeStateFromTheme();
    }

    private void OnInspectorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_disposed && args.PropertyName == nameof(DesktopInspectorViewModel.IsOpen))
            Navigation.SyncInspectorOpen(Inspector.IsOpen);
    }

    private void SyncThemeStateFromTheme()
    {
        if (_theme is null) return;
        string key = _theme.ThemeKey;
        foreach (DesktopThemePresetItem item in _themePresets)
            item.IsSelected = string.Equals(item.ThemeKey, key, StringComparison.OrdinalIgnoreCase);

        int modeIndex = (int)_theme.Preference;
        if (_appearanceModeIndex != modeIndex)
        {
            _appearanceModeIndex = modeIndex;
            OnPropertyChanged(nameof(AppearanceModeIndex));
        }
        if (modeIndex is >= 0 and <= 2 && _appearanceIndex != modeIndex)
        {
            _appearanceIndex = modeIndex;
            OnPropertyChanged(nameof(AppearanceIndex));
        }

        SyncCustomSlidersFromHex(_theme.CustomNaturalHex, _theme.CustomBrandHex);
        OnPropertyChanged(nameof(ActiveThemeKey));
        OnPropertyChanged(nameof(IsCustomPreset));
        OnPropertyChanged(nameof(IsHighContrast));
        OnPropertyChanged(nameof(HighContrastStatus));
    }

    private void SyncCustomSlidersFromHex(string naturalHex, string brandHex)
    {
        _updatingThemeControls = true;
        try
        {
            if (HslColor.TryNormalizeHex(naturalHex, out string normNat) &&
                HslColor.TryParseHex(normNat, out HslColor natHsl))
            {
                _customNaturalHex = normNat;
                _customNaturalHue = Math.Round(natHsl.H, 1);
                _customNaturalSaturation = Math.Round(natHsl.S, 1);
                _customNaturalLightness = Math.Round(natHsl.L, 1);
            }
            if (HslColor.TryNormalizeHex(brandHex, out string normBrand) &&
                HslColor.TryParseHex(normBrand, out HslColor brandHsl))
            {
                _customBrandHex = normBrand;
                _customBrandHue = Math.Round(brandHsl.H, 1);
                _customBrandSaturation = Math.Round(brandHsl.S, 1);
                _customBrandLightness = Math.Round(brandHsl.L, 1);
            }
            OnPropertyChanged(nameof(CustomNaturalHex));
            OnPropertyChanged(nameof(CustomBrandHex));
            OnPropertyChanged(nameof(CustomNaturalHue));
            OnPropertyChanged(nameof(CustomNaturalSaturation));
            OnPropertyChanged(nameof(CustomNaturalLightness));
            OnPropertyChanged(nameof(CustomBrandHue));
            OnPropertyChanged(nameof(CustomBrandSaturation));
            OnPropertyChanged(nameof(CustomBrandLightness));
        }
        finally
        {
            _updatingThemeControls = false;
        }
    }

    private void PushCustomNaturalFromSliders()
    {
        string hex = HslColor.FromHsl(_customNaturalHue, _customNaturalSaturation, _customNaturalLightness).ToHex();
        _updatingThemeControls = true;
        try
        {
            _customNaturalHex = hex;
            OnPropertyChanged(nameof(CustomNaturalHex));
        }
        finally
        {
            _updatingThemeControls = false;
        }
        _theme?.SetCustomColors(hex, _customBrandHex);
        SyncThemeStateFromTheme();
    }

    private void PushCustomBrandFromSliders()
    {
        string hex = HslColor.FromHsl(_customBrandHue, _customBrandSaturation, _customBrandLightness).ToHex();
        _updatingThemeControls = true;
        try
        {
            _customBrandHex = hex;
            OnPropertyChanged(nameof(CustomBrandHex));
        }
        finally
        {
            _updatingThemeControls = false;
        }
        _theme?.SetCustomColors(_customNaturalHex, hex);
        SyncThemeStateFromTheme();
    }

    private void SetStatus(string status, DesktopStatusGuidance guidance)
    {
        _guidance = guidance;
        Status = status;
        OnPropertyChanged(nameof(StatusHelp));
        OnPropertyChanged(nameof(StatusDisplay));
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FormWorkspaceViewModel.Strings)) return;
        Loader.SetLocale(Workspace.SelectedLocale);
        OnPropertyChanged(nameof(StatusHelp));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(CloseMessage));
        OnPropertyChanged(nameof(HighContrastStatus));
    }

    private static string ResolveStatusDisplay(string? status, DesktopStrings strings)
    {
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;

        string code = status.Trim();
        int colonIndex = code.IndexOf(':');
        string prefix = colonIndex >= 0 ? code[..colonIndex] : code;

        string? resourceKey = prefix switch
        {
            "definition-required" => "StatusCodeDefinitionRequired",
            "definition-loaded" => "StatusCodeDefinitionLoaded",
            "definition-invalid" => "StatusCodeDefinitionInvalid",
            "definition-unreadable" => "StatusCodeDefinitionUnreadable",
            "path-empty" => "StatusCodePathEmpty",
            "load-cancelled" => "StatusCodeLoadCancelled",
            "inputs-changed" => "StatusCodeInputsChanged",
            "operation-cancelled" => "StatusCodeOperationCancelled",
            "validation-ready" => "StatusCodeValidationReady",
            "validation-invalid" => "StatusCodeValidationInvalid",
            "artifact-ready" => "StatusCodePreparationReady",
            "artifact-incomplete" => "StatusCodePreparationIncomplete",
            "artifact-invalid" or "preparation-fault" => prefix == "preparation-fault"
                ? "StatusCodePreparationFault"
                : "StatusCodePreparationInvalid",
            "save-complete" => "StatusCodeSaveComplete",
            "save-failed" or "save-blocked" or "save-aborted" or "save-target-exists" or "save-denied" => "StatusCodeSaveFailed",
            "bundle-save-complete" => "StatusCodeBundleSaveComplete",
            "bundle-save-partial" => "StatusCodeBundleSavePartial",
            "bundle-save-blocked" => "StatusCodeBundleSaveBlocked",
            "bundle-invalid" => "StatusCodeBundleInvalid",
            "import-ready" => "StatusCodeImportReady",
            "import-comparison-ready" => "StatusCodeImportComparisonReady",
            "import-unreadable" or "import-read-failed" or "import-unsupported" or "import-invalid" or "import-conflict" => "StatusCodeImportFailed",
            "document-preview-ready" => "StatusCodeDocumentPreviewReady",
            "document-applied" => "StatusCodeDocumentApplied",
            "document-stale" => "StatusCodeDocumentStale",
            "document-invalid" or "document-rejected" or "document-parse-failed" => "StatusCodeDocumentError",
            "selection-unavailable" => "StatusCodeSelectionUnavailable",
            "selection-nonlocal" => "StatusCodeSelectionNonlocal",
            "remote-uri-invalid" or "remote-host-blocked" or "remote-payload-limit" or "remote-fetch-failed" or "remote-definition-invalid" => "StatusCodeRemoteError",
            "store-unavailable" or "store-project-not-found" or "store-revision-not-found" or "store-snapshot-mismatch" or "store-asset-corrupt" or "store-open-failed" => "StatusCodeStoreOpenFailed",
            _ when prefix.StartsWith("save-", StringComparison.Ordinal) => "StatusCodeSaveFailed",
            _ when prefix.StartsWith("bundle-save-", StringComparison.Ordinal) => "StatusCodeBundleSaveBlocked",
            _ when prefix.StartsWith("import-", StringComparison.Ordinal) => "StatusCodeImportFailed",
            _ when prefix.StartsWith("document-", StringComparison.Ordinal) => "StatusCodeDocumentError",
            _ when prefix.StartsWith("remote-", StringComparison.Ordinal) => "StatusCodeRemoteError",
            _ when prefix.StartsWith("store-", StringComparison.Ordinal) => "StatusCodeStoreOpenFailed",
            _ => null
        };

        return resourceKey is not null ? strings.Read(resourceKey) : code.Replace('-', ' ');
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
    private void StartFreshTask()
    {
        _pendingEntryTask = DesktopEntryTask.Fresh;
        ImportTaskExpanded = false;
        ProfileTaskExpanded = false;
        FocusDefinitionPath();
    }

    [RelayCommand]
    private void UseExistingTask()
    {
        _pendingEntryTask = DesktopEntryTask.Import;
        ProfileTaskExpanded = false;
        FocusDefinitionPath();
    }

    [RelayCommand]
    private void OpenSavedProfileTask()
    {
        _pendingEntryTask = null;
        ImportTaskExpanded = false;
        ProfileTaskExpanded = true;
        _messenger.Send(new FocusRequestedMessage("ProfileDirectory"));
    }

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
        if (Navigation.IsEndDockOpen && Navigation.IsInspectorTabVisible)
        {
            Inspector.Close();
        }
        else
        {
            Navigation.Stage = DesktopWorkspaceStage.Configure;
            Inspector.IsOpen = true;
            Navigation.SelectInspectorTab();
        }
    }

    [RelayCommand]
    public void SelectInspectorTab()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.Stage = DesktopWorkspaceStage.Configure;
        Inspector.IsOpen = true;
        Navigation.SelectInspectorTab();
    }

    [RelayCommand]
    public void SelectDocumentTab()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.Stage = DesktopWorkspaceStage.Configure;
        Navigation.IsDocumentOpen = true;
        Navigation.SelectDocumentTab();
    }

    [RelayCommand]
    public void CloseEndDock()
    {
        if (_disposed) return;
        Inspector.Close();
        Navigation.IsDocumentOpen = false;
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.OpenDiscovery(withCommandPalette: true);
        _messenger.Send(new FocusRequestedMessage("ConfigurationSearch"));
    }

    [RelayCommand]
    public void OpenDocumentMode()
    {
        if (_disposed || Workspace.Form is null) return;
        Navigation.Stage = DesktopWorkspaceStage.Configure;
        Navigation.IsDocumentOpen = !Navigation.IsDocumentOpen;
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
            case WorkspaceCommandId.SwitchToSectionBySection:
                Navigation.Stage = DesktopWorkspaceStage.Configure;
                Navigation.IsDocumentOpen = false;
                Navigation.LayoutMode = ConfigureLayoutMode.SectionBySection;
                break;
            case WorkspaceCommandId.SwitchToAllSections:
                if (Navigation.HasWorkspace)
                {
                    Navigation.Stage = DesktopWorkspaceStage.Configure;
                    Navigation.IsDocumentOpen = false;
                    Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
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
                ToggleInspector();
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
        Navigation.CloseCommandPaletteCommand.Execute(null);
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
        Inspector.PropertyChanged -= OnInspectorPropertyChanged;
        if (_theme is not null)
        {
            _theme.HighContrastChanged -= OnHighContrastChanged;
            _theme.ThemeChanged -= OnThemeChanged;
        }
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
        Inspector.Dispose();
        Navigation.Dispose();
        Workspace.Dispose();
        _messenger.UnregisterAll(this);
        ForceCloseCommand.NotifyCanExecuteChanged();
        StayOpenCommand.NotifyCanExecuteChanged();
    }

    public override string ToString() => nameof(DesktopShellViewModel);
}
