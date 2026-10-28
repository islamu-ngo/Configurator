namespace Configurator.Desktop.ViewModels.Fields;

using Configurator.Core.Definitions;

public sealed class ObjectFieldViewModel : FieldViewModel
{
    internal ObjectFieldViewModel(DefinitionNode schema, string name, bool required,
        FieldViewModel? parent, bool listItem, bool inheritedMasking = false)
        : base(schema, name, required, parent, listItem, inheritedMasking)
    {
        Children = Array.AsReadOnly(schema.Properties
            .Select(property => FormFieldFactory.Project(property.Value, property.Key,
                schema.Required.Contains(property.Key, StringComparer.Ordinal), this, false)).ToArray());
    }
    public IReadOnlyList<FieldViewModel> Children { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Children;

}
