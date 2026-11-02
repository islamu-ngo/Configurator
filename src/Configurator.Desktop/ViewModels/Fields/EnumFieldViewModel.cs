namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class EnumFieldViewModel : FieldViewModel
{
    private int _selectedIndex;
    private bool _loading;
    internal EnumFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(
            new[] { new EnumChoice(0, Strings["NotSupplied"]) }.Concat(schema.Keywords.GetProperty("enum").EnumerateArray()
                .Select((choice, index) => new EnumChoice(index + 1, Display(choice) is { Length: > 0 } text ? text : Strings["EmptyString"]))).ToArray());
    }
    public IReadOnlyList<EnumChoice> Choices { get; }
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
                Choices[0].UpdateLabel(Strings["NotSupplied"]);
                for (int index = 0; index < display.Choices.Count; index++)
                    Choices[index + 1].UpdateLabel(display.Choices[index].Label.Length == 0 ? Strings["EmptyString"] : display.Choices[index].Label);
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

}
