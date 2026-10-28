namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions.Authoring;

public static class DefinitionSchema
{
    /// <summary>Exports the current closed structural profile; runtime admission additionally checks semantics and budgets.</summary>
    public static string Generate() => Render(Create(source: false));

    /// <summary>Exports the private answer envelope independently of public definition versions.</summary>
    public static string GenerateAnswers()
    {
        JsonObject schema = Closed(new JsonObject
        {
            ["answerContractVersion"] = new JsonObject { ["const"] = 1 },
            ["values"] = new JsonObject { ["type"] = "object" },
            ["omitted"] = new JsonObject
            {
                ["type"] = "array", ["uniqueItems"] = true,
                ["items"] = new JsonObject { ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$" }
            }
        }, "answerContractVersion", "values", "omitted");
        Header(schema, "urn:islamu:configurator:answers:v1", "Configurator private answers v1");
        return Render(schema);
    }

    internal static JsonObject Create(bool source)
    {
        var project = Closed(new JsonObject
        {
            ["id"] = Text(256), ["name"] = Text(256), ["publisher"] = Text(256),
            ["description"] = Text(4096), ["repository"] = Text(2048), ["definitionLicense"] = Text(256)
        }, source ? ["id", "name"] : ["id", "name", "publisher"]);
        JsonObject artifact = Closed(new JsonObject
        {
            ["id"] = Text(128), ["fileName"] = Text(128), ["format"] = Choice(DefinitionVocabulary.Formats),
            ["targetUpdateMode"] = Choice(DefinitionVocabulary.TargetUpdateModes),
            ["restartRequirement"] = Choice(DefinitionVocabulary.RestartRequirements), ["projection"] = Ref("projection")
        }, "id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection");
        JsonObject properties = new()
        {
            ["project"] = project, ["applicationVersions"] = Strings(128, 128), ["entryPoints"] = Strings(128, 128),
            ["configuration"] = Ref("node"), ["presentation"] = Presentation(), ["localization"] = Localization(),
            ["annotations"] = new JsonObject { ["type"] = "object" }
        };
        JsonObject schema;
        if (source)
        {
            properties["$schema"] = new JsonObject { ["const"] = DefinitionAuthoringCodec.SchemaId };
            properties["identity"] = Closed(new JsonObject
            {
                ["familyId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                ["revision"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }
            }, "familyId", "revision");
            JsonObject output = artifact.DeepClone().AsObject();
            output["properties"]!.AsObject().Remove("fileName");
            output["properties"]!["bindings"] = new JsonObject
            {
                ["type"] = "object", ["minProperties"] = 1, ["maxProperties"] = 1024,
                ["additionalProperties"] = Ref("reference")
            };
            output["required"] = new JsonArray("format");
            output["not"] = new JsonObject { ["required"] = new JsonArray("bindings", "projection") };
            properties["outputs"] = new JsonObject
            {
                ["type"] = "object", ["minProperties"] = 1, ["maxProperties"] = 64, ["additionalProperties"] = output
            };
            schema = Closed(properties, "$schema", "identity", "project", "applicationVersions", "configuration", "outputs");
            Header(schema, DefinitionAuthoringCodec.SchemaId, "Configurator authoring v2");
        }
        else
        {
            properties["schemaVersion"] = new JsonObject { ["const"] = DefinitionProfile.RuntimeSchemaVersion };
            properties["profile"] = new JsonObject { ["const"] = DefinitionProfile.Id };
            properties["familyId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" };
            properties["revision"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 };
            properties["locales"] = Strings(128, 128);
            properties["requiredCapabilities"] = new JsonObject
            {
                ["type"] = "array", ["uniqueItems"] = true, ["items"] = Choice(DefinitionProfile.Capabilities.ToArray())
            };
            properties["artifacts"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 64, ["items"] = artifact };
            schema = Closed(properties, "schemaVersion", "profile", "familyId", "revision", "project", "applicationVersions",
                "entryPoints", "locales", "configuration", "artifacts", "requiredCapabilities");
            Header(schema, "urn:islamu:configurator:setup-definition:v2", "Configurator SetupDefinition v2");
        }
        schema["$defs"] = new JsonObject
        {
            ["node"] = Node(),
            ["reference"] = Closed(new JsonObject { ["scope"] = Choice("root", "item"), ["path"] = new JsonObject
            {
                ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$"
            } }, "scope", "path"),
            ["projection"] = Projection(),
            ["predicate"] = Predicate()
        };
        schema["allOf"] = JsonNode.Parse("""
            [{"properties":{"configuration":{"properties":{"type":{"const":"object"}},
              "anyOf":[{"required":["additionalProperties"]},{"required":["oneOf"]},{"required":["$ref"]}]}}}]
            """);
        schema["description"] = "Bounded recursive structure. Admission checks exact numbers, Unicode, acyclic local references, discriminators, privacy, computed capabilities and resource limits; unsupported semantics reject.";
        return schema;
    }

    private static JsonObject Node()
    {
        JsonObject properties = new()
        {
            ["type"] = new JsonObject { ["oneOf"] = new JsonArray(Choice(DefinitionVocabulary.FieldTypes), new JsonObject
            {
                ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 2, ["uniqueItems"] = true,
                ["items"] = Choice(DefinitionVocabulary.FieldTypes.Concat(["null"]).ToArray()),
                ["contains"] = new JsonObject { ["const"] = "null" }
            }) },
            ["title"] = Text(256), ["description"] = Text(4096), ["format"] = Text(256),
            ["properties"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = Ref("node") },
            ["$defs"] = new JsonObject { ["type"] = "object", ["maxProperties"] = 1024, ["additionalProperties"] = Ref("node") },
            ["$ref"] = new JsonObject { ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^#/" },
            ["oneOf"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 16, ["items"] = Ref("node") },
            ["if"] = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false, ["minProperties"] = 1,
                ["properties"] = new JsonObject
                {
                    ["required"] = Strings(1024, 65536),
                    ["properties"] = new JsonObject
                    {
                        ["type"] = "object", ["maxProperties"] = 1024,
                        ["additionalProperties"] = new JsonObject
                        {
                            ["oneOf"] = new JsonArray(
                                Closed(new JsonObject { ["const"] = new JsonObject() }, "const"),
                                Closed(new JsonObject { ["enum"] = new JsonObject
                                {
                                    ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 256,
                                    ["items"] = new JsonObject()
                                } }, "enum"))
                        }
                    }
                }
            },
            ["then"] = RequiredAssertion(), ["else"] = RequiredAssertion(),
            ["dependentRequired"] = new JsonObject
            {
                ["type"] = "object", ["maxProperties"] = 1024,
                ["additionalProperties"] = Strings(1024, 65536)
            },
            ["required"] = new JsonObject { ["type"] = "array", ["uniqueItems"] = true, ["items"] = new JsonObject { ["type"] = "string" } },
            ["additionalProperties"] = new JsonObject { ["oneOf"] = new JsonArray(
                new JsonObject { ["const"] = false }, Ref("node")) }, ["items"] = Ref("node"),
            ["multipleOf"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
            ["uniqueItems"] = new JsonObject { ["type"] = "boolean" },
            ["enum"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 256, ["uniqueItems"] = true,
                ["items"] = new JsonObject { ["type"] = new JsonArray("string", "boolean", "integer", "number", "null") } },
            ["const"] = new JsonObject { ["type"] = new JsonArray("string", "boolean", "integer", "number", "null") },
            ["default"] = new JsonObject(), ["examples"] = new JsonObject { ["type"] = "array", ["maxItems"] = 256 },
            ["readOnly"] = new JsonObject { ["type"] = "boolean" }, ["writeOnly"] = new JsonObject { ["type"] = "boolean" },
            ["deprecated"] = new JsonObject { ["type"] = "boolean" }, ["x-configurator"] = Extension()
        };
        foreach (string name in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum" })
            properties[name] = new JsonObject { ["type"] = "number" };
        foreach (string name in new[] { "minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties" })
            properties[name] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = int.MaxValue };
        JsonObject node = Closed(properties);
        node["oneOf"] = JsonNode.Parse("""
            [{"required":["type"],"not":{"required":["$ref"]}},
             {"required":["$ref"],"not":{"required":["type"]}}]
            """);
        node["allOf"] = JsonNode.Parse("""
            [
              {
                "if":{"required":["type"],"properties":{"type":{"anyOf":[{"const":"object"},{"type":"array","contains":{"const":"object"}}]}}},
                "then":{"anyOf":[{"required":["additionalProperties"]},{"required":["oneOf"]}]},
                "else":{"not":{"anyOf":[{"required":["properties"]},{"required":["required"]},{"required":["additionalProperties"]}]}}
              },
              {
                "if":{"required":["type"],"properties":{"type":{"anyOf":[{"const":"array"},{"type":"array","contains":{"const":"array"}}]}}},
                "then":{"required":["items"]},
                "else":{"not":{"required":["items"]}}
              }
            ]
            """);
        return node;
    }

    private static JsonObject RequiredAssertion() => Closed(new JsonObject
    {
        ["required"] = Strings(1024, 65536)
    }, "required");

    private static JsonObject Extension() => Closed(new JsonObject
    {
        ["sensitivity"] = Choice(DefinitionVocabulary.Sensitivities), ["initialValue"] = new JsonObject(),
        ["suggestedValue"] = new JsonObject(), ["notBlank"] = new JsonObject { ["type"] = "boolean" },
        ["detail"] = Choice("basic", "standard", "advanced"), ["units"] = Text(128),
        ["uniqueBy"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 65536 },
        ["applicableWhen"] = Ref("predicate"),
        ["assertFormat"] = new JsonObject { ["type"] = "boolean" },
        ["credential"] = Closed(new JsonObject
        {
            ["kind"] = Choice("password", "opaque-token", "symmetric-key", "private-key", "connection-string"),
            ["acquisition"] = Choice("existing", "create", "issuer")
        }, "kind", "acquisition"),
        ["guidance"] = Closed(new JsonObject
        {
            ["instructions"] = Text(4096),
            ["links"] = new JsonObject { ["type"] = "array", ["maxItems"] = 4, ["items"] = Closed(new JsonObject
            {
                ["title"] = Text(128), ["url"] = Text(2048)
            }, "title", "url") },
            ["searchTerms"] = Strings(16, 64, minimum: 0)
        }),
        ["choices"] = new JsonObject
        {
            ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 256,
            ["items"] = Closed(new JsonObject { ["value"] = new JsonObject(), ["label"] = Text(256), ["explanation"] = Text(2048) }, "value", "label")
        }
    });

    private static JsonObject Projection()
    {
        var alternatives = new JsonArray();
        foreach (string op in DefinitionVocabulary.ProjectionOperations)
        {
            var properties = new JsonObject { ["op"] = new JsonObject { ["const"] = op } };
            var required = new List<string> { "op" };
            if (op == "literal") { properties["value"] = new JsonObject(); required.Add("value"); }
            if (op is "copy" or "repeat") { properties["reference"] = Ref("reference"); required.Add("reference"); }
            if (op == "repeat") { properties["item"] = Ref("projection"); required.Add("item"); }
            if (op == "object")
            {
                properties["members"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = Ref("projection") };
                required.Add("members");
            }
            if (op == "list")
            {
                properties["items"] = new JsonObject { ["type"] = "array", ["items"] = Ref("projection") };
                required.Add("items");
            }
            properties["when"] = Ref("predicate");
            alternatives.Add(Closed(properties, required.ToArray()));
        }
        return new JsonObject { ["oneOf"] = alternatives };
    }

    private static JsonObject Predicate()
    {
        var alternatives = new JsonArray();
        foreach (string operation in new[] { "exists", "equals", "notEquals", "in", "all", "any", "not" })
        {
            var properties = new JsonObject { ["op"] = new JsonObject { ["const"] = operation } };
            var required = new List<string> { "op" };
            if (operation is "all" or "any" or "not")
            {
                properties["operands"] = new JsonObject
                {
                    ["type"] = "array", ["minItems"] = 1,
                    ["maxItems"] = operation == "not" ? 1 : 4096, ["items"] = Ref("predicate")
                };
                required.Add("operands");
            }
            else
            {
                properties["reference"] = Ref("reference");
                required.Add("reference");
                if (operation == "in")
                {
                    properties["values"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 4096 };
                    required.Add("values");
                }
                else if (operation != "exists")
                {
                    properties["value"] = new JsonObject();
                    required.Add("value");
                }
            }
            alternatives.Add(Closed(properties, required.ToArray()));
        }
        return new JsonObject { ["oneOf"] = alternatives };
    }

    private static JsonObject Presentation() => Closed(new JsonObject
    {
        ["defaultJourney"] = Text(64), ["navigation"] = Choice("wizard", "sections"),
        ["sections"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 64,
            ["items"] = Closed(new JsonObject
            {
                ["id"] = Text(64), ["title"] = Text(128), ["description"] = Text(2048),
                ["pointers"] = new JsonObject { ["type"] = "array", ["uniqueItems"] = true, ["items"] = new JsonObject { ["type"] = "string" } }
            }, "id", "title", "pointers") },
        ["journeys"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 16,
            ["items"] = Closed(new JsonObject
            {
                ["id"] = Text(64), ["title"] = Text(128), ["description"] = Text(2048), ["sections"] = Strings(64, 64)
            }, "id", "title", "sections") }
    }, "sections", "journeys", "defaultJourney");

    private static JsonObject Localization() => Closed(new JsonObject
    {
        ["defaultLocale"] = Text(128),
        ["translations"] = new JsonObject { ["type"] = "object", ["maxProperties"] = 128,
            ["additionalProperties"] = Closed(new JsonObject
            {
                ["direction"] = Choice("ltr", "rtl"),
                ["overrides"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = Text(4096) }
            }) }
    }, "defaultLocale", "translations");

    private static void Header(JsonObject schema, string id, string title)
    {
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$id"] = id;
        schema["title"] = title;
    }
    private static JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
    private static JsonObject Text(int maximum) => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = maximum };
    private static JsonObject Choice(params string[] values) => new()
    {
        ["type"] = "string", ["enum"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };
    private static JsonObject Strings(int maximum, int length, int minimum = 1) => new()
    {
        ["type"] = "array", ["minItems"] = minimum, ["maxItems"] = maximum, ["uniqueItems"] = true, ["items"] = Text(length)
    };
    private static JsonObject Closed(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false,
        ["required"] = new JsonArray(required.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };
    internal static string Render(JsonObject schema) => schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
}
