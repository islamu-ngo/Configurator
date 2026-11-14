namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;

public sealed class BooleanFieldViewModel : FieldViewModel
{
    private int _selectedIndex;
    private bool _loading;

    internal BooleanFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Choices = RequiresMasking ? [] : Array.AsReadOnly(
            new[] { new BooleanChoice(Strings["NotSupplied"]), new BooleanChoice(Strings["Yes"]), new BooleanChoice(Strings["No"]) });
        OmitSwitchCommand = new RelayCommand(() =>
        {
            if (!RequiresMasking && !IsDisposed && CanEdit)
            {
                MarkEdited();
                Form.SetValue(FullPath(), null);
            }
        }, () => !RequiresMasking && !IsDisposed && CanEdit && IsProvided);
    }

    public IReadOnlyList<BooleanChoice> Choices { get; }
    public IRelayCommand OmitSwitchCommand { get; }
    public bool IsSwitchWidget => IsPublic && !RequiresMasking && Presentation?.Widget == DefinitionFieldWidget.Switch;
    public bool IsSelectWidget => IsPublic && !RequiresMasking && !IsSwitchWidget;
    public bool IsOmitted => !IsProvided;
    public string TrueLabel => Choices.Count > 1 ? Choices[1].Label : Strings["Yes"];
    public string FalseLabel => Choices.Count > 2 ? Choices[2].Label : Strings["No"];

    public int SelectedIndex
    {
        get => RequiresMasking ? -1 : !IsProvided ? 0 : _selectedIndex;
        set
        {
            if (RequiresMasking || IsDisposed || !CanEdit || _loading || value < 0 || value >= Choices.Count) return;
            if (value != SelectedIndex) MarkEdited();
            Form.SetValue(FullPath(), value == 0 ? null : JsonSerializer.SerializeToElement(value == 1));
        }
    }

    public bool? Value
    {
        get => !RequiresMasking && bool.TryParse(Text, out bool value) ? value : null;
        set
        {
            if (!RequiresMasking) SelectedIndex = value is null ? 0 : value.Value ? 1 : 2;
        }
    }

    public bool? SwitchValue
    {
        get => Value;
        set
        {
            if (RequiresMasking || IsDisposed || !CanEdit || _loading) return;
            if (value is null) SelectedIndex = 0;
            else SelectedIndex = value.Value ? 1 : 2;
        }
    }

    internal override void Load(JsonElement? value)
    {
        _loading = true;
        try
        {
            if (!RequiresMasking)
            {
                Choices[0].UpdateLabel(Strings["NotSupplied"]);
                Choices[1].UpdateLabel(Presentation?.BooleanLabels.TrueLabel ?? Strings["Yes"]);
                Choices[2].UpdateLabel(Presentation?.BooleanLabels.FalseLabel ?? Strings["No"]);
            }
            _selectedIndex = value is { ValueKind: JsonValueKind.True } ? 1 :
                value is { ValueKind: JsonValueKind.False } ? 2 : 0;
            base.Load(value);
        }
        finally { _loading = false; }
    }

    protected override void NotifyValue()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(SwitchValue));
        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(IsSwitchWidget));
        OnPropertyChanged(nameof(IsSelectWidget));
        OnPropertyChanged(nameof(IsOmitted));
        OnPropertyChanged(nameof(TrueLabel));
        OnPropertyChanged(nameof(FalseLabel));
        OmitSwitchCommand.NotifyCanExecuteChanged();
    }
}

public sealed class BooleanChoice : ObservableObject
{
    private string _label;
    internal BooleanChoice(string label) => _label = label;
    public string Label => _label;
    internal void UpdateLabel(string label) => SetProperty(ref _label, label, nameof(Label));
    public override string ToString() => Label;
}
