namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;

public sealed class StringFieldViewModel : FieldViewModel
{
    internal StringFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking) { }
    public int MaxLength => 65_536;
}
