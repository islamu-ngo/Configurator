namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;
using System.Text.Json.Nodes;

public static class DefinitionAuthoringSchema
{
    /// <summary>Exports source structure; compiler/runtime admission additionally enforce references, privacy and bounds.</summary>
    public static string Generate()
    {
        JsonObject runtime = JsonNode.Parse(DefinitionSchema.Generate())!.AsObject();
        runtime.Remove("$id");
        RebaseReferences(runtime);
        JsonObject field = runtime["properties"]!["fields"]!["items"]!.DeepClone().AsObject();
        field["properties"]!.AsObject().Remove("id");
        field["required"] = new JsonArray("type");
        field["properties"]!["properties"] = new JsonObject
        {
            ["type"] = "object",
            ["maxProperties"] = 1024,
            ["additionalProperties"] = new JsonObject { ["$ref"] = "#/$defs/field" }
        };
        JsonObject output = runtime["properties"]!["artifacts"]!["items"]!.DeepClone().AsObject();
        output["properties"]!.AsObject().Remove("fileName");
        output["properties"]!["bindings"] = new JsonObject
        {
            ["type"] = "object",
            ["maxProperties"] = 1024,
            ["additionalProperties"] = new JsonObject { ["type"] = "string" }
        };
        output["required"] = new JsonArray("format");
        var projectProperties = new JsonObject();
        foreach (string name in new[] { "id", "name", "publisher", "description", "repository", "definitionLicense" })
            projectProperties[name] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = name == "description" ? 4096 : 256 };
        var properties = new JsonObject
        {
            ["$schema"] = new JsonObject { ["const"] = DefinitionAuthoringCodec.SchemaId },
            ["identity"] = Closed(new JsonObject
            {
                ["familyId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                ["revision"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }
            }, "familyId", "revision"),
            ["project"] = Closed(projectProperties, "id", "name"),
            ["applicationVersions"] = Strings(),
            ["entryPoints"] = Strings(),
            ["locales"] = Strings(),
            ["fields"] = new JsonObject { ["type"] = "object", ["maxProperties"] = 1024, ["additionalProperties"] = new JsonObject { ["$ref"] = "#/$defs/field" } },
            ["outputs"] = new JsonObject { ["type"] = "object", ["minProperties"] = 1, ["maxProperties"] = 64, ["additionalProperties"] = output }
        };
        JsonObject schema = Closed(properties, "$schema", "identity", "project", "applicationVersions", "fields", "outputs");
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$id"] = DefinitionAuthoringCodec.SchemaId;
        schema["title"] = "Configurator maintainer authoring v1";
        schema["description"] = "Closed bounded authoring contract; compiler admission enforces exact numbers, sensitivity, identifiers, references, cycles and output shape.";
        schema["$defs"] = new JsonObject { ["field"] = field, ["runtime"] = runtime };
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static JsonObject Strings() => new()
    {
        ["type"] = "array",
        ["minItems"] = 1,
        ["maxItems"] = 128,
        ["uniqueItems"] = true,
        ["items"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 128 }
    };

    private static JsonObject Closed(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["additionalProperties"] = false,
        ["required"] = new JsonArray(required.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray())
    };

    private static void RebaseReferences(JsonNode node)
    {
        if (node is JsonObject map)
        {
            if (map["$ref"] is JsonNode reference && reference.GetValue<string>().StartsWith("#/", StringComparison.Ordinal))
                map["$ref"] = "#/$defs/runtime/" + reference.GetValue<string>()[2..];
            foreach ((_, JsonNode? child) in map)
                if (child is not null) RebaseReferences(child);
        }
        else if (node is JsonArray array)
            foreach (JsonNode? child in array)
                if (child is not null) RebaseReferences(child);
    }
}
