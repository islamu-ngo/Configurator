namespace Configurator.Desktop.ViewModels;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Desktop.Messages;
using Configurator.Desktop.Resources;
using Configurator.Desktop.Services;
using Configurator.Presentation;

public enum CompactTableSortColumn
{
    Label,
    Section,
    Status,
    Origin
}

/// <summary>Projected dense table row over <see cref="CompactPropertyRow"/> with value-safe editing and redaction.</summary>
public sealed partial class CompactPropertyRowViewModel : ObservableObject
{
    private readonly CompactPropertyRow _source;
    private readonly Func<DefinitionPath, string?, bool> _edit;
    private readonly Action<DefinitionPath> _reset;
    private readonly Action<DefinitionPath> _omit;
    private readonly Action<DefinitionPath>? _openGuided;
    private readonly Action<DefinitionPath>? _inspect;
    private readonly ISecretClipboardService? _clipboard;
    private readonly string _locale;
    private string _editText;

    internal CompactPropertyRowViewModel(
        CompactPropertyRow source,
        string locale,
        Func<DefinitionPath, string?, bool> edit,
        Action<DefinitionPath> reset,
        Action<DefinitionPath> omit,
        Action<DefinitionPath>? openGuided,
        Action<DefinitionPath>? inspect,
        ISecretClipboardService? clipboard)
    {
        _source = source;
        _edit = edit;
        _reset = reset;
        _omit = omit;
        _openGuided = openGuided;
        _inspect = inspect;
        _clipboard = clipboard;
        _locale = locale;
        Strings = new DesktopStrings(locale);
        _editText = source.IsSensitiveMasked ? string.Empty : source.ValuePreview;
    }

    public DesktopStrings Strings { get; }
    public DefinitionPath Path => _source.DataPath;
    public string Identity => _source.EditorIdentity;
    public string SectionId => _source.SectionId;
    public string SectionTitle => _source.SectionTitle;
    public string Label => _source.PrimaryLabel;
    public string WireToken => _source.IsSensitiveMasked ? string.Empty : _source.WireToken;
    public string SecondaryLabel => _source.IsSensitiveMasked ? string.Empty : _source.SecondaryLabel;
    public bool HasSecondaryLabel => SecondaryLabel.Length > 0;
    public bool HasCopyableWireToken => !_source.IsSensitiveMasked && _source.HasCopyableWireToken && WireToken.Length > 0;
    public FieldSensitivity Sensitivity => _source.Sensitivity;
    public bool IsRequired => _source.IsRequired;
    public bool IsActive => _source.IsActive;
    public bool NeedsAttention => _source.HasError;
    public bool IsDeferred => _source.IsDeferred;
    public bool CanEdit => _source.CanEdit && !_source.IsSecretLocked;
    public bool IsReadOnly => _source.IsReadOnly;
    public bool IsSecretLocked => _source.IsSecretLocked;
    public bool IsMasked => _source.IsSensitiveMasked;
    public DefinitionAnswerOrigin Origin => _source.Origin;
    public string Units => _source.Units ?? string.Empty;
    public string Description => _source.Description ?? string.Empty;
    public string DefaultText => _source.DefaultText ?? string.Empty;
    public string IfOmittedDisplay => _source.IfOmittedDisplay ?? string.Empty;
    public bool IsDeprecated => _source.IsDeprecated;
    public string DeprecationGuidance => _source.DeprecationGuidance ?? string.Empty;
    public IReadOnlyList<DefinitionChoicePresentation> Choices => _source.IsSensitiveMasked ? [] : _source.Choices;
    public bool HasChoices => Choices.Count > 0 && CanEdit;
    public bool IsScalarTextEditable => CanEdit && !HasChoices && !IsMasked;
    public bool IsMaskedEditable => CanEdit && IsMasked;

    public string DisplayValue => _source.IsSensitiveMasked
        ? (_source.ValuePreview.Length > 0 ? Strings["CompactMaskedValue"] : Strings["CompactUnsetValue"])
        : (_source.ValuePreview.Length > 0 ? _source.ValuePreview : Strings["CompactUnsetValue"]);

    public string StatusLabel =>
        _source.HasError ? Strings["SectionStatusInvalid"]
        : !_source.IsActive ? Strings["SectionStatusOutsideScope"]
        : _source.IsDeferred ? Strings["SectionStatusDeferred"]
        : _source.IsRequired ? Strings["SectionStatusReady"]
        : Strings["SectionStatusOptional"];

