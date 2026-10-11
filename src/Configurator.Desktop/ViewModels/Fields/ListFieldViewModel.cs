namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Input;
using Configurator.Presentation;

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
        foreach (var item in _items) item.Dispose();
        _items.Clear();
        if (value is { ValueKind: JsonValueKind.Array } array)
            foreach (var item in array.EnumerateArray()) _items.Add(CreateItem(item));
        SetStructuredText(value);
        NotifyCommands();
    }
    private FieldViewModel CreateItem(JsonElement value)
    {
        string type = Schema.GetProperty("itemType").GetString()!;
        var metadata = new JsonObject
        {
            ["id"] = "item", ["title"] = $"{Title} item", ["type"] = type,
            ["sensitivity"] = RequiresMasking ? "secret" : "public", ["disclosure"] = "guided"
        };
        if (type == "object") metadata["properties"] = JsonNode.Parse(Schema.GetProperty("properties").GetRawText());
        var item = FormFieldFactory.Project(JsonSerializer.SerializeToElement(metadata), this, true);
        item.Load(value);
        item.AdoptRoot(Root);
        return item;
    }
    private void AddItem()
    {
        if (!AddItemCommand.CanExecute(null)) return;
        string type = Schema.GetProperty("itemType").GetString()!;
        JsonElement value = type == "object" ? DefinitionCollectionItem.CreateObject(Schema)
            : type switch
            {
                "string" => JsonSerializer.SerializeToElement(""),
                "boolean" => JsonSerializer.SerializeToElement(false),
                _ => JsonSerializer.SerializeToElement(0)
            };
        var item = CreateItem(value);
        _items.Add(item);
        MarkProvided();
        Commit();
        NotifyCommands();
        item.RequestFocus();
    }
    private void RemoveItem(FieldViewModel? item)
    {
        if (item is null || !RemoveItemCommand.CanExecute(item)) return;
        int index = _items.IndexOf(item);
        _items.RemoveAt(index);
        item.Dispose();
        MarkProvided();
        Commit();
        NotifyCommands();
        RequestAddFocus();
    }
    public void RequestAddFocus() => SignalFocus();
    private void NotifyCommands()
    {
        AddItemCommand.NotifyCanExecuteChanged();
        RemoveItemCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.RemoveCommand.NotifyCanExecuteChanged();
    }
    internal override bool TryValue(out JsonElement? value)
    {
        value = null;
        if (IsStructuredInvalid) return false;
        if (!IsProvided) return true;
        var values = new List<JsonElement>();
        foreach (var item in Items)
        {
            if (!item.TryValue(out var supplied) || supplied is null) return false;
            values.Add(supplied.Value);
        }
        value = JsonSerializer.SerializeToElement(values);
        return true;
    }
}
