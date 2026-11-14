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
            new[] { new EnumChoice(0, Strings["NotSupplied"], select: SelectChoiceIndex) }
                .Concat(schema.Keywords.GetProperty("enum").EnumerateArray()
                    .Select((choice, index) => new EnumChoice(
                        index + 1,
                        Display(choice) is { Length: > 0 } text ? text : Strings["EmptyString"],
                        select: SelectChoiceIndex)))
                .ToArray());
    }

    public IReadOnlyList<EnumChoice> Choices { get; }
    public bool IsRadioWidget => IsPublic && !RequiresMasking && Presentation?.Widget == DefinitionFieldWidget.Radio;
    public bool IsSelectWidget => IsPublic && !RequiresMasking && !IsRadioWidget;

    public int SelectedIndex
    {
        get => RequiresMasking ? -1 : !IsProvided ? 0 : _selectedIndex;
        set
        {
            if (RequiresMasking || IsDisposed || !CanEdit || _loading || value < 0 || value >= Choices.Count) return;
            if (value != SelectedIndex) MarkEdited();
            Form.SetValue(FullPath(), value == 0 ? null : Schema.Keywords.GetProperty("enum")[value - 1]);
        }
    }

    public string SelectedChoiceExplanation
    {
        get
        {
            int idx = SelectedIndex;
            if (RequiresMasking || idx <= 0 || idx >= Choices.Count) return string.Empty;
            return Choices[idx].Explanation ?? string.Empty;
        }
    }

    public string SelectedChoiceDeprecation
    {
        get
        {
            int idx = SelectedIndex;
            if (RequiresMasking || idx <= 0 || idx >= Choices.Count) return string.Empty;
            EnumChoice choice = Choices[idx];
            return !string.IsNullOrWhiteSpace(choice.DeprecationGuidance)
                ? Strings.DeprecatedText(choice.DeprecationGuidance!)
                : choice.IsDeprecated ? Strings["DeprecatedBadge"] : string.Empty;
        }
    }

    private void SelectChoiceIndex(int index)
    {
        if (!_loading) SelectedIndex = index;
    }

    internal override void Load(JsonElement? value)
    {
        _loading = true;
        try
        {
            if (!RequiresMasking && Form.GetEditorPresentation(FullPath()) is { } display)
            {
                Choices[0].UpdateLabel(Strings["NotSupplied"]);
                for (int index = 0; index < display.Choices.Count && index + 1 < Choices.Count; index++)
                {
                    var item = display.Choices[index];
                    string label = item.Label.Length == 0 ? Strings["EmptyString"] : item.Label;
                    Choices[index + 1].UpdateMetadata(label, item.Explanation, item.IsDeprecated, item.DeprecationGuidance);
                }
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
            int current = SelectedIndex;
            foreach (EnumChoice choice in Choices)
                choice.SyncSelected(choice.Index == current);
        }
        finally { _loading = false; }
    }

    protected override void NotifyValue()
    {
        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(IsRadioWidget));
        OnPropertyChanged(nameof(IsSelectWidget));
        OnPropertyChanged(nameof(SelectedChoiceExplanation));
        OnPropertyChanged(nameof(SelectedChoiceDeprecation));
    }
}
