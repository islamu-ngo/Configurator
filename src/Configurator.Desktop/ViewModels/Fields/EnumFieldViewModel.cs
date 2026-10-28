namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Core.Definitions;
using CommunityToolkit.Mvvm.ComponentModel;

public sealed class EnumFieldViewModel : FieldViewModel
{
    private int _selectedIndex;
    private bool _loading;
    internal EnumFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(
            new[] { new Choice(0, "Not supplied") }.Concat(schema.Keywords.GetProperty("enum").EnumerateArray()
                .Select((choice, index) => new Choice(index + 1, Display(choice) is { Length: > 0 } text ? text : "Empty string"))).ToArray());
    }
    public IReadOnlyList<Choice> Choices { get; }
    public int SelectedIndex
    {
        get => RequiresMasking ? -1 : !IsProvided ? 0 : _selectedIndex;
        set
        {
            if (RequiresMasking || IsDisposed || _loading || value < 0 || value >= Choices.Count) return;
            Form.SetValue(FullPath(), value == 0 ? null : Schema.Keywords.GetProperty("enum")[value - 1]);
        }
    }
    internal override void Load(JsonElement? value)
    {
        _loading = true;
        try
        {
            if (!RequiresMasking && Form.GetEditorPresentation(FullPath()) is { } display)
            {
                for (int index = 0; index < display.Choices.Count; index++)
                    Choices[index + 1].UpdateLabel(display.Choices[index].Label.Length == 0 ? "Empty string" : display.Choices[index].Label);
            }
            _selectedIndex = -1;
            if (!RequiresMasking && value is { } supplied)
            {
                int index = 1;
                foreach (JsonElement choice in Schema.Keywords.GetProperty("enum").EnumerateArray())
                {
                    if (JsonElement.DeepEquals(choice, supplied))
                    {
                        _selectedIndex = index;
                        break;
                    }
                    index++;
                }
            }
            base.Load(value);
        }
        finally { _loading = false; }
    }
    protected override void NotifyValue() => OnPropertyChanged(nameof(SelectedIndex));

    public sealed class Choice : ObservableObject
    {
        private string _label;
        internal Choice(int index, string label) { Index = index; _label = label; }
        public int Index { get; }
        public string Label => _label;
        internal void UpdateLabel(string label) => SetProperty(ref _label, label, nameof(Label));
    }
}
