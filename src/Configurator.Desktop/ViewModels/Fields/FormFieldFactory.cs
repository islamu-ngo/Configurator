namespace Configurator.Desktop.ViewModels.Fields;

using System.Text.Json;
using Configurator.Presentation;

/// <summary>Projects admitted fields; the caller owns and disposes the returned roots, not the shared form.</summary>
public sealed class FormFieldFactory
{
    public IReadOnlyList<FieldViewModel> Create(DefinitionFormModel form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return Array.AsReadOnly(form.Fields.Select(input =>
        {
            var field = Project(input.Definition.Constraints, null, false);
            field.Attach(form, input);
            return field;
        }).ToArray());
    }

    internal static FieldViewModel Project(JsonElement schema, FieldViewModel? parent, bool listItem) =>
        schema.GetProperty("type").GetString() switch
        {
            "string" => new StringFieldViewModel(schema, parent, listItem),
            "enum" => new EnumFieldViewModel(schema, parent, listItem),
            "integer" or "number" => new NumericFieldViewModel(schema, parent, listItem),
            "boolean" => new BooleanFieldViewModel(schema, parent, listItem),
            "object" => new ObjectFieldViewModel(schema, parent, listItem),
            "list" => new ListFieldViewModel(schema, parent, listItem),
            _ => throw new InvalidOperationException("Unknown admitted field type.")
        };
}
