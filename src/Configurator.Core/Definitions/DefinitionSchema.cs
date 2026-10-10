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
        AddVocabulary(schema);
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static void AddVocabulary(JsonNode node)
    {
        if (node is JsonObject map)
        {
            if (map["properties"] is JsonObject members)
            {
                if (members.ContainsKey("sensitivity"))
                {
                    Enum(members["type"]!, DefinitionVocabulary.FieldTypes);
                    Enum(members["sensitivity"]!, DefinitionVocabulary.Sensitivities);
                    Enum(members["disclosure"]!, DefinitionVocabulary.Disclosures);
                    Enum(members["validator"]!, DefinitionVocabulary.Validators);
                    Enum(members["itemType"]!, DefinitionVocabulary.ItemTypes);
                }
                if (members.ContainsKey("applySemantics"))
                {
                    Enum(members["format"]!, DefinitionVocabulary.Formats);
                    Enum(members["applySemantics"]!, DefinitionVocabulary.ApplySemantics);
                    Enum(members["restart"]!, DefinitionVocabulary.Restarts);
                }
                if (members.ContainsKey("op"))
                    Enum(members["op"]!, members.ContainsKey("operands")
                        ? DefinitionVocabulary.ConditionOperations : DefinitionVocabulary.ProjectionOperations);
            }
            foreach ((_, JsonNode? child) in map)
                if (child is not null) AddVocabulary(child);
        }
        else if (node is JsonArray array)
            foreach (JsonNode? child in array)
                if (child is not null) AddVocabulary(child);
    }

    private static void Enum(JsonNode schema, params string[] values) =>
        schema["enum"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
