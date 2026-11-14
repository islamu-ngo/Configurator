namespace Configurator.Desktop.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Presentation;

/// <summary>Collapsible authored group inside one Desktop section; auto-expands when any member fails validation.</summary>
public sealed class DesktopSectionGroupViewModel : ObservableObject
{
    private readonly Action<bool> _setExpanded;
    private string _title;
    private string? _description;
    private bool _isApplicable;
    private bool _hasBlockingIssue;
    private bool _isExpanded;
    private readonly ObservableCollection<FieldViewModel> _fields = [];

    internal DesktopSectionGroupViewModel(
        string id,
        string title,
        string? description,
        bool isApplicable,
        bool hasBlockingIssue,
        bool isExpanded,
        IEnumerable<FieldViewModel> fields,
        Action<bool> setExpanded)
    {
        Id = id;
        _title = title;
        _description = description;
        _isApplicable = isApplicable;
        _hasBlockingIssue = hasBlockingIssue;
        _isExpanded = isExpanded;
        _setExpanded = setExpanded;
        FieldProjectionCollection.Synchronize(_fields, fields.ToArray());
        Fields = new(_fields);
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public string Id { get; }
    public string Title => _title;
    public string? Description => _description;
    public bool IsApplicable => _isApplicable;
    public bool HasBlockingIssue => _hasBlockingIssue;
    public ReadOnlyObservableCollection<FieldViewModel> Fields { get; }
    public IRelayCommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value)) _setExpanded(value);
        }
    }

    internal void Refresh(
        string title,
        string? description,
        bool isApplicable,
        bool hasBlockingIssue,
        bool isExpanded,
        IEnumerable<FieldViewModel> fields)
    {
        SetProperty(ref _title, title, nameof(Title));
        SetProperty(ref _description, description, nameof(Description));
        SetProperty(ref _isApplicable, isApplicable, nameof(IsApplicable));
        SetProperty(ref _hasBlockingIssue, hasBlockingIssue, nameof(HasBlockingIssue));
        SetProperty(ref _isExpanded, isExpanded, nameof(IsExpanded));
        FieldProjectionCollection.Synchronize(_fields, fields.ToArray());
    }
}

/// <summary>Public section metadata and references to the workspace's existing editors.</summary>
public sealed class DesktopSectionViewModel(
    string id,
    string title,
    string? description,
    IReadOnlyList<FieldViewModel> fields,
    DefinitionFormProgress progress,
    SectionConfigurationStatus configurationStatus = SectionConfigurationStatus.OptionalUnset,
    SectionReviewCoverage coverage = SectionReviewCoverage.NotVisited,
    bool hasNewlyRelevantSettings = false,
    int availableOptionalCount = 0,
    IReadOnlyList<DesktopSectionGroupViewModel>? groups = null,
    DesktopStrings? strings = null) : ObservableObject
{
    private string _title = title;
    private string? _description = description;
    private IReadOnlyList<FieldViewModel> _fields = fields;
    private DefinitionFormProgress _progress = progress;
    private SectionConfigurationStatus _configurationStatus = configurationStatus;
    private SectionReviewCoverage _coverage = coverage;
    private bool _hasNewlyRelevantSettings = hasNewlyRelevantSettings;
    private int _availableOptionalCount = availableOptionalCount;
    private IReadOnlyList<DesktopSectionGroupViewModel> _groups = groups ?? [];
    private string _statusSummary = BuildStatusSummary(configurationStatus, coverage, strings);

    public string Id { get; } = id;
    public string Title => _title;
    public string? Description => _description;
    public IReadOnlyList<FieldViewModel> Fields => _fields;
    public DefinitionFormProgress Progress => _progress;
    public SectionConfigurationStatus ConfigurationStatus => _configurationStatus;
    public SectionReviewCoverage Coverage => _coverage;
    public bool HasNewlyRelevantSettings => _hasNewlyRelevantSettings;
    public int AvailableOptionalCount => _availableOptionalCount;
    public IReadOnlyList<DesktopSectionGroupViewModel> Groups => _groups;
    public bool HasGroups => _groups.Count > 0;
    public string StatusSummary => _statusSummary;

    internal void Refresh(
        string title,
        string? description,
        IReadOnlyList<FieldViewModel> fields,
        DefinitionFormProgress progress,
        SectionConfigurationStatus configurationStatus,
        SectionReviewCoverage coverage,
        bool hasNewlyRelevantSettings,
        int availableOptionalCount,
        IReadOnlyList<DesktopSectionGroupViewModel> groups,
        DesktopStrings strings)
    {
        SetProperty(ref _title, title, nameof(Title));
        SetProperty(ref _description, description, nameof(Description));
        if (!_fields.SequenceEqual(fields)) SetProperty(ref _fields, fields, nameof(Fields));
        SetProperty(ref _progress, progress, nameof(Progress));
        SetProperty(ref _configurationStatus, configurationStatus, nameof(ConfigurationStatus));
        SetProperty(ref _coverage, coverage, nameof(Coverage));
        SetProperty(ref _hasNewlyRelevantSettings, hasNewlyRelevantSettings, nameof(HasNewlyRelevantSettings));
        SetProperty(ref _availableOptionalCount, availableOptionalCount, nameof(AvailableOptionalCount));
        if (!_groups.SequenceEqual(groups))
        {
            SetProperty(ref _groups, groups, nameof(Groups));
            OnPropertyChanged(nameof(HasGroups));
        }
        SetProperty(ref _statusSummary, BuildStatusSummary(configurationStatus, coverage, strings), nameof(StatusSummary));
    }

    private static string BuildStatusSummary(
        SectionConfigurationStatus configurationStatus,
        SectionReviewCoverage coverage,
        DesktopStrings? strings)
    {
        if (strings is null) return $"{configurationStatus} · {coverage}";
        string healthKey = configurationStatus switch
        {
            SectionConfigurationStatus.Invalid => "SectionStatusInvalid",
            SectionConfigurationStatus.MissingRequired => "SectionStatusMissing",
            SectionConfigurationStatus.SecretDeferred => "SectionStatusDeferred",
            SectionConfigurationStatus.RequiredComplete => "SectionStatusReady",
            SectionConfigurationStatus.OutsideSelectedScope => "SectionStatusOutsideScope",
            _ => "SectionStatusOptional"
        };
        string coverageKey = coverage switch
        {
            SectionReviewCoverage.Reviewed => "SectionCoverageReviewed",
            SectionReviewCoverage.Opened => "SectionCoverageOpened",
            _ => "SectionCoverageUnvisited"
        };
        return $"{strings.Read(healthKey)} · {strings.Read(coverageKey)}";
    }
}
