namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions.Authoring;

public static class DefinitionSchema
{
    /// <summary>Exports the current closed structural profile; runtime admission additionally checks semantics and budgets.</summary>
    public static string Generate() => Render(Create(source: false));

    /// <summary>Exports the current private answer envelope.</summary>
    public static string GenerateAnswers()
    {
        JsonObject schema = Closed(new JsonObject
        {
            ["answerContractVersion"] = Describe(new JsonObject { ["const"] = AnswerSnapshotCodec.ContractVersion },
                "Private answer envelope contract version."),
            ["values"] = Describe(new JsonObject { ["type"] = "object" },
                "Answers keyed by root configuration field. Use `definition answer-schema` for a definition-specific schema."),
            ["omitted"] = Describe(new JsonObject
            {
                ["type"] = "array",
                ["uniqueItems"] = true,
                ["default"] = new JsonArray(),
                ["items"] = new JsonObject { ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$" }
            }, "JSON Pointers deliberately left unset even when an initial value exists. Optional; defaults to none.")
        }, "answerContractVersion", "values");
        Header(schema, "urn:islamu:configurator:answers:v" + ConfiguratorVersion.Current,
            "Configurator private answers " + ConfiguratorVersion.Current);
        return Render(schema);
    }

    internal static JsonObject Create(bool source)
    {
        var project = Closed(new JsonObject
        {
            ["id"] = Text(256),
            ["name"] = Text(256),
            ["publisher"] = Text(256),
            ["description"] = Text(4096),
            ["repository"] = Text(2048),
            ["definitionLicense"] = Text(256)
        }, source ? ["id", "name"] : ["id", "name", "publisher"]);
        JsonObject artifact = Closed(new JsonObject
        {
            ["id"] = Text(128),
            ["fileName"] = Text(128),
            ["format"] = Choice(DefinitionVocabulary.Formats),
            ["targetUpdateMode"] = Choice(DefinitionVocabulary.TargetUpdateModes),
            ["restartRequirement"] = Choice(DefinitionVocabulary.RestartRequirements),
            ["title"] = Text(256),
            ["description"] = Text(4096),
            ["consumerIds"] = Strings(64, 128),
            ["surface"] = Choice("process-environment", "compose-input"),
            ["environmentDialect"] = Choice("docker-run", "compose"),
            ["projection"] = Ref("projection")
        }, "id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection");
        JsonObject properties = new()
        {
            ["project"] = Describe(project, "Display identity of the configured application. Labels are not authentication."),
            ["applicationVersions"] = Describe(Strings(128, 128), "Target application versions this definition was reviewed against."),
            ["entryPoints"] = Describe(Strings(128, 128), "Named launch entry points; defaults to `setup` in authoring sources."),
            ["configuration"] = Describe(Ref("node"), "Root object schema of every answer the definition collects."),
            ["presentation"] = Describe(Presentation(), "Sections, collapsible groups and journeys. Layout never changes validation."),
            ["deployment"] = Describe(Deployment(), "Recipe and scope selectors choosing which components and outputs apply."),
            ["localization"] = Describe(Localization(), "Translations of display text addressed by JSON Pointer."),
            ["annotations"] = Describe(new JsonObject { ["type"] = "object" }, "Free-form publisher notes; never interpreted.")
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
            output["properties"]!["bindings"] = Describe(new JsonObject
            {
                ["type"] = "object",
                ["minProperties"] = 1,
                ["maxProperties"] = 1024,
                ["additionalProperties"] = new JsonObject
                {
                    ["oneOf"] = new JsonArray(
                        Describe(Pointer(), "Shorthand for a root-scoped copy of this configuration path."),
                        Describe(Closed(new JsonObject { ["literal"] = new JsonObject() }, "literal"), "A constant output value."),
                        Ref("reference"))
                }
            }, "Output targets (dotenv keys or JSON Pointers) mapped to configuration paths or literals.");
            output["properties"]!["includeBindings"] = Describe(Strings(64, 128),
                "Output file names whose bindings are reused first; explicit bindings override them.");
            output["properties"]!["bindObject"] = Describe(new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 64,
                ["items"] = new JsonObject
                {
                    ["oneOf"] = new JsonArray(Pointer(), Closed(new JsonObject
                    {
                        ["source"] = Pointer(),
                        ["target"] = Pointer(),
                        ["only"] = Strings(1024, 4096),
                        ["except"] = Strings(1024, 4096)
                    }, "source"))
                }
            }, "Binds every member of a configuration object by its key, optionally filtered and nested under a target.");
            output["properties"]!["when"] = Describe(Ref("predicate"), "The output is prepared only while this predicate holds.");
            output["required"] = new JsonArray("format");
            output["not"] = JsonNode.Parse("""
                {"required":["projection"],"anyOf":[{"required":["bindings"]},{"required":["includeBindings"]},{"required":["bindObject"]}]}
                """);
            properties["outputs"] = Describe(new JsonObject
            {
                ["type"] = "object",
                ["minProperties"] = 1,
                ["maxProperties"] = 64,
                ["additionalProperties"] = output
            }, "Prepared files keyed by file name.");
            properties["predicates"] = Describe(new JsonObject
            {
                ["type"] = "object",
                ["maxProperties"] = 256,
                ["propertyNames"] = new JsonObject { ["minLength"] = 1, ["maxLength"] = 64 },
                ["additionalProperties"] = Ref("predicate")
            }, "Reusable named conditions, referenced as {\"op\":\"predicate\",\"name\":...} and inlined at compile time.");
            schema = Closed(properties, "$schema", "identity", "project", "applicationVersions", "configuration", "outputs");
            Header(schema, DefinitionAuthoringCodec.SchemaId, "Configurator authoring " + ConfiguratorVersion.Current);
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
                ["type"] = "array",
                ["uniqueItems"] = true,
                ["items"] = Choice(DefinitionProfile.Capabilities.ToArray())
            };
            properties["artifacts"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 64, ["items"] = artifact };
            schema = Closed(properties, "schemaVersion", "profile", "familyId", "revision", "project", "applicationVersions",
                "entryPoints", "locales", "configuration", "artifacts", "requiredCapabilities");
            Header(schema, "urn:islamu:configurator:setup-definition:v" + ConfiguratorVersion.Current,
                "Configurator SetupDefinition " + ConfiguratorVersion.Current);
        }
        schema["$defs"] = new JsonObject
        {
            ["node"] = Node(source),
            ["reference"] = Closed(new JsonObject
            {
                ["scope"] = Choice("root", "item"),
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["maxLength"] = 65536,
                    ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$"
                }
            }, "scope", "path"),
            ["projection"] = Projection(),
            ["predicate"] = Predicate(source)
        };
        schema["allOf"] = JsonNode.Parse("""
            [{"properties":{"configuration":{"properties":{"type":{"const":"object"}},
              "anyOf":[{"required":["additionalProperties"]},{"required":["oneOf"]},{"required":["$ref"]}]}}}]
            """);
        schema["description"] = "Bounded recursive structure. Admission checks exact numbers, Unicode, acyclic local references, discriminators, privacy, computed capabilities and resource limits; unsupported semantics reject.";
        return schema;
    }

    private static JsonObject Deployment()
    {
        JsonObject scope = Closed(new JsonObject
        {
            ["id"] = Text(128),
            ["title"] = Text(256),
            ["description"] = Text(4096),
            ["componentIds"] = Strings(64, 128),
            ["artifactIds"] = Strings(64, 128),
            ["primaryArtifactId"] = Text(128),
            ["prerequisitePointers"] = Strings(1024, 65536, minimum: 0)
        }, "id", "title", "componentIds", "artifactIds");
        JsonObject recipe = Closed(new JsonObject
        {
            ["id"] = Text(128),
            ["title"] = Text(256),
            ["description"] = Text(4096),
            ["scopes"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 16, ["items"] = scope }
        }, "id", "title", "scopes");
        JsonObject component = Closed(new JsonObject
        {
            ["id"] = Text(128),
            ["title"] = Text(256),
            ["description"] = Text(4096)
        }, "id", "title");
        return Closed(new JsonObject
        {
            ["recipePointer"] = Text(65536),
            ["scopePointer"] = Text(65536),
            ["when"] = Ref("predicate"),
            ["components"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 64, ["items"] = component },
            ["recipes"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 16, ["items"] = recipe }
        }, "recipePointer", "scopePointer", "components", "recipes");
    }

    private static JsonObject Node(bool source)
    {
        JsonObject properties = new()
        {
            ["type"] = new JsonObject
            {
                ["oneOf"] = new JsonArray(Choice(DefinitionVocabulary.FieldTypes), new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = 2,
                    ["maxItems"] = 2,
                    ["uniqueItems"] = true,
                    ["items"] = Choice(DefinitionVocabulary.FieldTypes.Concat(["null"]).ToArray()),
                    ["contains"] = new JsonObject { ["const"] = "null" }
                })
            },
            ["title"] = Describe(Text(256), "Display title. When omitted, the key is humanized (`PUBLIC_BASE_URL` shows as `Public Base URL`)."),
            ["description"] = Describe(Text(4096), "Help text shown with the field."),
            ["format"] = source
                ? Describe(Choice(DefinitionFormatValidator.Formats.ToArray()), "Asserted lexical format. Every listed format is validated automatically in linear time.")
                : Describe(Text(256), "Lexical format. Asserted formats are validated automatically; other tokens are annotations."),
            ["properties"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = Ref("node") },
            ["$defs"] = new JsonObject { ["type"] = "object", ["maxProperties"] = 1024, ["additionalProperties"] = Ref("node") },
            ["$ref"] = new JsonObject { ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^#/" },
            ["oneOf"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 16, ["items"] = Ref("node") },
            ["if"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["minProperties"] = 1,
                ["properties"] = new JsonObject
                {
                    ["required"] = Strings(1024, 65536),
                    ["properties"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["maxProperties"] = 1024,
                        ["additionalProperties"] = new JsonObject
                        {
                            ["oneOf"] = new JsonArray(
                                Closed(new JsonObject { ["const"] = new JsonObject() }, "const"),
                                Closed(new JsonObject
                                {
                                    ["enum"] = new JsonObject
                                    {
                                        ["type"] = "array",
                                        ["minItems"] = 1,
                                        ["maxItems"] = 256,
                                        ["items"] = new JsonObject()
                                    }
                                }, "enum"))
                        }
                    }
                }
            },
            ["then"] = RequiredAssertion(),
            ["else"] = RequiredAssertion(),
            ["dependentRequired"] = new JsonObject
            {
                ["type"] = "object",
                ["maxProperties"] = 1024,
                ["additionalProperties"] = Strings(1024, 65536)
            },
            ["required"] = new JsonObject { ["type"] = "array", ["uniqueItems"] = true, ["items"] = new JsonObject { ["type"] = "string" } },
            ["additionalProperties"] = new JsonObject
            {
                ["oneOf"] = new JsonArray(
                new JsonObject { ["const"] = false }, Ref("node"))
            },
            ["items"] = Ref("node"),
            ["multipleOf"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
            ["uniqueItems"] = new JsonObject { ["type"] = "boolean" },
            ["enum"] = new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 256,
                ["uniqueItems"] = true,
                ["items"] = new JsonObject { ["type"] = new JsonArray("string", "boolean", "integer", "number", "null") }
            },
            ["const"] = new JsonObject { ["type"] = new JsonArray("string", "boolean", "integer", "number", "null") },
            ["default"] = new JsonObject(),
            ["examples"] = new JsonObject { ["type"] = "array", ["maxItems"] = 256 },
            ["readOnly"] = new JsonObject { ["type"] = "boolean" },
            ["writeOnly"] = new JsonObject { ["type"] = "boolean" },
            ["deprecated"] = new JsonObject { ["type"] = "boolean" },
            ["x-configurator"] = Describe(Extension(source), "Configurator semantics: sensitivity, guidance, choices, ordering, widgets and applicability.")
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

    private static JsonObject Extension(bool source) => Closed(new JsonObject
    {
        ["sensitivity"] = Describe(Choice(DefinitionVocabulary.Sensitivities),
            "Custody floor. Omitted means `sensitive`; `secret` values never appear in public metadata."),
        ["initialValue"] = Describe(new JsonObject(),
            "Public value prepared when the answer is absent. It never supplies a protected member; an object may "
            + "open with absent required members, which become operator obligations."),
        ["suggestedValue"] = Describe(new JsonObject(), "Public value offered to the operator but never applied automatically."),
        ["notBlank"] = Describe(new JsonObject { ["type"] = "boolean" }, "Rejects whitespace-only text."),
        ["detail"] = Describe(Choice("basic", "standard", "advanced"), "Progressive disclosure level."),
        ["units"] = Describe(Text(128), "Unit label shown beside the value, for example `seconds`."),
        ["uniqueBy"] = Describe(new JsonObject
        {
            ["oneOf"] = new JsonArray(Pointer(), new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 8,
                ["uniqueItems"] = true,
                ["items"] = Pointer()
            })
        }, "Item-relative pointer, or pointers forming a composite key, that must be unique across list rows."),
        ["applicableWhen"] = Describe(Ref("predicate"), "The field is collected only while this predicate holds."),
        ["requiredWhen"] = Describe(Ref("predicate"),
            "A collected object member must be supplied while this predicate holds. It may read any answer, "
            + "and the member must not also be listed in `required`."),
        ["propertyOrder"] = Describe(Strings(1025, 65536),
            "Display order of object members; `*` places every unlisted member. Never changes canonical identity."),
        ["order"] = Describe(new JsonObject { ["type"] = "integer", ["minimum"] = -1000000, ["maximum"] = 1000000 },
            "Display rank among sibling members without an explicit `propertyOrder`."),
        ["placeholder"] = Describe(Text(256), "Example input shown in an empty editor. Never a value; public fields only."),
        ["defaultText"] = Describe(Text(1024), "Explains what the target does when the field is omitted. Never a value."),
        ["deprecationMessage"] = Describe(Text(2048), "Guidance for a field marked `deprecated: true`."),
        ["widget"] = Describe(Choice(DefinitionFieldPresentationMetadata.WidgetTokens), "Preferred editor; clients may fall back to `auto`."),
        ["rows"] = Describe(new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 64 },
            "Visible lines for `multiline`, `markdown` and `code` widgets."),
        ["syntax"] = Describe(new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 32 },
            "Highlighting language for the `code` widget, for example `json`."),
        ["booleanLabels"] = Describe(Closed(new JsonObject { ["true"] = Text(128), ["false"] = Text(128) }, "true", "false"),
            "Display labels for the two boolean states."),
        ["itemTitlePointer"] = Describe(Pointer(), "Item-relative pointer of a public scalar that titles each list row."),
        ["displayRawKey"] = Describe(new JsonObject { ["type"] = "boolean" }, "Shows the exact key instead of the humanized title."),
        ["secret"] = Describe(DefinitionSecretSchema.Create(), "Secret kind, acquisition, encoding, constraints and local generation."),
        ["guidance"] = Describe(Closed(new JsonObject
        {
            ["instructions"] = Text(4096),
            ["links"] = new JsonObject
            {
                ["type"] = "array",
                ["maxItems"] = 4,
                ["items"] = Closed(new JsonObject
                {
                    ["title"] = Text(128),
                    ["url"] = Text(2048)
                }, "title", "url")
            },
            ["searchTerms"] = Strings(16, 64, minimum: 0)
        }), "Operator instructions, public HTTPS links and search terms."),
        ["choices"] = Describe(new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = 1,
            ["maxItems"] = 256,
            ["items"] = Closed(new JsonObject
            {
                ["value"] = new JsonObject(),
                ["label"] = Text(256),
                ["explanation"] = Text(2048),
                ["deprecated"] = new JsonObject { ["type"] = "boolean" },
                ["deprecationMessage"] = Text(2048)
            }, "value")
        }, source
            ? "Enum member metadata. Without `enum`, the choices define it in order; unlabeled values are humanized."
            : "Metadata for a subset of `enum` members; unlabeled values are humanized.")
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

    private static JsonObject Predicate(bool source)
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
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["maxItems"] = operation == "not" ? 1 : 4096,
                    ["items"] = Ref("predicate")
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
        if (source)
            alternatives.Add(Describe(Closed(new JsonObject
            {
                ["op"] = new JsonObject { ["const"] = "predicate" },
                ["name"] = Text(64)
            }, "op", "name"), "Reference to a named root predicate, inlined at compile time."));
        return new JsonObject { ["oneOf"] = alternatives };
    }

    private static JsonObject Presentation()
    {
        JsonObject group = Closed(new JsonObject
        {
            ["id"] = Text(64),
            ["title"] = Text(128),
            ["description"] = Text(2048),
            ["pointers"] = Describe(new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 1024,
                ["uniqueItems"] = true,
                ["items"] = Pointer()
            }, "Settings in this group; each lies within one of the section's pointers."),
            ["detail"] = Choice("basic", "standard", "advanced"),
            ["applicableWhen"] = Ref("predicate"),
            ["collapsed"] = Describe(new JsonObject { ["type"] = "boolean" }, "Starts collapsed.")
        }, "id", "title", "pointers");
        return Closed(new JsonObject
        {
            ["defaultJourney"] = Text(64),
            ["navigation"] = Choice("wizard", "sections"),
            ["outputSelector"] = Describe(Closed(new JsonObject
            {
                ["pointer"] = new JsonObject
                {
                    ["type"] = "string",
                    ["maxLength"] = 65536,
                    ["pattern"] = "^(?:/(?:[^/~]|~[01])*)+$"
                },
                ["options"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["maxItems"] = 64,
                    ["items"] = Closed(new JsonObject
                    {
                        ["value"] = new JsonObject { ["type"] = "string", ["maxLength"] = 65536 },
                        ["title"] = Text(128),
                        ["description"] = Text(2048),
                        ["nextActions"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["maxItems"] = 63,
                            ["items"] = Closed(new JsonObject
                            {
                                ["targetOption"] = Text(65536),
                                ["title"] = Text(128),
                                ["description"] = Text(2048),
                                ["proposedWhen"] = Ref("predicate"),
                                ["priority"] = Choice("recommended", "available", "optional")
                            }, "targetOption", "title")
                        },
                        ["artifactIds"] = Strings(64, 128)
                    }, "value", "title", "artifactIds")
                }
            }, "pointer", "options"), "Public required enum whose value picks the prepared output set."),
            ["sections"] = new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 64,
                ["items"] = Closed(new JsonObject
                {
                    ["id"] = Text(64),
                    ["title"] = Text(128),
                    ["description"] = Text(2048),
                    ["pointers"] = Describe(new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 1024,
                        ["uniqueItems"] = true,
                        ["items"] = Pointer()
                    }, "Settings shown by this section; nested pointers such as `/server/domain` are allowed."),
                    ["detail"] = Choice("basic", "standard", "advanced"),
                    ["applicableWhen"] = Ref("predicate"),
                    ["groups"] = Describe(new JsonObject { ["type"] = "array", ["maxItems"] = 32, ["items"] = group },
                        "Collapsible groups organizing the section's settings.")
                }, "id", "title", "pointers")
            },
            ["journeys"] = new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 16,
                ["items"] = Closed(new JsonObject
                {
                    ["id"] = Text(64),
                    ["title"] = Text(128),
                    ["description"] = Text(2048),
                    ["sections"] = Strings(64, 64),
                    ["detail"] = Choice("basic", "standard", "advanced"),
                    ["applicableWhen"] = Ref("predicate")
                }, "id", "title", "sections")
            }
        }, "sections", "journeys", "defaultJourney");
    }

    private static JsonObject Localization() => Closed(new JsonObject
    {
        ["defaultLocale"] = Text(128),
        ["translations"] = new JsonObject
        {
            ["type"] = "object",
            ["maxProperties"] = 128,
            ["additionalProperties"] = Closed(new JsonObject
            {
                ["direction"] = Choice("ltr", "rtl"),
                ["overrides"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = Text(4096) }
            })
        }
    }, "defaultLocale", "translations");

    private static void Header(JsonObject schema, string id, string title)
    {
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["$id"] = id;
        schema["title"] = title;
    }
    private static JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
    private static JsonObject Pointer() => new() { ["type"] = "string", ["maxLength"] = 65536, ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$" };
    private static JsonObject Describe(JsonObject schema, string description)
    {
        schema["description"] = description;
        return schema;
    }
    private static JsonObject Text(int maximum) => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = maximum };
    internal static JsonObject Choice(params string[] values) => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };
    private static JsonObject Strings(int maximum, int length, int minimum = 1) => new()
    {
        ["type"] = "array",
        ["minItems"] = minimum,
        ["maxItems"] = maximum,
        ["uniqueItems"] = true,
        ["items"] = Text(length)
    };
    internal static JsonObject Closed(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["additionalProperties"] = false,
        ["required"] = new JsonArray(required.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
    };
    internal static string Render(JsonObject schema) => schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
}
