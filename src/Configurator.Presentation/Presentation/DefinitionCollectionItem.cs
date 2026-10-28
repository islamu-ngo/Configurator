namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Definitions;

/// <summary>Creates an explicitly supplied empty container; Core alone resolves child initialization.</summary>
public static class DefinitionCollectionItem
{
    public static JsonElement CreateObject(DefinitionNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Type != DefinitionNodeType.Mapping) throw new ArgumentException("field-invalid", nameof(schema));
        return JsonSerializer.SerializeToElement(new { });
    }
}
