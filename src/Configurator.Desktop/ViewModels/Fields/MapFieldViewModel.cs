namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;

public sealed class MapFieldViewModel : FieldViewModel
{
    private readonly ObservableCollection<FieldViewModel> _entries = [];
    private string _newKey = "";

    internal MapFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Entries = new(_entries);
        AddEntryCommand = new RelayCommand(AddEntry, () => !IsDisposed && _entries.Count < MaximumEntries &&
            !_entries.Any(entry => entry.Id == NewKey) && ValidKey());
        RemoveEntryCommand = new RelayCommand<FieldViewModel>(RemoveEntry,
            entry => !IsDisposed && entry is not null && _entries.Contains(entry));
    }

    public ReadOnlyObservableCollection<FieldViewModel> Entries { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Entries;
    public int MaximumEntries => Schema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum)
        ? maximum.GetInt32() : SetupCompositionLimits.DefaultMappingEntries;
    public string NewKey
    {
        get => _newKey;
        set
        {
            if (IsDisposed) return;
            if (SetProperty(ref _newKey, value ?? "")) AddEntryCommand.NotifyCanExecuteChanged();
        }
    }
    public string NewKeyLabel => DesktopActionText.NewKeyLabel;
    public string AddEntryLabel => DesktopActionText.AddEntryLabel;
    public string AddEntryName => DesktopActionText.AddEntryName(Title);
    public IRelayCommand AddEntryCommand { get; }
    public IRelayCommand<FieldViewModel> RemoveEntryCommand { get; }

    internal override void Load(JsonElement? value)
    {
        string[] names = value is { ValueKind: JsonValueKind.Object } mapping
            ? mapping.EnumerateObject().Select(property => property.Name).ToArray() : [];
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
        AddEntryCommand.NotifyCanExecuteChanged();
        RemoveEntryCommand.NotifyCanExecuteChanged();
        foreach (FieldViewModel entry in _entries) entry.RemoveCommand.NotifyCanExecuteChanged();
    }

    private bool ValidKey()
    {
        try { _ = DefinitionPath.FromSegments([.. FullPath(), NewKey]); return true; }
        catch (ArgumentException) { return false; }
    }

    private FieldViewModel CreateEntry(string name)
    {
        var entry = FormFieldFactory.Project(Schema.AdditionalProperties!, name, false, this, false);
        entry.AdoptRoot(Root);
        entry.EditorIdentity = Form.GetEditorIdentity([.. FullPath(), name]);
        return entry;
    }

    private void AddEntry()
    {
        if (!AddEntryCommand.CanExecute(null)) return;
        string key = NewKey;
        JsonElement value = Schema.AdditionalProperties!.Type switch
        {
            DefinitionNodeType.Text => JsonSerializer.SerializeToElement(""),
            DefinitionNodeType.Boolean => JsonSerializer.SerializeToElement(false),
            DefinitionNodeType.WholeNumber or DefinitionNodeType.Number => JsonSerializer.SerializeToElement(0),
            DefinitionNodeType.Sequence => JsonSerializer.SerializeToElement(Array.Empty<object>()),
            _ => JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        };
        if (Form.SetValue([.. FullPath(), key], value))
        {
            NewKey = "";
            _entries.Single(entry => entry.Id == key).RequestFocus();
        }
    }

    private void RemoveEntry(FieldViewModel? entry)
    {
        if (!RemoveEntryCommand.CanExecute(entry)) return;
        if (Form.EditValue([.. FullPath(), entry!.Id], null)) RequestFocus();
    }

    public override void RequestFocus() => SignalFocus();
    public override void Dispose()
    {
        if (IsDisposed) return;
        NewKey = "";
        base.Dispose();
        _entries.Clear();
        AddEntryCommand.NotifyCanExecuteChanged();
        RemoveEntryCommand.NotifyCanExecuteChanged();
    }
}
