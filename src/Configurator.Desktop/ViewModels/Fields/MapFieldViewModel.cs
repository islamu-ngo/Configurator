namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

public sealed class MapFieldViewModel : FieldViewModel
{
    public const int PageSize = 25;
    private readonly ObservableCollection<FieldViewModel> _entries = [];
    private readonly ObservableCollection<FieldViewModel> _visibleChildren = [];
    private readonly ObservableCollection<FieldViewModel> _visibleEntries = [];
    private int _suppliedMembers;
    private int _pageIndex;
    private bool _isExpanded;
    private string _newKey = "";

    internal MapFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        _isExpanded = parent is null or RootFieldViewModel;
        Children = Array.AsReadOnly(schema.Properties
            .Select(property => FormFieldFactory.Project(property.Value, property.Key,
                schema.Required.Contains(property.Key, StringComparer.Ordinal), this, false)).ToArray());
        Entries = new(_entries);
        VisibleChildren = new(_visibleChildren);
        VisibleEntries = new(_visibleEntries);
        AddEntryCommand = new RelayCommand(AddEntry, () => !IsDisposed && CanEdit && _suppliedMembers < MaximumEntries &&
            !Schema.Properties.ContainsKey(NewKey) &&
            !_entries.Any(entry => string.Equals(entry.Id, NewKey, StringComparison.Ordinal)) && ValidKey());
        RemoveEntryCommand = new RelayCommand<FieldViewModel>(RemoveEntry,
            entry => !IsDisposed && CanEdit && entry is not null && _entries.Contains(entry));
        PreviousPageCommand = new RelayCommand(PreviousPage, () => CanPreviousPage);
        NextPageCommand = new RelayCommand(NextPage, () => CanNextPage);
    }

    public ReadOnlyObservableCollection<FieldViewModel> Entries { get; }
    public IReadOnlyList<FieldViewModel> Children { get; }
    public ReadOnlyObservableCollection<FieldViewModel> VisibleChildren { get; }
    public ReadOnlyObservableCollection<FieldViewModel> VisibleEntries { get; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (IsDisposed || !SetProperty(ref _isExpanded, value)) return;
            RefreshPage();
        }
    }
    public int PageNumber => _pageIndex + 1;
    public int PageCount => Math.Max(1, (Entries.Count + PageSize - 1) / PageSize);
    public bool HasPages => PageCount > 1;
    public bool CanPreviousPage => !IsDisposed && IsExpanded && _pageIndex > 0;
    public bool CanNextPage => !IsDisposed && IsExpanded && PageNumber < PageCount;
    public string PageText
    {
        get
        {
            int first = Entries.Count == 0 ? 0 : _pageIndex * PageSize + 1;
            int last = Math.Min(Entries.Count, (_pageIndex + 1) * PageSize);
            return Strings.Format("CollectionPageFormat", first, last, Entries.Count, PageNumber, PageCount);
        }
    }
    internal override IEnumerable<FieldViewModel> Descendants => Children.Concat(Entries);
    public int MaximumEntries => Schema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum)
        ? maximum.GetInt32() : CompositionLimits.DefaultMappingEntries;
    public string NewKey
    {
        get => _newKey;
        set
        {
            if (IsDisposed) return;
            if (SetProperty(ref _newKey, value ?? "")) AddEntryCommand.NotifyCanExecuteChanged();
        }
    }
    public string NewKeyLabel => Strings.NewKeyLabel;
    public string AddEntryLabel => Strings.AddEntryLabel;
    public string AddEntryName => Strings.AddEntryName(Title);
    public IRelayCommand AddEntryCommand { get; }
    public IRelayCommand<FieldViewModel> RemoveEntryCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }

    protected override void OnRefreshed()
    {
        OnPropertyChanged(nameof(NewKeyLabel));
        OnPropertyChanged(nameof(AddEntryLabel));
        OnPropertyChanged(nameof(AddEntryName));
        OnPropertyChanged(nameof(PageText));
    }

    internal override void Load(JsonElement? value)
    {
        string[] names = value is { ValueKind: JsonValueKind.Object } mapping
            ? mapping.EnumerateObject().Select(property => property.Name).ToArray() : [];
        _suppliedMembers = names.Length;
        names = names.Where(name => !Schema.Properties.ContainsKey(name)).ToArray();
        var retained = names.ToHashSet(StringComparer.Ordinal);
        for (int index = _entries.Count - 1; index >= 0; index--)
            if (!retained.Contains(_entries[index].Id))
            {
                _entries[index].Dispose();
                _entries.RemoveAt(index);
            }
        var existing = _entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            if (index < _entries.Count && _entries[index].Id == name) continue;
            if (existing.TryGetValue(name, out FieldViewModel? entry))
                _entries.Move(_entries.IndexOf(entry), index);
            else
            {
                _entries.Add(CreateEntry(name));
                if (index != _entries.Count - 1) _entries.Move(_entries.Count - 1, index);
            }
        }
        base.Load(value);
        RefreshPage();
        AddEntryCommand.NotifyCanExecuteChanged();
        RemoveEntryCommand.NotifyCanExecuteChanged();
        foreach (FieldViewModel entry in _entries) entry.RemoveCommand.NotifyCanExecuteChanged();
    }

    private bool ValidKey()
    {
        try { _ = DefinitionPath.FromSegments([.. FullPath().Segments, NewKey]); return true; }
        catch (DefinitionException) { return false; }
    }

    private FieldViewModel CreateEntry(string name)
    {
        var entry = FormFieldFactory.Project(Schema.AdditionalProperties!, name, false, this, false);
        entry.AdoptRoot(Root);
        entry.EditorIdentity = Form.GetEditorIdentity(DefinitionPath.FromSegments([.. FullPath().Segments, name]));
        return entry;
    }

    private void AddEntry()
    {
        if (!AddEntryCommand.CanExecute(null)) return;
        string key = NewKey;
        if (Form.AddMapEntry(FullPath(), key))
        {
            NewKey = "";
            _entries.Single(entry => entry.Id == key).RequestFocus();
        }
    }

    private void RemoveEntry(FieldViewModel? entry)
    {
        if (!RemoveEntryCommand.CanExecute(entry)) return;
        if (Form.EditValue(DefinitionPath.FromSegments([.. FullPath().Segments, entry!.Id]), null)) RequestFocus();
    }

    internal void RevealChild(FieldViewModel child)
    {
        _isExpanded = true;
        OnPropertyChanged(nameof(IsExpanded));
        int index = _entries.IndexOf(child);
        if (index >= 0) _pageIndex = index / PageSize;
        RefreshPage();
    }

    private void PreviousPage()
    {
        if (!CanPreviousPage) return;
        _pageIndex--;
        RefreshPage();
    }

    private void NextPage()
    {
        if (!CanNextPage) return;
        _pageIndex++;
        RefreshPage();
    }

    private void RefreshPage()
    {
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        FieldViewModel[] children = IsExpanded ? Children.ToArray() : [];
        FieldProjectionCollection.Synchronize(_visibleChildren, children);
        FieldViewModel[] entries = IsExpanded
            ? _entries.Skip(_pageIndex * PageSize).Take(PageSize).ToArray()
            : [];
        FieldProjectionCollection.Synchronize(_visibleEntries, entries);
        foreach (string property in new[] { nameof(PageNumber), nameof(PageCount), nameof(HasPages),
            nameof(CanPreviousPage), nameof(CanNextPage), nameof(PageText) }) OnPropertyChanged(property);
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    public override void RequestFocus()
    {
        if (IsDisposed) return;
        if (Descendants.FirstOrDefault(child => child.IsVisible && child.HasError) is { } invalid)
            invalid.RequestFocus();
        else SignalFocus();
    }
    public override void Dispose()
    {
        if (IsDisposed) return;
        NewKey = "";
        base.Dispose();
        _entries.Clear();
        _visibleChildren.Clear();
        _visibleEntries.Clear();
        AddEntryCommand.NotifyCanExecuteChanged();
        RemoveEntryCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
