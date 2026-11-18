namespace Configurator.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>Active tab inside the desktop End Dock (<see cref="Inspector"/> or <see cref="Document"/>).</summary>
public enum DesktopEndDockTab
{
    Inspector = 0,
    Document = 1
}

/// <summary>Single actionable error/required entry displayed inside the inline Desktop ErrorSummary banner.</summary>
public sealed record DesktopErrorSummaryItem(
    string Title,
    string Description,
    DefinitionDiagnostic Diagnostic,
    IRelayCommand NavigateCommand);

/// <summary>Owns view selection only. The workspace retains every answer, draft and editor lifetime.</summary>
public sealed partial class DesktopNavigationViewModel : ObservableObject, IDisposable
{
    public const int RootPageSize = 50;
    public const double StartDockDefaultWidth = 310;
    public const double StartDockMinWidth = 240;
    public const double StartDockMaxWidth = 460;
    public const double EndDockDefaultWidth = 360;
    public const double EndDockMinWidth = 300;
    public const double EndDockMaxWidth = 620;
    public const double CenterCanvasMinWidth = 420;

    private readonly FormWorkspaceViewModel _workspace;
    private DefinitionFormModel? _form;
    private DesktopSectionViewModel[] _sections = [];
    private DesktopSectionViewModel? _selectedSection;
    private DesktopSettingsSection[] _settingsSections;
    private DesktopSettingsSection? _selectedSettingsSection;
    private readonly ObservableCollection<FieldViewModel> _visibleFields = [];
    private readonly ObservableCollection<FieldViewModel> _ungroupedFields = [];
    private readonly ObservableCollection<FieldViewModel> _allSectionsFields = [];
    private DesktopErrorSummaryItem[] _errorSummaryItems = [];
    private DesktopWorkspaceStage _stage;
    private ConfigureLayoutMode _layoutMode = ConfigureLayoutMode.SectionBySection;
    private double _viewportWidth = 1280;
    private bool _isWideViewport = true;
    private bool _isNarrowRailOpen;
    private double _startPaneWidth = StartDockDefaultWidth;
    private double _endPaneWidth = EndDockDefaultWidth;
    private bool _isEndDockInspectorOpen;
    private DesktopEndDockTab _activeEndDockTab = DesktopEndDockTab.Inspector;
    private bool _isDiscoveryOpen;
    private bool _showErrorSummary;
    private bool _isDocumentOpen;
    private bool _refreshing;
    private int _pageIndex;
    private string _searchText = string.Empty;
    private bool _includeInactive;
    private DefinitionDiscoveryFilter _discoveryFilter = DefinitionDiscoveryFilter.All;
    private bool _isCommandPaletteOpen;
    private int _discoveryPageIndex;
    private DefinitionDiscoveryMatch[] _discoveryResults = [];
    private WorkspaceCommandDescriptor[] _discoveryCommands = [];
    private DefinitionDiscoveryMatch? _selectedDiscoveryResult;
    private DiscoveryFeedback _discoveryFeedback;
    private bool _disposed;
    private enum DiscoveryFeedback { None, Limit, Inactive, Unavailable }

    public event Action<FieldViewModel>? AllSectionsScrollRequested;

    public DesktopNavigationViewModel(FormWorkspaceViewModel workspace)
    {
        _workspace = workspace;
        VisibleFields = new(_visibleFields);
        UngroupedFields = new(_ungroupedFields);
        AllSectionsFields = new(_allSectionsFields);
        _settingsSections =
        [
            new(DesktopSettingsSectionId.Appearance, workspace.Strings.Read("SettingsAppearanceTitle"), workspace.Strings.Read("SettingsAppearanceDescription")),
            new(DesktopSettingsSectionId.LanguageAndDirection, workspace.Strings.Read("SettingsLanguageTitle"), workspace.Strings.Read("SettingsLanguageDescription")),
            new(DesktopSettingsSectionId.KeyboardAndDialogs, workspace.Strings.Read("SettingsKeyboardTitle"), workspace.Strings.Read("SettingsKeyboardDescription")),
            new(DesktopSettingsSectionId.PrivacyAndHost, workspace.Strings.Read("SettingsPrivacyTitle"), workspace.Strings.Read("SettingsPrivacyDescription"))
        ];
        _selectedSettingsSection = _settingsSections[0];
        workspace.PropertyChanged += WorkspaceChanged;
        workspace.EditorFocusRequested += Reveal;
        workspace.TransferReviewRequested += ShowTransferReview;
        workspace.ValidationCompleted += OnWorkspaceValidationCompleted;
        Refresh();
    }