    public string OriginLabel => PresentationStrings.Get("Origin" + _source.Origin, _locale);

    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value ?? string.Empty);
    }

    public DefinitionChoicePresentation? SelectedChoice
    {
        get => HasChoices ? Choices.FirstOrDefault(choice =>
            string.Equals(choice.WireToken, _source.ValuePreview, StringComparison.Ordinal)) : null;
        set
        {
            if (value is null || !HasChoices) return;
            _edit(Path, value.WireToken);
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void CommitEdit()
    {
        if (!CanEdit) return;
        string pending = EditText;
        _edit(Path, pending);
        if (IsMasked) EditText = string.Empty;
    }

    [RelayCommand]
    private void Reset() => _reset(Path);

    [RelayCommand]
    private void Omit() => _omit(Path);

    [RelayCommand]
    private void OpenInGuided() => _openGuided?.Invoke(Path);

    [RelayCommand]
    private void Inspect() => _inspect?.Invoke(Path);

    [RelayCommand]
    private async Task CopyWireKeyAsync()
    {
        if (!HasCopyableWireToken || _clipboard is null) return;
        await _clipboard.CopyAsync(WireToken);
    }
}

/// <summary>Virtualized dense table workspace synchronized with <see cref="FormWorkspaceViewModel"/>.</summary>
public sealed partial class CompactSettingsTableViewModel : ObservableObject, IDisposable
{
    private readonly FormWorkspaceViewModel _workspace;
    private readonly DesktopNavigationViewModel _navigation;
    private readonly IMessenger _messenger;
    private readonly ISecretClipboardService? _clipboard;
    private Action<DefinitionPath>? _inspectPath;
    private DefinitionFormModel? _form;
    private bool _disposed;

    [ObservableProperty]
    private string _searchQuery = string.Empty;
    [ObservableProperty]
    private DefinitionDiscoveryFilter _filter = DefinitionDiscoveryFilter.All;
    [ObservableProperty]
    private bool _includeInactive;
    [ObservableProperty]
    private bool _currentSectionOnly;
    [ObservableProperty]
    private CompactTableSortColumn _sortColumn = CompactTableSortColumn.Label;
    [ObservableProperty]
    private bool _sortAscending = true;
    [ObservableProperty]
    private IReadOnlyList<CompactPropertyRowViewModel> _rows = [];
    [ObservableProperty]
    private CompactPropertyRowViewModel? _selectedRow;

    public CompactSettingsTableViewModel(
        FormWorkspaceViewModel workspace,
        DesktopNavigationViewModel navigation,
        IMessenger messenger,
        ISecretClipboardService? clipboard = null,
        Action<DefinitionPath>? inspectPath = null)
    {
        _workspace = workspace;
        _navigation = navigation;
        _messenger = messenger;
        _clipboard = clipboard;
        _inspectPath = inspectPath;
        _workspace.PropertyChanged += WorkspacePropertyChanged;
        _navigation.PropertyChanged += NavigationPropertyChanged;
        _messenger.Register<CompactSettingsTableViewModel, FormInputsChangedMessage>(this,
            static (table, _) => table.RefreshRows());
        _messenger.Register<CompactSettingsTableViewModel, WorkspaceInvalidatedMessage>(this,
            static (table, _) => table.RefreshRows());
        AttachForm(_workspace.Form);
    }

    public DesktopStrings Strings => _workspace.Strings;

    public IReadOnlyList<string> SortColumnOptions =>
    [
        Strings["CompactSortLabelOption"],
        Strings["CompactSortSectionOption"],
        Strings["CompactSortStatusOption"],
        Strings["CompactSortOriginOption"]
    ];

    public int SortIndex
    {
        get => (int)SortColumn;
        set
        {
            if (value is >= 0 and <= 3) SortColumn = (CompactTableSortColumn)value;
        }
    }

    public string SortDirectionLabel => Strings[SortAscending ? "CompactSortAscendingLabel" : "CompactSortDescendingLabel"];
    public string RowsCountSummary => Strings.Format("CompactRowsCount", Rows.Count);

    public bool IsFilterAll => Filter == DefinitionDiscoveryFilter.All;
    public bool IsFilterInvalid => Filter == DefinitionDiscoveryFilter.Invalid;
    public bool IsFilterMissing => Filter == DefinitionDiscoveryFilter.MissingRequired;
    public bool IsFilterModified => Filter == DefinitionDiscoveryFilter.Modified;
    public bool IsFilterDeferred => Filter == DefinitionDiscoveryFilter.DeferredSecret;

    internal void SetInspectCallback(Action<DefinitionPath> inspectPath) => _inspectPath = inspectPath;

    public bool EditValue(DefinitionPath path, string? text)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_disposed || _form is null) return false;
        bool updated = _form.EditCompactValue(path, text);
        if (updated) RefreshRows();
        return updated;
    }

    [RelayCommand]
    private void SetFilter(DefinitionDiscoveryFilter filter)
    {
        if (_disposed || !Enum.IsDefined(filter)) return;
        Filter = filter;
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        if (_disposed) return;
        SortAscending = !SortAscending;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        if (_disposed) return;
        SearchQuery = string.Empty;
        Filter = DefinitionDiscoveryFilter.All;
        IncludeInactive = false;
        CurrentSectionOnly = false;
    }

    partial void OnSearchQueryChanged(string value) => RefreshRows();
    partial void OnFilterChanged(DefinitionDiscoveryFilter value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterInvalid));
        OnPropertyChanged(nameof(IsFilterMissing));
        OnPropertyChanged(nameof(IsFilterModified));
        OnPropertyChanged(nameof(IsFilterDeferred));
        RefreshRows();
    }
    partial void OnIncludeInactiveChanged(bool value) => RefreshRows();
    partial void OnCurrentSectionOnlyChanged(bool value) => RefreshRows();
    partial void OnSortColumnChanged(CompactTableSortColumn value)
    {
        OnPropertyChanged(nameof(SortIndex));
        RefreshRows();
    }
    partial void OnSortAscendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortDirectionLabel));
        RefreshRows();
    }
    partial void OnSelectedRowChanged(CompactPropertyRowViewModel? value)
    {
        if (value is not null) _inspectPath?.Invoke(value.Path);
    }

    public void RefreshRows()
    {
        if (_disposed || _form is null)
        {
            Rows = [];
            OnPropertyChanged(nameof(RowsCountSummary));
            return;
        }
        string? sectionId = CurrentSectionOnly ? _navigation.SelectedSection?.Id : null;
        string? query = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim();
        IReadOnlyList<CompactPropertyRow> raw = _form.GetCompactRows(IncludeInactive, sectionId, query, Filter);
        IEnumerable<CompactPropertyRow> ordered = SortColumn switch
        {
            CompactTableSortColumn.Section => SortAscending
                ? raw.OrderBy(r => r.SectionTitle, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(r => r.SectionTitle, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase),
            CompactTableSortColumn.Status => SortAscending
                ? raw.OrderBy(StatusRank).ThenBy(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(StatusRank).ThenByDescending(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase),
            CompactTableSortColumn.Origin => SortAscending
                ? raw.OrderBy(r => r.Origin).ThenBy(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(r => r.Origin).ThenByDescending(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase),
            _ => SortAscending
                ? raw.OrderBy(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(r => r.PrimaryLabel, StringComparer.OrdinalIgnoreCase)
        };

        string locale = _form.Locale;
        DefinitionPath? selectedPath = SelectedRow?.Path;
        CompactPropertyRowViewModel[] projected = ordered
            .Select(row => new CompactPropertyRowViewModel(
                row,
                locale,
                EditValue,
                path => { _form?.ResetValue(path); RefreshRows(); },
                path => { _form?.EditCompactValue(path, null); RefreshRows(); },
                path => _navigation.FocusInGuided(path),
                path => _inspectPath?.Invoke(path),
                _clipboard))
            .ToArray();
        Rows = Array.AsReadOnly(projected);
        OnPropertyChanged(nameof(RowsCountSummary));
        if (selectedPath is not null)
            SelectedRow = projected.FirstOrDefault(r => r.Path == selectedPath);
    }

    private static int StatusRank(CompactPropertyRow row) =>
        row.HasError ? 0 : !row.IsActive ? 4 : row.IsDeferred ? 1 : row.IsRequired ? 2 : 3;

    private void AttachForm(DefinitionFormModel? next)
    {
        if (ReferenceEquals(_form, next)) return;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
        _form = next;
        if (_form is not null) _form.ViewChanged += FormViewChanged;
        RefreshRows();
    }

    private void WorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(FormWorkspaceViewModel.Form)) AttachForm(_workspace.Form);
        else if (args.PropertyName is nameof(FormWorkspaceViewModel.Strings) or nameof(FormWorkspaceViewModel.NamingMode))
        {
            OnPropertyChanged(nameof(Strings));
            OnPropertyChanged(nameof(SortColumnOptions));
            OnPropertyChanged(nameof(SortDirectionLabel));
            RefreshRows();
        }
    }

    private void NavigationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(DesktopNavigationViewModel.SelectedSection) && CurrentSectionOnly)
            RefreshRows();
        else if (args.PropertyName == nameof(DesktopNavigationViewModel.ConfigureMode) && _navigation.IsCompactMode)
            RefreshRows();
    }

    private void FormViewChanged(object? sender, EventArgs args) => RefreshRows();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.PropertyChanged -= WorkspacePropertyChanged;
        _navigation.PropertyChanged -= NavigationPropertyChanged;
        if (_form is not null) _form.ViewChanged -= FormViewChanged;
        _messenger.UnregisterAll(this);
        Rows = [];
    }
}
