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
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
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
                if (members.ContainsKey("defaultExperience"))
                {
                    String(members["defaultExperience"]!, 64);
                    Array(members["sections"]!, 64, minimum: 1);
                    Array(members["experiences"]!, 16, minimum: 1);
                }
                if (members.ContainsKey("description") && members.ContainsKey("id") && members.ContainsKey("title"))
                {
                    String(members["id"]!, 64);
                    members["id"]!["pattern"] = "^[a-z](?:[a-z0-9]|-(?=[a-z0-9]))*$";
                    String(members["title"]!, 128);
                    String(members["description"]!, 2048);
                    if (members.ContainsKey("fields"))
                    {
                        Enum(members["disclosure"]!, DefinitionVocabulary.Disclosures);
                        members["disclosure"]!["default"] = "guided";
                        Array(members["fields"]!, 1024, minimum: 1, unique: true);
                    }
                    else
                    {
                        Enum(members["initialDisclosure"]!, DefinitionVocabulary.Disclosures);
                        members["initialDisclosure"]!["default"] = "guided";
                        Enum(members["navigation"]!, "guided", "sections");
                        members["navigation"]!["default"] = "sections";
                        Array(members["sections"]!, 64, minimum: 1, unique: true);
                    }
                }
                if (members.ContainsKey("instructions"))
                {
                    String(members["instructions"]!, 4096);
                    Array(members["links"]!, 4);
                    Array(members["searchTerms"]!, 16, unique: true);
                    String(members["searchTerms"]!["items"]!, 64);
                }
                if (members.ContainsKey("url") && members.ContainsKey("title"))
                {
                    String(members["title"]!, 128);
                    String(members["url"]!, 2048);
                    members["url"]!["format"] = "uri";
                    members["url"]!["pattern"] = "^https://[^/?#@\\s]+(?:/[^?#\\s]*)?$";
                }
                if (members.ContainsKey("kind") && members.ContainsKey("acquisition"))
                {
                    Enum(members["kind"]!, "password", "opaque-token", "symmetric-key", "private-key", "connection-string");
                    Enum(members["acquisition"]!, "existing", "create", "issuer");
                    map["allOf"] = JsonNode.Parse("""
                        [{"if":{"properties":{"acquisition":{"const":"create"}}},
                          "then":{"properties":{"kind":{"enum":["password","symmetric-key"]}}}},
                         {"if":{"properties":{"acquisition":{"const":"issuer"}}},
                          "then":{"properties":{"kind":{"enum":["password","opaque-token","private-key","connection-string"]}}}}]
                        """);
                }
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

    private static void String(JsonNode schema, int maximum)
    {
        schema["minLength"] = 1;
        schema["maxLength"] = maximum;
    }

    private static void Array(JsonNode schema, int maximum, int minimum = 0, bool unique = false)
    {
        schema["minItems"] = minimum;
        schema["maxItems"] = maximum;
        if (unique) schema["uniqueItems"] = true;
    }
}
