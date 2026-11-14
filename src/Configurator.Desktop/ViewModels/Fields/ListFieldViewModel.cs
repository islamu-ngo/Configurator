namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;

public sealed class ListFieldViewModel : FieldViewModel
{
    public const int PageSize = 25;
    private readonly ObservableCollection<FieldViewModel> _items = [];
    private readonly ObservableCollection<FieldViewModel> _visibleItems = [];
    private int _pageIndex;
    private bool _isExpanded;
    private bool _hasDuplicateKeys;
    private string _duplicateKeysWarning = string.Empty;

    internal ListFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        _isExpanded = parent is null or RootFieldViewModel;
        Items = new ReadOnlyObservableCollection<FieldViewModel>(_items);
        VisibleItems = new(_visibleItems);
        AddItemCommand = new RelayCommand(AddItem, () => !IsDisposed && CanEdit && Items.Count < MaximumItems);
        RemoveItemCommand = new RelayCommand<FieldViewModel>(RemoveItem, item =>
            !IsDisposed && CanEdit && Items.Count > MinimumItems && item is not null && _items.Contains(item));
        PreviousPageCommand = new RelayCommand(PreviousPage, () => CanPreviousPage);
        NextPageCommand = new RelayCommand(NextPage, () => CanNextPage);
    }

    public ReadOnlyObservableCollection<FieldViewModel> Items { get; }
    public ReadOnlyObservableCollection<FieldViewModel> VisibleItems { get; }
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
    public int PageCount => Math.Max(1, (Items.Count + PageSize - 1) / PageSize);
    public bool HasPages => PageCount > 1;
    public bool CanPreviousPage => !IsDisposed && IsExpanded && _pageIndex > 0;
    public bool CanNextPage => !IsDisposed && IsExpanded && PageNumber < PageCount;
    public string PageText
    {
        get
        {
            int first = Items.Count == 0 ? 0 : _pageIndex * PageSize + 1;
            int last = Math.Min(Items.Count, (_pageIndex + 1) * PageSize);
            return Strings.Format("CollectionPageFormat", first, last, Items.Count, PageNumber, PageCount);
        }
    }
    public string AddItemName => Strings.Format("AddItemName", Title);
    public bool HasDuplicateKeys => _hasDuplicateKeys;
    public string DuplicateKeysWarning => _duplicateKeysWarning;
    protected override void OnRefreshed()
    {
        OnPropertyChanged(nameof(AddItemName));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(DuplicateKeysWarning));
    }
    public int MaximumItems => Schema.Keywords.TryGetProperty("maxItems", out var maximum) ? maximum.GetInt32() : 4096;
    public int MinimumItems => Schema.Keywords.TryGetProperty("minItems", out var minimum) ? minimum.GetInt32() : 0;
    public IRelayCommand AddItemCommand { get; }
    public IRelayCommand<FieldViewModel> RemoveItemCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Items;

    internal override void Load(JsonElement? value)
    {
        int count = value is { ValueKind: JsonValueKind.Array } array ? array.GetArrayLength() : 0;
        IReadOnlyList<string> path = FullPath().Segments;
        string[] identities = Enumerable.Range(0, count)
            .Select(index => Form.GetEditorIdentity(DefinitionPath.FromSegments(
                [.. path, index.ToString(CultureInfo.InvariantCulture)]))).ToArray();
        var existing = _items.ToDictionary(item => item.EditorIdentity, StringComparer.Ordinal);
        var retained = identities.ToHashSet(StringComparer.Ordinal);
        for (int index = _items.Count - 1; index >= 0; index--)
            if (!retained.Contains(_items[index].EditorIdentity))
            {
                _items[index].Dispose();
                _items.RemoveAt(index);
            }
        for (int index = 0; index < count; index++)
        {
            if (existing.TryGetValue(identities[index], out var item))
            {
                int previous = _items.IndexOf(item);
                if (previous != index) _items.Move(previous, index);
            }
            else
            {
                _items.Add(CreateItem(identities[index]));
                if (index != _items.Count - 1) _items.Move(_items.Count - 1, index);
            }
        }

        ApplyItemTitlesAndUniqueChecks(value as JsonElement?);

        base.Load(value);
        RefreshPage();
        NotifyCommands();
    }

    private void ApplyItemTitlesAndUniqueChecks(JsonElement? arrayValue)
    {
        string? titlePointer = !RequiresMasking ? Schema.PresentationMetadata.ItemTitlePointer : null;
        DefinitionPath? titlePath = TryParseRelativePath(titlePointer);
        IReadOnlyList<DefinitionPath> uniquePaths = !RequiresMasking ? Schema.UniqueBy : [];
        var seenTuples = new HashSet<string>(StringComparer.Ordinal);
        bool duplicate = false;

        if (arrayValue is { ValueKind: JsonValueKind.Array } array)
        {
            for (int index = 0; index < _items.Count && index < array.GetArrayLength(); index++)
            {
                JsonElement element = array[index];
                string? resolvedTitle = titlePath is not null
                    ? ResolveScalarPointerText(element, titlePath)
                    : null;
                _items[index].SetItemDisplayTitle(resolvedTitle);

                if (uniquePaths.Count > 0)
                {
                    var keyParts = new string[uniquePaths.Count];
                    bool allPresent = true;
                    for (int k = 0; k < uniquePaths.Count; k++)
                    {
                        string? part = ResolveScalarPointerText(element, uniquePaths[k]);
                        if (string.IsNullOrEmpty(part))
                        {
                            allPresent = false;
                            break;
                        }
                        keyParts[k] = part;
                    }
                    if (allPresent)
                    {
                        string tuple = string.Join('\x1f', keyParts);
                        if (!seenTuples.Add(tuple)) duplicate = true;
                    }
                }
            }
        }
        else
        {
            foreach (FieldViewModel item in _items) item.SetItemDisplayTitle(null);
        }

        if (_hasDuplicateKeys != duplicate)
        {
            _hasDuplicateKeys = duplicate;
            OnPropertyChanged(nameof(HasDuplicateKeys));
        }
        string warning = duplicate ? Strings.UniqueDuplicateWarning : string.Empty;
        if (!string.Equals(_duplicateKeysWarning, warning, StringComparison.Ordinal))
        {
            _duplicateKeysWarning = warning;
            OnPropertyChanged(nameof(DuplicateKeysWarning));
        }
    }

    private static DefinitionPath? TryParseRelativePath(string? pointer)
    {
        if (string.IsNullOrWhiteSpace(pointer)) return null;
        try { return DefinitionPath.Parse(pointer.StartsWith('/') ? pointer : "/" + pointer); }
        catch (DefinitionException) { return null; }
    }

    private static string? ResolveScalarPointerText(JsonElement root, DefinitionPath path)
    {
        JsonElement current = root;
        foreach (string segment in path.Segments)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out JsonElement prop))
                current = prop;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int idx) && idx >= 0 && idx < current.GetArrayLength())
                current = current[idx];
            else
                return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.GetRawText(),
            _ => null
        };
    }
    private FieldViewModel CreateItem(string identity)
    {
        var item = FormFieldFactory.Project(Schema.Items!, "item", true, this, true);
        item.EditorIdentity = identity;
        item.AdoptRoot(Root);
        return item;
    }
    private void AddItem()
    {
        if (!AddItemCommand.CanExecute(null)) return;
        if (Form.AddListItem(FullPath())) Items.Last().RequestFocus();
    }
    private void RemoveItem(FieldViewModel? item)
    {
        if (item is null || !RemoveItemCommand.CanExecute(item)) return;
        int index = _items.IndexOf(item);
        if (Form.RemoveListItem(FullPath(), index)) RequestAddFocus();
    }
    public void RequestAddFocus() => SignalFocus();

    internal void RevealChild(FieldViewModel child)
    {
        int index = _items.IndexOf(child);
        if (index < 0) return;
        _isExpanded = true;
        OnPropertyChanged(nameof(IsExpanded));
        _pageIndex = index / PageSize;
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
        FieldViewModel[] page = IsExpanded
            ? _items.Skip(_pageIndex * PageSize).Take(PageSize).ToArray()
            : [];
        FieldProjectionCollection.Synchronize(_visibleItems, page);
        foreach (string property in new[] { nameof(PageNumber), nameof(PageCount), nameof(HasPages),
            nameof(CanPreviousPage), nameof(CanNextPage), nameof(PageText) }) OnPropertyChanged(property);
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        AddItemCommand.NotifyCanExecuteChanged();
        RemoveItemCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.RemoveCommand.NotifyCanExecuteChanged();
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        _visibleItems.Clear();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
