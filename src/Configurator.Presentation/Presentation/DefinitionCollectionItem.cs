namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Creates declared object containers and defaults without inventing required scalar answers.</summary>
public static class DefinitionCollectionItem
{
    public static JsonElement CreateObject(JsonElement metadata) =>
        JsonSerializer.SerializeToElement(CreateProperties(metadata));

    private static JsonObject CreateProperties(JsonElement metadata)
    {
        JsonObject result = [];
        if (!metadata.TryGetProperty("properties", out JsonElement properties)) return result;
        foreach (JsonElement property in properties.EnumerateArray())
        {
            string id = property.GetProperty("id").GetString()!;
            if (property.TryGetProperty("default", out JsonElement value))
                result[id] = JsonNode.Parse(value.GetRawText());
            else if (property.TryGetProperty("required", out JsonElement required) && required.GetBoolean())
            {
                if (property.GetProperty("type").GetString() == "object") result[id] = CreateProperties(property);
                else if (property.GetProperty("type").GetString() == "list") result[id] = new JsonArray();
            }
        }
        return result;
    }
}
