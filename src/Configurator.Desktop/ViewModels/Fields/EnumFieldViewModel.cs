namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class EnumFieldViewModel : FieldViewModel
{
    private static readonly string[] UnsetChoice = ["Not supplied"];
    private int _selectedIndex;
    internal EnumFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(UnsetChoice
            .Concat(schema.Keywords.GetProperty("enum").EnumerateArray()
                .Select(choice => Display(choice) is { Length: > 0 } text ? text : "Empty string")).ToArray());
    }
    public IReadOnlyList<string> Choices { get; }
    public int SelectedIndex
    {
        get => RequiresMasking ? -1 : !IsProvided ? 0 : _selectedIndex;
        set
        {
            if (RequiresMasking || IsDisposed || value == SelectedIndex || value < 0 || value >= Choices.Count) return;
            Form.SetValue(FullPath(), value == 0 ? null : Schema.Keywords.GetProperty("enum")[value - 1]);
        }
    }
    internal override void Load(JsonElement? value)
    {
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
    protected override void NotifyValue() => OnPropertyChanged(nameof(SelectedIndex));
}
