namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;

public sealed class BooleanFieldViewModel : FieldViewModel
{
    internal BooleanFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking) { }
    public bool? Value
    {
        get => !RequiresMasking && bool.TryParse(Text, out bool value) ? value : null;
        set { if (!RequiresMasking) Text = value is { } supplied ? supplied ? "true" : "false" : ""; }
    }
    protected override void NotifyValue() => OnPropertyChanged(nameof(Value));
}
