namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

public static class DefinitionSchema
{
    /// <summary>Exports the closed wire structure. Admission also enforces semantic and resource invariants.</summary>
    public static string Generate()
    {
        JsonNode schema = SetupDefinitionCodec.Options.GetJsonSchemaAsNode(typeof(DefinitionDocument),
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$id"] = "urn:islamu:configurator:setup-definition:v1";
        schema["title"] = "Configurator SetupDefinition v1";
        schema["description"] = "Closed wire structure; Core admission additionally checks vocabulary, bounds, references, cycles and sensitive defaults.";
        JsonNode properties = schema["properties"]!;
        properties["schemaVersion"]!["const"] = 1;
        properties["familyId"]!["format"] = "uuid";
        properties["revision"]!["minimum"] = 1;
        properties["fields"]!["maxItems"] = 1024;
        properties["artifacts"]!["minItems"] = 1;
        properties["artifacts"]!["maxItems"] = 64;
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
}
