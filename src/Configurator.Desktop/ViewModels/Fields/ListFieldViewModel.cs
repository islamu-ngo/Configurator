namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

public sealed class ListFieldViewModel : FieldViewModel
{
    private readonly ObservableCollection<FieldViewModel> _items = [];
    internal ListFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem)
    {
        Items = new ReadOnlyObservableCollection<FieldViewModel>(_items);
        AddItemCommand = new RelayCommand(AddItem, () => !IsDisposed && Items.Count < MaximumItems);
        RemoveItemCommand = new RelayCommand<FieldViewModel>(RemoveItem, item =>
            !IsDisposed && Items.Count > MinimumItems && item is not null && _items.Contains(item));
    }
    public ReadOnlyObservableCollection<FieldViewModel> Items { get; }
    public string AddItemName => $"Add item to {Title}";
    public int MaximumItems => Schema.TryGetProperty("maxItems", out var maximum) ? maximum.GetInt32() : 4096;
    public int MinimumItems => Schema.TryGetProperty("minItems", out var minimum) ? minimum.GetInt32() : 0;
    public IRelayCommand AddItemCommand { get; }
    public IRelayCommand<FieldViewModel> RemoveItemCommand { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Items;

    internal override void Load(JsonElement? value)
    {
        int count = value is { ValueKind: JsonValueKind.Array } array ? array.GetArrayLength() : 0;
        IReadOnlyList<string> path = FullPath();
        string[] identities = Enumerable.Range(0, count)
            .Select(index => Form.GetEditorIdentity([.. path, index.ToString(CultureInfo.InvariantCulture)])).ToArray();
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
        base.Load(value);
        NotifyCommands();
    }
    private FieldViewModel CreateItem(string identity)
    {
        var item = FormFieldFactory.Project(Form.GetListItemMetadata(FullPath()), this, true);
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
    private void NotifyCommands()
    {
        AddItemCommand.NotifyCanExecuteChanged();
        RemoveItemCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.RemoveCommand.NotifyCanExecuteChanged();
    }
}
