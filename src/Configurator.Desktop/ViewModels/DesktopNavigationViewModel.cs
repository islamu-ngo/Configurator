namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using System.Collections.ObjectModel;
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
    private DesktopWorkspaceStage _stage;
    private int _pageIndex;
    private string _searchText = string.Empty;
    private bool _includeInactive;
    private int _discoveryPageIndex;
    private DefinitionDiscoveryMatch[] _discoveryResults = [];
    private DefinitionDiscoveryMatch? _selectedDiscoveryResult;
    private DiscoveryFeedback _discoveryFeedback;
    private bool _disposed;
    private enum DiscoveryFeedback { None, Limit, Inactive, Unavailable }

    public DesktopNavigationViewModel(FormWorkspaceViewModel workspace)
    {
        _workspace = workspace;
        VisibleFields = new(_visibleFields);
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
            RefreshPage();
        }
    }
    public ReadOnlyObservableCollection<FieldViewModel> VisibleFields { get; }
    public DefinitionFormProgress? Progress => _form?.Projection.Progress;
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
    public IReadOnlyList<DefinitionDiscoveryMatch> DiscoveryResults => _discoveryResults;
    public int DiscoveryTotal => _discoveryFeedback == DiscoveryFeedback.Limit ? 0 : _form?.Projection.Discovery.TotalCount ?? 0;
    public int DiscoveryPageNumber => _discoveryPageIndex + 1;
    public int DiscoveryPageCount => Math.Max(1, (DiscoveryTotal + 24) / 25);
    public bool HasDiscoveryQuery => SearchText.Length > 0 || IncludeInactive;
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
        if (_disposed) return;
        bool replaced = !ReferenceEquals(_form, _workspace.Form);
        _form = _workspace.Form;
        string? selectedId = replaced ? null : _selectedSection?.Id;
        DesktopSectionViewModel Section(string id, string title, string? description,
            IReadOnlyList<FieldViewModel> fields, DefinitionFormProgress progress)
        {
            DesktopSectionViewModel? existing = replaced ? null : _sections.FirstOrDefault(section => section.Id == id);
            if (existing is null) return new(id, title, description, fields, progress);
            existing.Refresh(title, description, fields, progress);
            return existing;
        }
        DesktopSectionViewModel[] sections;
        if (_form is null) sections = [];
        else if (_workspace.Fields.Any(field => field is RootFieldViewModel))
            sections = [Section("root", _workspace.Strings["Configure"], null, _workspace.Fields, _form.Projection.Progress)];
        else
            sections = _form.Projection.Sections.Select(section => Section(section.Id,
                section.Title, section.Description,
                Array.AsReadOnly(_workspace.Fields.Where(field => section.Fields.Contains(field.Id) && field.IsVisible).ToArray()),
                _form.Projection.SectionProgress.GetValueOrDefault(section.Id) ?? new(0, 0, 0, 0, 0))).ToArray();
        bool sectionsChanged = !_sections.SequenceEqual(sections);
        if (sectionsChanged) _sections = sections;
        _selectedSection = _sections.FirstOrDefault(section => section.Id == selectedId) ??
            (_sections.Length == 0 ? null : _sections[0]);
        if (replaced)
        {
            _pageIndex = 0;
            _searchText = _form?.DiscoveryQuery ?? string.Empty;
            _includeInactive = _form?.DiscoverySelection.IncludeInactive ?? false;
            _discoveryPageIndex = 0;
            _discoveryFeedback = DiscoveryFeedback.None;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(IncludeInactive));
            Stage = _form is null ? DesktopWorkspaceStage.Open : DesktopWorkspaceStage.Configure;
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
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(HasWorkspace));
        RefreshPage();
        RefreshDiscovery();
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
            try { _form.SetDiscoverySelection(new(SearchText, IncludeInactive)); }
            catch (ArgumentException) { _discoveryFeedback = DiscoveryFeedback.Limit; }
        }
        RefreshDiscovery();
    }

    private void RefreshDiscovery()
    {
        _discoveryPageIndex = Math.Clamp(_discoveryPageIndex, 0, DiscoveryPageCount - 1);
        _discoveryResults = _discoveryFeedback == DiscoveryFeedback.Limit ? [] :
            _form?.Projection.Matches.Skip(_discoveryPageIndex * 25).Take(25).ToArray() ?? [];
        _selectedDiscoveryResult = null;
        foreach (string property in new[] { nameof(DiscoveryResults), nameof(SelectedDiscoveryResult),
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
        foreach (string property in new[] { nameof(PageNumber), nameof(PageCount), nameof(HasPages),
            nameof(CanPreviousPage), nameof(CanNextPage) }) OnPropertyChanged(property);
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
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
        _form = null;
        _sections = [];
        _visibleFields.Clear();
        _selectedSection = null;
        _settingsSections = [];
        _selectedSettingsSection = null;
        _discoveryResults = [];
        _selectedDiscoveryResult = null;
    }
}
