namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Presentation;

public sealed class ObjectFieldViewModel : FieldViewModel
{
    internal ObjectFieldViewModel(JsonElement schema, FieldViewModel? parent, bool listItem) : base(schema, parent, listItem)
    {
        Children = Array.AsReadOnly(schema.GetProperty("properties").EnumerateArray()
            .Select(property => FormFieldFactory.Project(property, this, false)).ToArray());
    }
    public IReadOnlyList<FieldViewModel> Children { get; }
    internal override IEnumerable<FieldViewModel> Descendants => Children;

    internal override void Load(JsonElement? value)
    {
        JsonElement initial = value ?? DefinitionCollectionItem.CreateObject(Schema);
        foreach (var child in Children)
            child.Load(initial.TryGetProperty(child.Id, out var supplied) ? supplied : null);
        SetStructuredText(value);
    }

    internal override bool TryValue(out JsonElement? value)
    {
        value = null;
        if (IsStructuredInvalid) return false;
        if (!IsProvided) return true;
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var child in Children)
        {
            if (!child.TryValue(out var supplied)) return false;
            if (supplied is { } typed) properties.Add(child.Id, typed);
        }
        value = JsonSerializer.SerializeToElement(properties);
        return true;
    }
}
