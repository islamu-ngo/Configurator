namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;

public sealed class ObjectFieldViewModel : FieldViewModel
{
    internal ObjectFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem)
    {
        Children = Array.AsReadOnly(schema.GetProperty("properties").EnumerateArray()
            .Select(property => FormFieldFactory.Project(property, this, false)).ToArray());
    }
    public IReadOnlyList<FieldViewModel> Children { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Children;

}
