namespace Configurator.Desktop.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>Owns view selection only. The workspace retains every answer, draft and editor lifetime.</summary>
public sealed partial class DesktopNavigationViewModel : ObservableObject, IDisposable
{
    public const int RootPageSize = 50;
    private readonly FormWorkspaceViewModel _workspace;
    private DefinitionFormModel? _form;
    private DesktopSectionViewModel[] _sections = [];
    private DesktopSectionViewModel? _selectedSection;
    private DesktopSettingsSection[] _settingsSections;
    private DesktopSettingsSection? _selectedSettingsSection;
    private readonly ObservableCollection<FieldViewModel> _visibleFields = [];
    private readonly ObservableCollection<FieldViewModel> _ungroupedFields = [];
    private DesktopWorkspaceStage _stage;
    private DesktopConfigureViewMode _configureMode = DesktopConfigureViewMode.Guided;
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

    public DesktopNavigationViewModel(FormWorkspaceViewModel workspace)
    {
        _workspace = workspace;
        VisibleFields = new(_visibleFields);
        UngroupedFields = new(_ungroupedFields);
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
        Refresh();
    }

    public DesktopWorkspaceStage Stage
    {
        get => _stage;
        set
        {
            if (_disposed || value is DesktopWorkspaceStage.Configure or DesktopWorkspaceStage.Review && _form is null) return;
            if (!SetProperty(ref _stage, value)) return;
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(IsConfigure));
            OnPropertyChanged(nameof(IsReview));
            OnPropertyChanged(nameof(IsSettings));
        }
    }
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
            if (!_refreshing) _form?.RecordSectionOpened(value.Id);
            OnPropertyChanged(nameof(HasSectionGroups));
            OnPropertyChanged(nameof(WizardNavigation));
            RefreshPage();
            NotifyWizardCommands();
        }
    }
    public DesktopConfigureViewMode ConfigureMode
    {
        get => _configureMode;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _configureMode, value)) return;
            OnPropertyChanged(nameof(IsGuidedMode));
            OnPropertyChanged(nameof(IsCompactMode));
            OnPropertyChanged(nameof(IsDocumentMode));
            OnPropertyChanged(nameof(IsBoundedConfigureMode));
            _form?.SetWorkspaceLayoutMode(value == DesktopConfigureViewMode.Compact
                ? WorkspaceLayoutMode.Compact : WorkspaceLayoutMode.Guided);
        }
    }
    public bool IsGuidedMode => ConfigureMode == DesktopConfigureViewMode.Guided;
    public bool IsCompactMode => ConfigureMode == DesktopConfigureViewMode.Compact;
    public bool IsDocumentMode => ConfigureMode == DesktopConfigureViewMode.Document;
    public bool IsBoundedConfigureMode => IsConfigure && ConfigureMode != DesktopConfigureViewMode.Guided;
    public bool HasSectionGroups => SelectedSection?.HasGroups == true;
    public ReadOnlyObservableCollection<FieldViewModel> VisibleFields { get; }
    public ReadOnlyObservableCollection<FieldViewModel> UngroupedFields { get; }
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
            ApplyDiscovery();
        }
    }
    public bool IncludeInactive
    {
        get => _includeInactive;
        set { if (!_disposed && SetProperty(ref _includeInactive, value)) ApplyDiscovery(); }
    }
    public DefinitionDiscoveryFilter DiscoveryFilter
    {
        get => _discoveryFilter;
        set
        {
            if (_disposed || !Enum.IsDefined(value) || !SetProperty(ref _discoveryFilter, value)) return;
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
    [RelayCommand] private void SelectGuidedMode() => ConfigureMode = DesktopConfigureViewMode.Guided;
    [RelayCommand] private void SelectCompactMode() => ConfigureMode = DesktopConfigureViewMode.Compact;
    [RelayCommand] private void SelectDocumentMode() => ConfigureMode = DesktopConfigureViewMode.Document;
    [RelayCommand] private void SetDiscoveryFilter(DefinitionDiscoveryFilter filter) => DiscoveryFilter = filter;
    [RelayCommand] private void ToggleCommandPalette() => IsCommandPaletteOpen = !IsCommandPaletteOpen;
    [RelayCommand] private void CloseCommandPalette() => IsCommandPaletteOpen = false;
    public bool FocusInGuided(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_disposed || _form is not { } form) return false;
        ConfigureMode = DesktopConfigureViewMode.Guided;
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
    private void JumpNextError() => SelectSectionById(WizardNavigation?.NextErrorSectionId);
    [RelayCommand(CanExecute = nameof(CanJumpNextRequired))]
    private void JumpNextRequired() => SelectSectionById(WizardNavigation?.NextRequiredSectionId);
    [RelayCommand(CanExecute = nameof(CanJumpNextUnvisited))]
    private void JumpNextUnvisited() => SelectSectionById(WizardNavigation?.NextUnvisitedSectionId);

    private bool CanMoveBackWizardStep() => !_disposed && WizardNavigation?.CanMoveBackWizardStep == true;
    private bool CanAdvanceWizardStep() => !_disposed && WizardNavigation?.CanAdvanceWizardStep == true;
    private bool CanJumpNextError() => !_disposed && !string.IsNullOrEmpty(WizardNavigation?.NextErrorSectionId);
    private bool CanJumpNextRequired() => !_disposed && !string.IsNullOrEmpty(WizardNavigation?.NextRequiredSectionId);
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
                    return new(id, title, description, fields, progress, configStatus, coverage, newlyRelevant, hiddenOpt, groups, _workspace.Strings);
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
                _configureMode = DesktopConfigureViewMode.Guided;
                _searchText = _form?.DiscoveryQuery ?? string.Empty;
                _includeInactive = _form?.DiscoverySelection.IncludeInactive ?? false;
                _discoveryFilter = _form?.DiscoverySelection.Filter ?? DefinitionDiscoveryFilter.All;
                _isCommandPaletteOpen = false;
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
                OnPropertyChanged(nameof(HasDiscoveryCommands));
                OnPropertyChanged(nameof(ConfigureMode));
                OnPropertyChanged(nameof(IsGuidedMode));
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(IsDocumentMode));
                OnPropertyChanged(nameof(IsBoundedConfigureMode));
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
            OnPropertyChanged(nameof(WizardNavigation));
            OnPropertyChanged(nameof(IsWizardMode));
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(IsBoundedConfigureMode));
            RefreshPage();
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
        ConfigureMode = DesktopConfigureViewMode.Guided;
        Stage = DesktopWorkspaceStage.Configure;
    }

    internal void Reveal(FieldViewModel field)
    {
        FieldViewModel root = field.RootEditor;
        DesktopSectionViewModel? section = _sections.FirstOrDefault(item => item.Fields.Contains(root));
        if (section is null) return;
        Stage = DesktopWorkspaceStage.Configure;
        SelectedSection = section;
        _pageIndex = section.Fields.ToList().IndexOf(root) / RootPageSize;
        RefreshPage();
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
        _form = null;
        _sections = [];
        _visibleFields.Clear();
        _ungroupedFields.Clear();
        _selectedSection = null;
        _settingsSections = [];
        _selectedSettingsSection = null;
        _discoveryResults = [];
        _selectedDiscoveryResult = null;
    }
}
