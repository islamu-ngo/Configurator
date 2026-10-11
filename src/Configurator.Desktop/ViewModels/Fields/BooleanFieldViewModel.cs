namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class BooleanFieldViewModel : FieldViewModel
{
    internal BooleanFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem) { }
    public bool? Value
    {
        get => !RequiresMasking && bool.TryParse(Text, out bool value) ? value : null;
        set { if (!RequiresMasking) Text = value is { } supplied ? supplied ? "true" : "false" : ""; }
    }
    protected override void NotifyValue() => OnPropertyChanged(nameof(Value));
}