    public DesktopWorkspaceStage Stage
    {
        get => _stage;
        set
        {
            if (_disposed || value is DesktopWorkspaceStage.Configure or DesktopWorkspaceStage.Review && _form is null) return;
            if (!SetProperty(ref _stage, value)) return;
            if (value is DesktopWorkspaceStage.Open or DesktopWorkspaceStage.Review)
                _isNarrowRailOpen = false;
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(IsConfigure));
            OnPropertyChanged(nameof(IsReview));
            OnPropertyChanged(nameof(IsSettings));
            OnPropertyChanged(nameof(IsSplitPaneOpen));
            OnPropertyChanged(nameof(IsOverlayPane));
            OnPropertyChanged(nameof(SplitPaneOpenLength));
            OnPropertyChanged(nameof(IsBoundedAllSections));
            OnPropertyChanged(nameof(ShowRailToggle));
            OnPropertyChanged(nameof(CanReturnToConfiguration));
            ReturnToConfigurationCommand.NotifyCanExecuteChanged();
            NotifyEndDockChanged();
        }
    }
    public bool IsWideViewport
    {
        get => _isWideViewport;
        set
        {
            if (_disposed || !SetProperty(ref _isWideViewport, value)) return;
            if (value) _isNarrowRailOpen = false;
            OnPropertyChanged(nameof(IsNarrowViewport));
            OnPropertyChanged(nameof(IsSplitPaneOpen));
            OnPropertyChanged(nameof(IsOverlayPane));
            OnPropertyChanged(nameof(SplitPaneOpenLength));
            OnPropertyChanged(nameof(ShowRailToggle));
            NotifyEndDockChanged();
        }
    }

    public void UpdateViewportWidth(double width)
    {
        if (_disposed || double.IsNaN(width) || width <= 0) return;
        _viewportWidth = width;
        IsWideViewport = width >= 900;
        NotifyEndDockChanged();
    }

    public bool IsNarrowViewport => !IsWideViewport;
    public bool IsNarrowRailOpen
    {
        get => _isNarrowRailOpen;
        set
        {
            if (_disposed || !SetProperty(ref _isNarrowRailOpen, value)) return;
            OnPropertyChanged(nameof(IsSplitPaneOpen));
            OnPropertyChanged(nameof(IsOverlayPane));
            OnPropertyChanged(nameof(SplitPaneOpenLength));
            NotifyEndDockChanged();
        }
    }
    public bool IsSplitPaneOpen => (IsConfigure || IsSettings) && (IsWideViewport || IsNarrowRailOpen);
    public bool IsWideRailOpen => IsWideViewport && (IsConfigure || IsSettings);
    public bool IsOverlayPane => !IsWideViewport || (!IsConfigure && !IsSettings);
    public double StartPaneWidth
    {
        get => _startPaneWidth;
        set
        {
            double clamped = Math.Clamp(value, StartDockMinWidth, StartDockMaxWidth);
            if (_disposed || Math.Abs(_startPaneWidth - clamped) < 0.1) return;
            _startPaneWidth = clamped;
            OnPropertyChanged(nameof(StartPaneWidth));
            OnPropertyChanged(nameof(SplitPaneOpenLength));
            NotifyEndDockChanged();
        }
    }
    public double SplitPaneOpenLength => IsSplitPaneOpen ? StartPaneWidth : 0;
    public bool ShowRailToggle => !IsWideViewport && (IsConfigure || IsSettings);

    public double EndPaneWidth
    {
        get => _endPaneWidth;
        set
        {
            double clamped = Math.Clamp(value, EndDockMinWidth, EndDockMaxWidth);
            if (_disposed || Math.Abs(_endPaneWidth - clamped) < 0.1) return;
            _endPaneWidth = clamped;
            OnPropertyChanged(nameof(EndPaneWidth));
            OnPropertyChanged(nameof(EndDockColumnWidth));
            NotifyEndDockChanged();
        }
    }

    public GridLength EndDockColumnWidth
    {
        get => IsInlineEndDockVisible ? new GridLength(_endPaneWidth, GridUnitType.Pixel) : GridLength.Auto;
        set
        {
            if (!_disposed && value.IsAbsolute)
                EndPaneWidth = value.Value;
        }
    }

    public DesktopEndDockTab ActiveEndDockTab
    {
        get => _activeEndDockTab;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _activeEndDockTab, value)) return;
            NotifyEndDockChanged();
        }
    }

    public bool IsEndDockOpen => !_disposed && IsConfigure && (_isDocumentOpen || _isEndDockInspectorOpen);
    public bool IsOverlayEndDock =>
        !IsWideViewport ||
        (_viewportWidth - (IsSplitPaneOpen ? StartPaneWidth : 0) - EndPaneWidth < CenterCanvasMinWidth);
    public bool IsInlineEndDockVisible => IsEndDockOpen && !IsOverlayEndDock;
    public bool IsOverlayEndDockVisible => IsEndDockOpen && IsOverlayEndDock;
    public bool IsInspectorTabSelected => ActiveEndDockTab == DesktopEndDockTab.Inspector;
    public bool IsDocumentTabSelected => ActiveEndDockTab == DesktopEndDockTab.Document;
    public bool IsInspectorTabVisible => IsEndDockOpen && ActiveEndDockTab == DesktopEndDockTab.Inspector;
    public bool IsDocumentTabVisible => IsEndDockOpen && ActiveEndDockTab == DesktopEndDockTab.Document;

    public void ResizeStartPane(double deltaPx, bool isRightToLeft = false)
    {
        double directional = isRightToLeft ? -deltaPx : deltaPx;
        StartPaneWidth += directional;
    }

    public void ResetStartPaneWidth() => StartPaneWidth = StartDockDefaultWidth;

    public void ResizeEndPane(double deltaPx, bool isRightToLeft = false)
    {
        double directional = isRightToLeft ? deltaPx : -deltaPx;
        EndPaneWidth += directional;
    }

    public void ResetEndPaneWidth() => EndPaneWidth = EndDockDefaultWidth;

    internal void SyncInspectorOpen(bool open)
    {
        if (_disposed || _isEndDockInspectorOpen == open) return;
        _isEndDockInspectorOpen = open;
        if (open)
            _activeEndDockTab = DesktopEndDockTab.Inspector;
        else if (_isDocumentOpen)
            _activeEndDockTab = DesktopEndDockTab.Document;
        NotifyEndDockChanged();
    }

    public void SelectInspectorTab()
    {
        if (_disposed || _form is null) return;
        _activeEndDockTab = DesktopEndDockTab.Inspector;
        NotifyEndDockChanged();
    }

    public void SelectDocumentTab()
    {
        if (_disposed || _form is null) return;
        _activeEndDockTab = DesktopEndDockTab.Document;
        if (!_isDocumentOpen)
            IsDocumentOpen = true;
        else
            NotifyEndDockChanged();
    }

    private void NotifyEndDockChanged()
    {
        OnPropertyChanged(nameof(IsWideRailOpen));
        OnPropertyChanged(nameof(ActiveEndDockTab));
        OnPropertyChanged(nameof(IsEndDockOpen));
        OnPropertyChanged(nameof(IsOverlayEndDock));
        OnPropertyChanged(nameof(IsInlineEndDockVisible));
        OnPropertyChanged(nameof(IsOverlayEndDockVisible));
        OnPropertyChanged(nameof(EndDockColumnWidth));
        OnPropertyChanged(nameof(IsInspectorTabSelected));
        OnPropertyChanged(nameof(IsDocumentTabSelected));
        OnPropertyChanged(nameof(IsInspectorTabVisible));
        OnPropertyChanged(nameof(IsDocumentTabVisible));
    }
    public bool IsDiscoveryOpen
    {
        get => _isDiscoveryOpen;
        private set
        {
            if (_disposed || !SetProperty(ref _isDiscoveryOpen, value)) return;
            OnPropertyChanged(nameof(HasDiscoveryCommands));
        }
    }
    public bool HasErrorSummary => !_disposed && IsConfigure && _showErrorSummary && _errorSummaryItems.Length > 0;
    public IReadOnlyList<DesktopErrorSummaryItem> ErrorSummaryItems => _errorSummaryItems;
    public string ErrorSummaryHeading => _errorSummaryItems.Length == 0
        ? string.Empty
        : $"{_workspace.Strings.Read("ErrorSummaryTitle")} ({_errorSummaryItems.Length})";
    public string CompactProgressText => ProgressSummary is { } summary
        ? $"{summary.ReadyRequired}/{summary.TotalRequired} {(_workspace.Strings.Read("Required"))} · {summary.MissingRequired + summary.InvalidCount}"
        : string.Empty;
    public bool IsOpen => Stage == DesktopWorkspaceStage.Open;
    public bool IsConfigure => Stage == DesktopWorkspaceStage.Configure;
    public bool IsReview => Stage == DesktopWorkspaceStage.Review;
    public bool IsSettings => Stage == DesktopWorkspaceStage.Settings;
    public bool HasWorkspace => _form is not null;
    public IReadOnlyList<DesktopSettingsSection> SettingsSections => _settingsSections;
    public DesktopSettingsSection? SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (_disposed || value is null || !_settingsSections.Contains(value) ||
                !SetProperty(ref _selectedSettingsSection, value)) return;
            OnPropertyChanged(nameof(IsAppearanceSettings));
            OnPropertyChanged(nameof(IsLanguageSettings));
            OnPropertyChanged(nameof(IsKeyboardSettings));
            OnPropertyChanged(nameof(IsPrivacySettings));
        }
    }
    public bool IsAppearanceSettings => SelectedSettingsSection?.Id == DesktopSettingsSectionId.Appearance;
    public bool IsLanguageSettings => SelectedSettingsSection?.Id == DesktopSettingsSectionId.LanguageAndDirection;
    public bool IsKeyboardSettings => SelectedSettingsSection?.Id == DesktopSettingsSectionId.KeyboardAndDialogs;
    public bool IsPrivacySettings => SelectedSettingsSection?.Id == DesktopSettingsSectionId.PrivacyAndHost;
    public IReadOnlyList<DesktopSectionViewModel> Sections => _sections;
    public DesktopSectionViewModel? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (_disposed || value is null || !_sections.Contains(value) ||
                !SetProperty(ref _selectedSection, value)) return;
            _pageIndex = 0;
            if (_refreshing) return;
            _form?.RecordSectionOpened(value.Id);
            if (IsAllSectionsMode && value.Fields.Count > 0)
                AllSectionsScrollRequested?.Invoke(value.Fields[0]);
            OnPropertyChanged(nameof(HasSectionGroups));
            OnPropertyChanged(nameof(WizardNavigation));
            RefreshPage();
            NotifyWizardCommands();
        }
    }
    public ConfigureLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _layoutMode, value)) return;
            OnPropertyChanged(nameof(IsSectionBySectionMode));
            OnPropertyChanged(nameof(IsAllSectionsMode));
            OnPropertyChanged(nameof(IsBoundedAllSections));
            _form?.SetConfigureLayoutMode(value);
        }
    }

    public bool IsDocumentOpen
    {
        get => _isDocumentOpen;
        set
        {
            if (_disposed || !SetProperty(ref _isDocumentOpen, value)) return;
            if (value)
            {
                Stage = DesktopWorkspaceStage.Configure;
                _activeEndDockTab = DesktopEndDockTab.Document;
                _isDiscoveryOpen = false;
                _isCommandPaletteOpen = false;
                OnPropertyChanged(nameof(IsDiscoveryOpen));
                OnPropertyChanged(nameof(IsCommandPaletteOpen));
                OnPropertyChanged(nameof(HasDiscoveryCommands));
            }
            else if (_isEndDockInspectorOpen)
            {
                _activeEndDockTab = DesktopEndDockTab.Inspector;
            }
            OnPropertyChanged(nameof(IsSectionBySectionMode));
            OnPropertyChanged(nameof(IsAllSectionsMode));
            OnPropertyChanged(nameof(IsBoundedAllSections));
            NotifyEndDockChanged();
        }
    }

    public bool CanReturnToConfiguration => !_disposed && IsSettings && HasWorkspace;

    public bool IsSectionBySectionMode => LayoutMode == ConfigureLayoutMode.SectionBySection;
    public bool IsAllSectionsMode => LayoutMode == ConfigureLayoutMode.AllSections;
    public bool IsBoundedAllSections => IsConfigure && LayoutMode == ConfigureLayoutMode.AllSections;
    public bool HasSectionGroups => SelectedSection?.HasGroups == true;
    public ReadOnlyObservableCollection<FieldViewModel> VisibleFields { get; }
    public ReadOnlyObservableCollection<FieldViewModel> UngroupedFields { get; }
    public ReadOnlyObservableCollection<FieldViewModel> AllSectionsFields { get; }
    public bool HasUngroupedFields => HasSectionGroups && _ungroupedFields.Count > 0;
    public DefinitionFormProgress? Progress => _form?.Projection.Progress;
    public WorkspaceProgressSummary? ProgressSummary => _form?.Projection.ProgressSummary;
    public WorkspaceNavigationProjection? WizardNavigation => _form?.Projection.Navigation;
    public bool IsWizardMode => WizardNavigation?.IsWizardNavigation == true;
    public int PageNumber => _pageIndex + 1;
    public int PageCount => Math.Max(1, ((SelectedSection?.Fields.Count ?? 0) + RootPageSize - 1) / RootPageSize);
    public bool HasPages => PageCount > 1;
    public bool CanPreviousPage => !_disposed && _pageIndex > 0;
    public bool CanNextPage => !_disposed && PageNumber < PageCount;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_disposed || !SetProperty(ref _searchText, value ?? string.Empty)) return;
            if (!string.IsNullOrEmpty(_searchText)) IsDiscoveryOpen = true;
            ApplyDiscovery();
        }
    }
    public bool IncludeInactive
    {
        get => _includeInactive;
        set
        {
            if (_disposed || !SetProperty(ref _includeInactive, value)) return;
            if (value) IsDiscoveryOpen = true;
            ApplyDiscovery();
        }
    }
    public DefinitionDiscoveryFilter DiscoveryFilter
    {
        get => _discoveryFilter;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _discoveryFilter, value)) return;
            if (value != DefinitionDiscoveryFilter.All) IsDiscoveryOpen = true;
            OnPropertyChanged(nameof(IsDiscoveryFilterAll));
            OnPropertyChanged(nameof(IsDiscoveryFilterInvalid));
            OnPropertyChanged(nameof(IsDiscoveryFilterMissing));
            OnPropertyChanged(nameof(IsDiscoveryFilterModified));
            OnPropertyChanged(nameof(IsDiscoveryFilterDeferred));
            ApplyDiscovery();
        }
    }
    public bool IsDiscoveryFilterAll => DiscoveryFilter == DefinitionDiscoveryFilter.All;
    public bool IsDiscoveryFilterInvalid => DiscoveryFilter == DefinitionDiscoveryFilter.Invalid;
    public bool IsDiscoveryFilterMissing => DiscoveryFilter == DefinitionDiscoveryFilter.MissingRequired;
    public bool IsDiscoveryFilterModified => DiscoveryFilter == DefinitionDiscoveryFilter.Modified;
    public bool IsDiscoveryFilterDeferred => DiscoveryFilter == DefinitionDiscoveryFilter.DeferredSecret;
    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set
        {
            if (_disposed || !SetProperty(ref _isCommandPaletteOpen, value)) return;
            if (value) IsDiscoveryOpen = true;
            OnPropertyChanged(nameof(HasDiscoveryCommands));
        }
    }
    public IReadOnlyList<WorkspaceCommandDescriptor> DiscoveryCommands => _discoveryCommands;
    public bool HasDiscoveryCommands => !_disposed && IsCommandPaletteOpen && _discoveryCommands.Length > 0;
    public IReadOnlyList<DefinitionDiscoveryMatch> DiscoveryResults => _discoveryResults;
    public int DiscoveryTotal => _discoveryFeedback == DiscoveryFeedback.Limit ? 0 : _form?.Projection.Discovery.TotalCount ?? 0;
    public int DiscoveryPageNumber => _discoveryPageIndex + 1;
    public int DiscoveryPageCount => Math.Max(1, (DiscoveryTotal + 24) / 25);
    public bool HasDiscoveryQuery => SearchText.Length > 0 || IncludeInactive || DiscoveryFilter != DefinitionDiscoveryFilter.All;
    public bool HasDiscoveryPages => DiscoveryPageCount > 1;
    public bool CanPreviousDiscoveryPage => !_disposed && _discoveryPageIndex > 0;
    public bool CanNextDiscoveryPage => !_disposed && DiscoveryPageNumber < DiscoveryPageCount;
    public string DiscoveryGuidance => _discoveryFeedback == DiscoveryFeedback.None
        ? string.Empty : _workspace.Strings["Discovery" + _discoveryFeedback];
    public DefinitionDiscoveryMatch? SelectedDiscoveryResult
    {
        get => _selectedDiscoveryResult;
        set
        {
            if (_disposed || value is not null && !_discoveryResults.Contains(value) ||
                !SetProperty(ref _selectedDiscoveryResult, value) || value is null) return;
            NavigateDiscovery(value);
        }
    }

    [RelayCommand] private void Open() => Stage = DesktopWorkspaceStage.Open;
    [RelayCommand] private void Configure() => Stage = DesktopWorkspaceStage.Configure;
    [RelayCommand] private void Review() => Stage = DesktopWorkspaceStage.Review;
    [RelayCommand] private void Settings() => Stage = DesktopWorkspaceStage.Settings;
    [RelayCommand(CanExecute = nameof(CanReturnToConfiguration))]
    private void ReturnToConfiguration() => Stage = DesktopWorkspaceStage.Configure;
    [RelayCommand] private void ToggleNarrowRail() { if (!IsWideViewport) IsNarrowRailOpen = !IsNarrowRailOpen; }
    [RelayCommand] private void SelectSectionBySectionMode() { IsDocumentOpen = false; LayoutMode = ConfigureLayoutMode.SectionBySection; }
    [RelayCommand] private void SelectAllSectionsMode() { IsDocumentOpen = false; LayoutMode = ConfigureLayoutMode.AllSections; }
    [RelayCommand] private void SelectDocumentMode() => IsDocumentOpen = !IsDocumentOpen;
    [RelayCommand] private void CloseDocumentDrawer() => IsDocumentOpen = false;
    [RelayCommand] private void SetDiscoveryFilter(DefinitionDiscoveryFilter filter) => DiscoveryFilter = filter;
    [RelayCommand] private void ToggleCommandPalette() => IsCommandPaletteOpen = !IsCommandPaletteOpen;
    [RelayCommand] private void CloseCommandPalette() { IsCommandPaletteOpen = false; IsDiscoveryOpen = false; }
    [RelayCommand]
    public void OpenDiscovery(bool withCommandPalette = true)
    {
        if (_disposed || _form is null) return;
        Stage = DesktopWorkspaceStage.Configure;
        IsDocumentOpen = false;
        if (withCommandPalette) IsCommandPaletteOpen = true;
        IsDiscoveryOpen = true;
    }
    public bool FocusInSectionBySection(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_disposed || _form is not { } form) return false;
        IsDocumentOpen = false;
        LayoutMode = ConfigureLayoutMode.SectionBySection;
        Stage = DesktopWorkspaceStage.Configure;
        if (!form.FocusEditor(path, preserveDiscovery: true)) return false;
        if (FindEditor(_workspace.Fields, path) is { } target)
        {
            Reveal(target);
            target.RequestFocus();
        }
        return true;
    }
    [RelayCommand(CanExecute = nameof(CanMoveBackWizardStep))]
    private void PreviousStep() => SelectSectionById(WizardNavigation?.PreviousStepSectionId);
    [RelayCommand(CanExecute = nameof(CanAdvanceWizardStep))]
    private void NextStep() => SelectSectionById(WizardNavigation?.NextStepSectionId);
    [RelayCommand(CanExecute = nameof(CanJumpNextError))]
    private void JumpNextError() => FocusActionableTarget(errorsOnly: true);
    [RelayCommand(CanExecute = nameof(CanJumpNextRequired))]
    private void JumpNextRequired() => FocusActionableTarget(errorsOnly: false);
    [RelayCommand(CanExecute = nameof(CanJumpNextUnvisited))]
    private void JumpNextUnvisited()
    {
        string? sectionId = WizardNavigation?.NextUnvisitedSectionId;
        SelectSectionById(sectionId);
        if (!string.IsNullOrEmpty(sectionId) &&
            _sections.FirstOrDefault(s => s.Id == sectionId) is { Fields.Count: > 0 } section)
        {
            Reveal(section.Fields[0]);
            section.Fields[0].RequestFocus();
        }
    }

    private bool FocusActionableTarget(bool errorsOnly)
    {
        if (_disposed || _form is not { } form) return false;
        WorkspaceActionableTarget? target = form.ResolveNextActionableTarget(SelectedSection?.Id, errorsOnly);
        if (target is null)
        {
            string? fallbackSection = errorsOnly ? WizardNavigation?.NextErrorSectionId : WizardNavigation?.NextRequiredSectionId;
            SelectSectionById(fallbackSection);
            return fallbackSection is not null;
        }
        if (FocusPath(target.DefinitionPath))
        {
            _showErrorSummary = true;
            RefreshErrorSummary();
            return true;
        }
        SelectSectionById(target.SectionId);
        return true;
    }

    private bool FocusPath(DefinitionPath path)
    {
        if (_disposed || _form is not { } form || !form.FocusEditor(path, preserveDiscovery: true))
            return false;
        FieldViewModel? target = FindEditor(_workspace.Fields, path);
        if (target is null) return false;
        Reveal(target);
        target.RequestFocus();
        return true;
    }

    private bool CanMoveBackWizardStep() => !_disposed && WizardNavigation?.CanMoveBackWizardStep == true;
    private bool CanAdvanceWizardStep() => !_disposed && WizardNavigation?.CanAdvanceWizardStep == true;
    private bool CanJumpNextError() => !_disposed && (!string.IsNullOrEmpty(WizardNavigation?.NextErrorSectionId) || _form?.ResolveNextActionableTarget(SelectedSection?.Id, errorsOnly: true) is not null);
    private bool CanJumpNextRequired() => !_disposed && (!string.IsNullOrEmpty(WizardNavigation?.NextRequiredSectionId) || _form?.ResolveNextActionableTarget(SelectedSection?.Id, errorsOnly: false) is not null);
    private bool CanJumpNextUnvisited() => !_disposed && !string.IsNullOrEmpty(WizardNavigation?.NextUnvisitedSectionId);

    private void SelectSectionById(string? sectionId)
    {
        if (_disposed || string.IsNullOrEmpty(sectionId)) return;
        DesktopSectionViewModel? match = _sections.FirstOrDefault(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
        if (match is not null) SelectedSection = match;
    }

    private void NotifyWizardCommands()
    {
        PreviousStepCommand.NotifyCanExecuteChanged();
        NextStepCommand.NotifyCanExecuteChanged();
        JumpNextErrorCommand.NotifyCanExecuteChanged();
        JumpNextRequiredCommand.NotifyCanExecuteChanged();
        JumpNextUnvisitedCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    private void PreviousPage() { _pageIndex--; RefreshPage(); }
    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage() { _pageIndex++; RefreshPage(); }
    [RelayCommand(CanExecute = nameof(CanPreviousDiscoveryPage))]
    private void PreviousDiscoveryPage() { _discoveryPageIndex--; RefreshDiscovery(); }
    [RelayCommand(CanExecute = nameof(CanNextDiscoveryPage))]
    private void NextDiscoveryPage() { _discoveryPageIndex++; RefreshDiscovery(); }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FormWorkspaceViewModel.Form) or nameof(FormWorkspaceViewModel.Strings))
            Refresh();
    }

    private void Refresh()
    {
        if (_disposed || _refreshing) return;
        _refreshing = true;
        try
        {
            bool replaced = !ReferenceEquals(_form, _workspace.Form);
            _form = _workspace.Form;
            string? selectedId = replaced ? null : _selectedSection?.Id;
            DesktopSectionViewModel Section(
                string id,
                string title,
                string? description,
                IReadOnlyList<FieldViewModel> fields,
                DefinitionFormProgress progress,
                SectionStatusProjection? status,
                IReadOnlyList<DefinitionFormSectionGroup>? rawGroups)
            {
                DesktopSectionViewModel? existing = replaced ? null : _sections.FirstOrDefault(section => section.Id == id);
                DesktopSectionGroupViewModel[] groups = BuildGroups(existing?.Groups, id, rawGroups, fields);
                SectionConfigurationStatus configStatus = status?.ConfigurationStatus ?? SectionConfigurationStatus.OptionalUnset;
                SectionReviewCoverage coverage = status?.Coverage ?? SectionReviewCoverage.NotVisited;
                bool newlyRelevant = status?.HasNewlyRelevantSettings == true;
                int hiddenOpt = status?.AvailableOptionalCount ?? 0;
                if (existing is null)
                {
                    string capturedId = id;
                    return new(id, title, description, fields, progress, configStatus, coverage, newlyRelevant, hiddenOpt, groups, _workspace.Strings,
                        reviewed => _form?.SetSectionReviewed(capturedId, reviewed));
                }
                existing.Refresh(title, description, fields, progress, configStatus, coverage, newlyRelevant, hiddenOpt, groups, _workspace.Strings);
                return existing;
            }
            DesktopSectionViewModel[] sections;
            if (_form is null) sections = [];
            else if (_workspace.Fields.Any(field => field is RootFieldViewModel))
                sections = [Section("root", _workspace.Strings["Configure"], null, _workspace.Fields, _form.Projection.Progress, null, null)];
            else
                sections = _form.Projection.Sections.Select(section => Section(
                    section.Id,
                    section.Title,
                    section.Description,
                    Array.AsReadOnly(_workspace.Fields.Where(field => section.Fields.Contains(field.Id) && field.IsVisible).ToArray()),
                    _form.Projection.SectionProgress.GetValueOrDefault(section.Id) ?? new(0, 0, 0, 0, 0),
                    _form.Projection.SectionStatuses.GetValueOrDefault(section.Id),
                    section.Groups)).ToArray();
            bool sectionsChanged = !_sections.SequenceEqual(sections);
            if (sectionsChanged) _sections = sections;
            _selectedSection = _sections.FirstOrDefault(section => section.Id == selectedId) ??
                (_sections.Length == 0 ? null : _sections[0]);
            if (replaced)
            {
                _pageIndex = 0;
                _layoutMode = ConfigureLayoutMode.SectionBySection;
                _isDocumentOpen = false;
                _searchText = _form?.DiscoveryQuery ?? string.Empty;
                _includeInactive = _form?.DiscoverySelection.IncludeInactive ?? false;
                _discoveryFilter = _form?.DiscoverySelection.Filter ?? DefinitionDiscoveryFilter.All;
                _isCommandPaletteOpen = false;
                _isDiscoveryOpen = false;
                _showErrorSummary = false;
                _discoveryPageIndex = 0;
                _discoveryFeedback = DiscoveryFeedback.None;
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(IncludeInactive));
                OnPropertyChanged(nameof(DiscoveryFilter));
                OnPropertyChanged(nameof(IsDiscoveryFilterAll));
                OnPropertyChanged(nameof(IsDiscoveryFilterInvalid));
                OnPropertyChanged(nameof(IsDiscoveryFilterMissing));
                OnPropertyChanged(nameof(IsDiscoveryFilterModified));
                OnPropertyChanged(nameof(IsDiscoveryFilterDeferred));
                OnPropertyChanged(nameof(IsCommandPaletteOpen));
                OnPropertyChanged(nameof(IsDiscoveryOpen));
                OnPropertyChanged(nameof(HasDiscoveryCommands));
                OnPropertyChanged(nameof(LayoutMode));
                OnPropertyChanged(nameof(IsDocumentOpen));
                OnPropertyChanged(nameof(IsSectionBySectionMode));
                OnPropertyChanged(nameof(IsAllSectionsMode));
                OnPropertyChanged(nameof(IsBoundedAllSections));
                Stage = _form is null ? DesktopWorkspaceStage.Open : DesktopWorkspaceStage.Configure;
                if (_form is not null && _selectedSection is not null)
                    _form.RecordSectionOpened(_selectedSection.Id);
            }
            if (_form is not null && _discoveryFeedback != DiscoveryFeedback.Limit &&
                _searchText != _form.DiscoveryQuery)
            {
                _searchText = _form.DiscoveryQuery;
                OnPropertyChanged(nameof(SearchText));
            }
            if (sectionsChanged) OnPropertyChanged(nameof(Sections));
            RefreshSettingsSections();
            OnPropertyChanged(nameof(SelectedSection));
            OnPropertyChanged(nameof(HasSectionGroups));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(ProgressSummary));
            OnPropertyChanged(nameof(CompactProgressText));
            OnPropertyChanged(nameof(WizardNavigation));
            OnPropertyChanged(nameof(IsWizardMode));
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(IsBoundedAllSections));
            OnPropertyChanged(nameof(IsSplitPaneOpen));
            OnPropertyChanged(nameof(IsOverlayPane));
            OnPropertyChanged(nameof(SplitPaneOpenLength));
            OnPropertyChanged(nameof(ShowRailToggle));
            OnPropertyChanged(nameof(CanReturnToConfiguration));
            ReturnToConfigurationCommand.NotifyCanExecuteChanged();
            NotifyEndDockChanged();
            RefreshPage();
            RefreshAllSections();
            RefreshErrorSummary();
            RefreshDiscovery();
            NotifyWizardCommands();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private DesktopSectionGroupViewModel[] BuildGroups(
        IReadOnlyList<DesktopSectionGroupViewModel>? existingGroups,
        string sectionId,
        IReadOnlyList<DefinitionFormSectionGroup>? rawGroups,
        IReadOnlyList<FieldViewModel> sectionFields)
    {
        if (rawGroups is null || rawGroups.Count == 0) return [];
        var list = new List<DesktopSectionGroupViewModel>(rawGroups.Count);
        foreach (DefinitionFormSectionGroup raw in rawGroups)
        {
            if (!raw.IsApplicable) continue;
            FieldViewModel[] groupFields = sectionFields
                .Where(field => raw.Fields.Contains(field.Id, StringComparer.Ordinal))
                .ToArray();
            DesktopSectionGroupViewModel? existing = existingGroups?
                .FirstOrDefault(item => string.Equals(item.Id, raw.Id, StringComparison.Ordinal));
            if (existing is null)
            {
                string groupId = raw.Id;
                list.Add(new DesktopSectionGroupViewModel(
                    raw.Id,
                    raw.Title,
                    raw.Description,
                    raw.IsApplicable,
                    raw.HasBlockingIssue,
                    raw.IsExpanded,
                    groupFields,
                    expanded => _form?.SetGroupExpanded(sectionId, groupId, expanded)));
            }
            else
            {
                existing.Refresh(raw.Title, raw.Description, raw.IsApplicable, raw.HasBlockingIssue, raw.IsExpanded, groupFields);
                list.Add(existing);
            }
        }
        return list.ToArray();
    }

    private void RefreshSettingsSections()
    {
        if (_settingsSections.Length < 4) return;
        _settingsSections[0].Refresh(_workspace.Strings.Read("SettingsAppearanceTitle"), _workspace.Strings.Read("SettingsAppearanceDescription"));
        _settingsSections[1].Refresh(_workspace.Strings.Read("SettingsLanguageTitle"), _workspace.Strings.Read("SettingsLanguageDescription"));
        _settingsSections[2].Refresh(_workspace.Strings.Read("SettingsKeyboardTitle"), _workspace.Strings.Read("SettingsKeyboardDescription"));
        _settingsSections[3].Refresh(_workspace.Strings.Read("SettingsPrivacyTitle"), _workspace.Strings.Read("SettingsPrivacyDescription"));
    }

    private void ApplyDiscovery()
    {
        _discoveryPageIndex = 0;
        _discoveryFeedback = DiscoveryFeedback.None;
        if (_form is not null)
        {
            try { _form.SetDiscoverySelection(new(SearchText, IncludeInactive, filter: DiscoveryFilter)); }
            catch (ArgumentException) { _discoveryFeedback = DiscoveryFeedback.Limit; }
        }
        RefreshDiscovery();
    }

    private void RefreshDiscovery()
    {
        _discoveryPageIndex = Math.Clamp(_discoveryPageIndex, 0, DiscoveryPageCount - 1);
        _discoveryResults = _discoveryFeedback == DiscoveryFeedback.Limit ? [] :
            _form?.Projection.Matches.Skip(_discoveryPageIndex * 25).Take(25).ToArray() ?? [];
        _discoveryCommands = _form is null || _discoveryFeedback == DiscoveryFeedback.Limit ? [] :
            _form.GetAvailableCommands(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()).ToArray();
        _selectedDiscoveryResult = null;
        foreach (string property in new[] { nameof(DiscoveryResults), nameof(SelectedDiscoveryResult),
            nameof(DiscoveryCommands), nameof(HasDiscoveryCommands),
            nameof(DiscoveryTotal), nameof(DiscoveryPageNumber), nameof(DiscoveryPageCount), nameof(HasDiscoveryQuery),
            nameof(HasDiscoveryPages), nameof(CanPreviousDiscoveryPage), nameof(CanNextDiscoveryPage),
            nameof(DiscoveryGuidance) }) OnPropertyChanged(property);
        PreviousDiscoveryPageCommand.NotifyCanExecuteChanged();
        NextDiscoveryPageCommand.NotifyCanExecuteChanged();
    }

    private void NavigateDiscovery(DefinitionDiscoveryMatch match)
    {
        if (_form is not { } form) return;
        _discoveryFeedback = DiscoveryFeedback.None;
        if (!match.Entry.IsActive) _discoveryFeedback = DiscoveryFeedback.Inactive;
        else
        {
            var input = form.Fields.FirstOrDefault(field => field.Definition.Id == match.Entry.FieldId);
            DefinitionPath path = DefinitionPath.FromSegments(input is null
                ? match.Entry.RelativePath : new[] { input.Definition.Name }.Concat(match.Entry.RelativePath));
            if (!form.FocusEditor(path, preserveDiscovery: true) ||
                FindEditor(_workspace.Fields, path) is not { } target)
                _discoveryFeedback = DiscoveryFeedback.Unavailable;
            else
            {
                IsDiscoveryOpen = false;
                IsCommandPaletteOpen = false;
                Reveal(target);
                target.RequestFocus();
            }
        }
        OnPropertyChanged(nameof(DiscoveryGuidance));
    }

    private void RefreshPage()
    {
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        FieldViewModel[] page = SelectedSection?.Fields.Skip(_pageIndex * RootPageSize).Take(RootPageSize).ToArray() ?? [];
        FieldProjectionCollection.Synchronize(_visibleFields, page);
        FieldViewModel[] ungrouped;
        if (HasSectionGroups && SelectedSection is { } section)
        {
            var grouped = section.Groups.SelectMany(group => group.Fields).ToHashSet();
            ungrouped = page.Where(field => !grouped.Contains(field)).ToArray();
        }
        else ungrouped = [];
        FieldProjectionCollection.Synchronize(_ungroupedFields, ungrouped);
        foreach (string property in new[] { nameof(PageNumber), nameof(PageCount), nameof(HasPages),
            nameof(CanPreviousPage), nameof(CanNextPage), nameof(HasUngroupedFields) }) OnPropertyChanged(property);
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void ShowTransferReview()
    {
        IsDocumentOpen = false;
        LayoutMode = ConfigureLayoutMode.SectionBySection;
        Stage = DesktopWorkspaceStage.Configure;
    }

    internal void Reveal(FieldViewModel field)
    {
        FieldViewModel root = field.RootEditor;
        DesktopSectionViewModel? section = _sections.FirstOrDefault(item => item.Fields.Contains(root));
        if (section is null) return;
        Stage = DesktopWorkspaceStage.Configure;
        IsDocumentOpen = false;
        SelectedSection = section;
        _pageIndex = section.Fields.ToList().IndexOf(root) / RootPageSize;
        RefreshPage();
        if (IsAllSectionsMode)
            AllSectionsScrollRequested?.Invoke(root);
    }

    private void RefreshAllSections()
    {
        FieldViewModel[] flattened = _sections.SelectMany(section => section.Fields).Distinct().ToArray();
        FieldProjectionCollection.Synchronize(_allSectionsFields, flattened);
    }

    private void OnWorkspaceValidationCompleted(bool ready)
    {
        _showErrorSummary = !ready;
        RefreshErrorSummary();
    }

    private void RefreshErrorSummary()
    {
        if (_disposed || _form is null || !_showErrorSummary)
        {
            _errorSummaryItems = [];
            OnPropertyChanged(nameof(ErrorSummaryItems));
            OnPropertyChanged(nameof(ErrorSummaryHeading));
            OnPropertyChanged(nameof(HasErrorSummary));
            return;
        }
        DefinitionValidationResult validation = _form.Validate();
        List<DesktopErrorSummaryItem> items = [];
        foreach (DefinitionDiagnostic diagnostic in validation.Diagnostics)
        {
            FieldViewModel? field = FindFieldForDiagnostic(diagnostic);
            string title = field?.Title ?? diagnostic.FieldId;
            string message = field?.ErrorText ?? diagnostic.Code;
            var captured = diagnostic;
            items.Add(new DesktopErrorSummaryItem(
                title,
                message,
                captured,
                new RelayCommand(() =>
                {
                    if (captured.Location is { } location)
                        FocusCorrection(captured.FieldId, location);
                    else if (_form?.ResolveActionableTarget(captured) is { } actionable)
                        FocusPath(actionable.DefinitionPath);
                })));
            if (items.Count >= 12) break;
        }
        _errorSummaryItems = items.ToArray();
        if (_errorSummaryItems.Length == 0) _showErrorSummary = false;
        OnPropertyChanged(nameof(ErrorSummaryItems));
        OnPropertyChanged(nameof(ErrorSummaryHeading));
        OnPropertyChanged(nameof(HasErrorSummary));
    }

    private FieldViewModel? FindFieldForDiagnostic(DefinitionDiagnostic diagnostic)
    {
        if (_form is null) return null;
        if (diagnostic.Location is { } location &&
            _form.TryResolveCorrectionPath(diagnostic.FieldId, location, out DefinitionPath path))
        {
            return FindEditor(_workspace.Fields, path)
                ?? _workspace.Fields.FirstOrDefault(f => string.Equals(f.Id, diagnostic.FieldId, StringComparison.Ordinal));
        }
        if (_form.ResolveActionableTarget(diagnostic) is { } actionable)
        {
            return FindEditor(_workspace.Fields, actionable.DefinitionPath)
                ?? _workspace.Fields.FirstOrDefault(f => string.Equals(f.Id, diagnostic.FieldId, StringComparison.Ordinal));
        }
        return _workspace.Fields.FirstOrDefault(f => string.Equals(f.Id, diagnostic.FieldId, StringComparison.Ordinal));
    }

    public bool FocusCorrection(string fieldId, DefinitionCorrectionLocation location)
    {
        if (_disposed || _form is not { } form ||
            !form.TryResolveCorrectionPath(fieldId, location, out DefinitionPath path) ||
            !form.FocusEditor(path)) return false;
        FieldViewModel? target = FindEditor(_workspace.Fields, path);
        if (target is null) return false;
        Reveal(target);
        target.RequestFocus();
        return true;
    }

    private static FieldViewModel? FindEditor(IEnumerable<FieldViewModel> fields, DefinitionPath path)
    {
        foreach (FieldViewModel field in fields)
        {
            // The root wrapper and its actual editor share the literal empty path.
            if (field is not RootFieldViewModel && field.EditorPath.Segments.SequenceEqual(path.Segments))
                return field;
            if (FindEditor(field.Descendants, path) is { } child) return child;
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.PropertyChanged -= WorkspaceChanged;
        _workspace.EditorFocusRequested -= Reveal;
        _workspace.TransferReviewRequested -= ShowTransferReview;
        _workspace.ValidationCompleted -= OnWorkspaceValidationCompleted;
        _form = null;
        _sections = [];
        _visibleFields.Clear();
        _ungroupedFields.Clear();
        _allSectionsFields.Clear();
        _errorSummaryItems = [];
        _selectedSection = null;
        _settingsSections = [];
        _selectedSettingsSection = null;
        _discoveryResults = [];
        _selectedDiscoveryResult = null;
    }
}
