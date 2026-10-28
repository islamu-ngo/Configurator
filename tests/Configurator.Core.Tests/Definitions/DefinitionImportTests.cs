using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionImportTests
{
    [Test]
    public async Task UnaddressableTargetPointersRefuseWithoutThrowing()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["outputs"]!["service.json"]!["projection"]!["members"] = new JsonObject
        {
            [new string('x', 65_536)] = JsonNode.Parse(
                """{"op":"copy","reference":{"scope":"root","path":"/service.name"}}""")
        };
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(DefinitionImport.CanImport(definition, "config")).IsFalse();
        DefinitionImportResult result = DefinitionImport.Assess(definition, "config", "{}"u8.ToArray(), Empty());
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(result.Diagnostic).IsNull();
    }

    [Test]
    public async Task NestedCopiesReconstructExactPointersAndRoundTripIndependentBytes()
    {
        foreach ((string format, string bytes) in new[]
        {
            ("json", """{"out":{"literal/name":"host","tilde~name":7}}"""),
            ("yaml", "\"out\":\n  \"literal/name\": \"host\"\n  \"tilde~name\": 7\n")
        })
        {
            SetupDefinition definition = NestedDefinition(format);
            DefinitionImportResult result = DefinitionImport.Assess(definition, "nested",
                Encoding.UTF8.GetBytes(bytes), Empty());
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(JsonElement.DeepEquals(result.Answers!.Values["options"],
                JsonSerializer.SerializeToElement(new Dictionary<string, object>
                {
                    ["db/host"] = "host", ["~port"] = 7
                }))).IsTrue();
            await Assert.That(result.ProvidedFields.SequenceEqual(["options"])).IsTrue();
            DefinitionArtifactResult roundTrip = DefinitionArtifactComposer.Compose(definition, "nested", result.Answers);
            await Assert.That(roundTrip.Succeeded).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(roundTrip.Artifact!.Bytes.Span)).IsEqualTo(bytes);
        }
    }

    [Test]
    public async Task NestedImportMergesSiblingsAndProtectsConflictsAndExplicitOmissions()
    {
        SetupDefinition definition = NestedDefinition("json");
        var current = new SetupAnswerSnapshot(8, new Dictionary<string, JsonElement>
        {
            ["options"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["db/host"] = "old", ["~port"] = 9
            })
        });
        byte[] bytes = """{"out":{"literal/name":"new"}}"""u8.ToArray();
        DefinitionImportResult conflict = DefinitionImport.Assess(definition, "nested", bytes, current);
        await Assert.That(conflict.Code).IsEqualTo("answer-conflict");
        await Assert.That(conflict.Answers).IsNull();
        await Assert.That(current.Values["options"].GetProperty("db/host").GetString()).IsEqualTo("old");
        await Assert.That(current.InputRevision).IsEqualTo(8);
        DefinitionImportResult replaced = DefinitionImport.Assess(definition, "nested", bytes, current,
            new HashSet<string>(StringComparer.Ordinal) { "options" });
        await Assert.That(replaced.Succeeded).IsTrue();
        await Assert.That(replaced.Answers!.Values["options"].GetProperty("db/host").GetString()).IsEqualTo("new");
        await Assert.That(replaced.Answers.Values["options"].GetProperty("~port").GetInt32()).IsEqualTo(9);
        foreach (string pointer in new[] { "/options", "/options/db~1host" })
        {
            var omitted = new SetupAnswerSnapshot(8, new Dictionary<string, JsonElement>(),
                [DefinitionPath.Parse(pointer)]);
            DefinitionImportResult result = DefinitionImport.Assess(definition, "nested", bytes, omitted,
                new HashSet<string>(StringComparer.Ordinal) { "options" });
            await Assert.That(result.Code).IsEqualTo("answer-conflict");
            await Assert.That(result.Answers).IsNull();
            await Assert.That(omitted.Omitted.Contains(DefinitionPath.Parse(pointer))).IsTrue();
        }
    }

    [Test]
    public async Task UnknownNestedInputRejectsAtomicallyWithoutPrivateDiagnostics()
    {
        SetupDefinition definition = NestedDefinition("json");
        foreach (string input in new[]
        {
            """{"out":{"literal/name":"host","synthetic-private-key":"synthetic-private-value"}}""",
            """{"out":{"literal/name":"host"},"synthetic-private-key":{}}""",
            """{"out":[]}""",
            """{"out":{"literal/name":"host","literal/name":"other"}}"""
        })
        {
            DefinitionImportResult result = DefinitionImport.Assess(definition, "nested",
                Encoding.UTF8.GetBytes(input), Empty());
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Answers).IsNull();
            await Assert.That(result.Diagnostic).IsNull();
            await Assert.That(result.ToString()).DoesNotContain("synthetic-private");
        }
    }

    [Test]
    public async Task WholeRootCopyPreservesDeclaredTypedMapEntriesAndRejectsInvalidEntries()
    {
        JsonNode source = JsonNode.Parse(Source)!;
        source["configuration"]!["additionalProperties"] = JsonNode.Parse("""{"type":"integer"}""");
        source["outputs"] = JsonNode.Parse("""
            {"root.json":{"id":"root","format":"json","projection":{"op":"copy","reference":{"scope":"root","path":""}}}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        const string bytes = """{"synthetic-private/key":7}""";
        DefinitionImportResult result = DefinitionImport.Assess(definition, "root",
            Encoding.UTF8.GetBytes(bytes), Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["synthetic-private/key"].GetInt32()).IsEqualTo(7);
        await Assert.That(result.ProvidedFields.Count).IsEqualTo(0);
        DefinitionArtifactResult composed = DefinitionArtifactComposer.Compose(definition, "root", result.Answers);
        await Assert.That(Encoding.UTF8.GetString(composed.Artifact!.Bytes.Span)).IsEqualTo(bytes);
        DefinitionImportResult invalid = DefinitionImport.Assess(definition, "root",
            """{"synthetic-private/key":"invalid"}"""u8.ToArray(), Empty());
        await Assert.That(invalid.Succeeded).IsFalse();
        await Assert.That(invalid.Answers).IsNull();
        await Assert.That(invalid.Diagnostic).IsNull();
        var current = new SetupAnswerSnapshot(2, new Dictionary<string, JsonElement>
        {
            ["synthetic-private/key"] = JsonSerializer.SerializeToElement(8)
        });
        DefinitionImportResult conflict = DefinitionImport.Assess(definition, "root",
            Encoding.UTF8.GetBytes(bytes), current);
        await Assert.That(conflict.Code).IsEqualTo("answer-conflict");
        await Assert.That(conflict.Diagnostic).IsNull();
    }

    [Test]
    public async Task NestedDotenvCopiesRestoreDeclaredScalarTypes()
    {
        SetupDefinition definition = NestedDefinition("dotenv");
        DefinitionImportResult result = DefinitionImport.Assess(definition, "nested",
            "HOST=host\nPORT=7\n"u8.ToArray(), Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["options"].GetProperty("db/host").GetString()).IsEqualTo("host");
        await Assert.That(result.Answers.Values["options"].GetProperty("~port").GetInt32()).IsEqualTo(7);
        await Assert.That(DefinitionImport.Assess(definition, "nested",
            "PORT=07\n"u8.ToArray(), Empty()).Succeeded).IsFalse();
    }

    [Test]
    public async Task SourceOverlapAndArrayIndexMappingsHaveNoInverse()
    {
        foreach (string members in new[]
        {
            """{"a":{"op":"copy","reference":{"scope":"root","path":"/options"}},"b":{"op":"copy","reference":{"scope":"root","path":"/options/db~1host"}}}""",
            """{"a":{"op":"copy","reference":{"scope":"root","path":"/options/db~1host"}},"b":{"op":"copy","reference":{"scope":"root","path":"/options/db~1host"}}}""",
            """{"a":{"op":"copy","reference":{"scope":"root","path":"/tags/0"}}}"""
        })
        {
            JsonNode source = NestedSource("json");
            source["outputs"]!["nested.json"]!["projection"]!["members"] = JsonNode.Parse(members);
            SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
            await Assert.That(DefinitionImport.CanImport(definition, "nested")).IsFalse();
            await Assert.That(DefinitionImport.Assess(definition, "nested",
                """{"a":"host"}"""u8.ToArray(), Empty()).Answers).IsNull();
        }
    }

    [Test]
    public async Task CoreCandidateValidationOwnsNestedSourceApplicability()
    {
        JsonNode source = NestedSource("json");
        source["configuration"]!["properties"]!["options"]!["properties"]!["db/host"]!["x-configurator"] =
            JsonNode.Parse("""
                {"sensitivity":"public","applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}
                """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        foreach (bool enabled in new[] { false, true })
        {
            var current = new SetupAnswerSnapshot(3, new Dictionary<string, JsonElement>
            {
                ["enabled"] = JsonSerializer.SerializeToElement(enabled)
            });
            DefinitionImportResult result = DefinitionImport.Assess(definition, "nested",
                """{"out":{"literal/name":"host"}}"""u8.ToArray(), current);
            await Assert.That(result.Succeeded).IsEqualTo(enabled);
            if (!enabled) await Assert.That(result.Answers).IsNull();
        }
    }

    [Test]
    public async Task WholeCopiesProtectOmittedDescendantsAndRejectUndeclaredMembers()
    {
        JsonNode source = NestedSource("json");
        source["outputs"]!["nested.json"]!["projection"] = JsonNode.Parse("""
            {"op":"object","members":{"out":{"op":"copy","reference":{"scope":"root","path":"/options"}}}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        var omitted = new SetupAnswerSnapshot(4, new Dictionary<string, JsonElement>(),
            [DefinitionPath.Parse("/options/db~1host")]);
        DefinitionImportResult conflict = DefinitionImport.Assess(definition, "nested",
            """{"out":{"db/host":"host"}}"""u8.ToArray(), omitted,
            new HashSet<string>(StringComparer.Ordinal) { "options" });
        await Assert.That(conflict.Code).IsEqualTo("answer-conflict");
        await Assert.That(conflict.Answers).IsNull();
        DefinitionImportResult sibling = DefinitionImport.Assess(definition, "nested",
            """{"out":{"~port":7}}"""u8.ToArray(), omitted);
        await Assert.That(sibling.Succeeded).IsTrue();
        await Assert.That(sibling.Answers!.Omitted.Contains(DefinitionPath.Parse("/options/db~1host"))).IsTrue();
        DefinitionImportResult unknown = DefinitionImport.Assess(definition, "nested",
            """{"out":{"synthetic-private-key":"synthetic-private-value"}}"""u8.ToArray(), Empty());
        await Assert.That(unknown.Succeeded).IsFalse();
        await Assert.That(unknown.Answers).IsNull();
        await Assert.That(unknown.Diagnostic!.RelativePath.Count).IsEqualTo(0);
        await Assert.That(unknown.Diagnostic.ToString()).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task WholeArraysAndLiteralNumericPropertiesImportButRepeatsDoNot()
    {
        JsonNode source = NestedSource("json");
        source["configuration"]!["properties"]!["options"]!["properties"]!["0"] =
            JsonNode.Parse("""{"type":"string"}""");
        source["outputs"]!["nested.json"]!["projection"] = JsonNode.Parse("""
            {"op":"object","members":{
             "array":{"op":"copy","reference":{"scope":"root","path":"/tags"}},
             "literal":{"op":"copy","reference":{"scope":"root","path":"/options/0"}}}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        DefinitionImportResult result = DefinitionImport.Assess(definition, "nested",
            """{"array":["one","two"],"literal":"zero"}"""u8.ToArray(), Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["tags"].GetArrayLength()).IsEqualTo(2);
        await Assert.That(result.Answers.Values["options"].GetProperty("0").GetString()).IsEqualTo("zero");
        source["outputs"]!["nested.json"]!["projection"]!["members"]!["array"] = JsonNode.Parse("""
            {"op":"repeat","reference":{"scope":"root","path":"/tags"},
             "item":{"op":"copy","reference":{"scope":"item","path":""}}}
            """);
        SetupDefinition repeated = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(DefinitionImport.CanImport(repeated, "nested")).IsFalse();
        await Assert.That(DefinitionImport.Assess(repeated, "nested",
            """{"array":["one"]}"""u8.ToArray(), Empty()).Answers).IsNull();
    }

    private static SetupDefinition NestedDefinition(string format) => DefinitionFixture.Parse(NestedSource(format).ToJsonString());

    private static JsonNode NestedSource(string format)
    {
        JsonNode source = JsonNode.Parse(Source)!;
        source["configuration"]!["properties"]!["options"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
             "properties":{"db/host":{"type":"string"},"~port":{"type":"integer"}}}
            """);
        source["outputs"] = format == "dotenv" ? JsonNode.Parse("""
            {"nested.env":{"id":"nested","format":"dotenv",
             "bindings":{"HOST":{"scope":"root","path":"/options/db~1host"},"PORT":{"scope":"root","path":"/options/~0port"}}}}
            """) : JsonNode.Parse("""
            {"nested.json":{"id":"nested","format":"json",
             "projection":{"op":"object","members":{"out":{"op":"object","members":{
              "literal/name":{"op":"copy","reference":{"scope":"root","path":"/options/db~1host"}},
              "tilde~name":{"op":"copy","reference":{"scope":"root","path":"/options/~0port"}}}}}}}}
            """);
        if (format != "dotenv") source["outputs"]!["nested.json"]!["format"] = format;
        return source;
    }

    [Test]
    public async Task InvalidNestedImportRetainsOnlyTheAdmittedLiteralDiagnosticPath()
    {
        JsonNode source = JsonNode.Parse(Source)!;
        source["configuration"]!["properties"]!["options"] = JsonNode.Parse("""
        {"type":"object","title":"Options","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
         "properties":{"db.host":{"type":"string","title":"Host","maxLength":3,"x-configurator":{"sensitivity":"public"}}}}
        """);
        source["outputs"]!["config.json"]!["projection"]!["members"]!["options"] = JsonNode.Parse(
            """{"op":"copy","reference":{"scope":"root","path":"/options"}}""");
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        DefinitionImportResult result = DefinitionImport.Assess(definition, "config",
            """{"enabled":true,"options":{"db.host":"synthetic-private-import"}}"""u8.ToArray(), Empty());
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(result.Diagnostic!.FieldId).IsEqualTo("options");
        await Assert.That(result.Diagnostic.RelativePath.Count).IsEqualTo(1);
        await Assert.That(result.Diagnostic.RelativePath[0]).IsEqualTo("db.host");
        await Assert.That(result.ToString()).DoesNotContain("synthetic-private-import");
    }

    [Test]
    public async Task DirectCopyPrefillsWithoutRevealingValuesInResultText()
    {
        SetupDefinition definition = Definition();
        DefinitionImportResult result = DefinitionImport.Assess(definition, "config",
            """{"name":"private-import-marker","enabled":true}"""u8.ToArray(), Empty());

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.InputRevision).IsEqualTo(1);
        await Assert.That(result.Answers.Values["service.name"].GetString()).IsEqualTo("private-import-marker");
        await Assert.That(result.ProvidedFields.Count).IsEqualTo(2);
        await Assert.That(result.ProvidedFields).Contains("enabled");
        await Assert.That(result.ProvidedFields).Contains("service.name");
        await Assert.That(DefinitionImport.CanImport(definition, "config")).IsTrue();
        await Assert.That(DefinitionImport.CanImport(definition, "transformed")).IsFalse();
        await Assert.That(result.ToString()).DoesNotContain("private-import-marker");
    }

    [Test]
    public async Task AmbiguousInvalidAndUnsupportedInputLeavesAnswersUnchanged()
    {
        SetupDefinition definition = Definition();
        foreach (byte[] bytes in new[]
        {
            """{"name":"one","name":"two"}"""u8.ToArray(),
            """{"name":"private-import-marker","extra":1}"""u8.ToArray(),
            """{"name":42}"""u8.ToArray(),
            "{bad}"u8.ToArray(),
            new byte[4_194_305]
        })
        {
            DefinitionImportResult result = DefinitionImport.Assess(definition, "config", bytes, Empty());
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Answers).IsNull();
            await Assert.That(result.ToString()).DoesNotContain("private-import-marker");
        }

        DefinitionImportResult transformed = DefinitionImport.Assess(definition, "transformed",
            """{"enabled":true}"""u8.ToArray(), Empty());
        await Assert.That(transformed.Succeeded).IsFalse();
        DefinitionImportResult conditional = DefinitionImport.Assess(definition, "conditional",
            """{"enabled":true}"""u8.ToArray(), Empty());
        await Assert.That(conditional.Succeeded).IsFalse();
        DefinitionImportResult repeated = DefinitionImport.Assess(definition, "repeated",
            """{"enabled":true}"""u8.ToArray(), Empty());
        await Assert.That(repeated.Succeeded).IsFalse();
    }

    [Test]
    public async Task DotenvAndYamlUseDeclaredFieldTypes()
    {
        SetupDefinition definition = Definition();
        DefinitionImportResult dotenv = DefinitionImport.Assess(definition, "environment",
            "ENABLED=true\nNAME=private-import-marker\nCOUNT=7\nRATE=1.5\n"u8.ToArray(), Empty());
        await Assert.That(dotenv.Succeeded).IsTrue();
        await Assert.That(dotenv.Answers!.Values["enabled"].ValueKind).IsEqualTo(JsonValueKind.True);
        await Assert.That(dotenv.Answers.Values["service.name"].GetString()).IsEqualTo("private-import-marker");
        await Assert.That(dotenv.Answers.Values["count"].GetInt64()).IsEqualTo(7);
        await Assert.That(dotenv.Answers.Values["rate"].GetDecimal()).IsEqualTo(1.5m);
        DefinitionImportResult yaml = DefinitionImport.Assess(definition, "yaml",
            "enabled: true\nname: private-import-marker\n"u8.ToArray(), Empty());
        await Assert.That(yaml.Succeeded).IsTrue();
        await Assert.That(yaml.Answers!.Values["enabled"].ValueKind).IsEqualTo(JsonValueKind.True);
        await Assert.That(DefinitionImport.Assess(definition, "environment",
            "ENABLED=TRUE\n"u8.ToArray(), Empty()).Succeeded).IsFalse();
        JsonNode unsupported = JsonNode.Parse(Source)!;
        unsupported["outputs"]!["unsupported.env"] = JsonNode.Parse("""
            {"id":"unsupported-env","format":"dotenv","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"object","members":{
               "TAGS":{"op":"copy","reference":{"scope":"root","path":"/tags"}},
               "ENABLED":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}}
            """);
        await Assert.That(() => DefinitionFixture.Parse(unsupported.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task InactiveAndConflictingFieldsRejectTheWholeImport()
    {
        SetupDefinition definition = Definition();
        DefinitionImportResult inactive = DefinitionImport.Assess(definition, "config",
            """{"name":"private-import-marker","enabled":false}"""u8.ToArray(), Empty());
        await Assert.That(inactive.Succeeded).IsFalse();

        var existing = new SetupAnswerSnapshot(4, new Dictionary<string, JsonElement>
        {
            ["service.name"] = JsonSerializer.SerializeToElement("different")
        });
        DefinitionImportResult conflict = DefinitionImport.Assess(definition, "config",
            """{"name":"private-import-marker","enabled":true}"""u8.ToArray(), existing);
        await Assert.That(conflict.Succeeded).IsFalse();
        await Assert.That(conflict.Answers).IsNull();
        await Assert.That(conflict.Diagnostic!.FieldId).IsEqualTo("service.name");
        await Assert.That(conflict.ToString()).DoesNotContain("private-import-marker");
        DefinitionImportResult unknown = DefinitionImport.Assess(definition, "config",
            """{"private-import-marker":"private-import-marker"}"""u8.ToArray(), Empty());
        await Assert.That(unknown.Diagnostic).IsNull();
        var unknownCurrent = new SetupAnswerSnapshot(4, new Dictionary<string, JsonElement>
        {
            ["private-import-marker"] = JsonSerializer.SerializeToElement("private-import-marker")
        });
        DefinitionImportResult global = DefinitionImport.Assess(definition, "config",
            """{"enabled":true}"""u8.ToArray(), unknownCurrent);
        await Assert.That(global.Succeeded).IsFalse();
        await Assert.That(global.Diagnostic?.FieldId).IsNull();
        await Assert.That(global.Diagnostic).IsNull();
    }

    private static SetupAnswerSnapshot Empty() => new(0, new Dictionary<string, JsonElement>());

    private static SetupDefinition Definition() => DefinitionFixture.Parse(Source);

    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"2b51dfa0-024c-4d9b-a5ae-e38da01ec862","revision":1},
         "project":{"id":"example.service","name":"Example","publisher":"example"},
         "applicationVersions":["1"],"entryPoints":["setup"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
          "properties":{
           "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public"}},
           "count":{"type":"integer","title":"Count","x-configurator":{"sensitivity":"public"}},
           "rate":{"type":"number","title":"Rate","x-configurator":{"sensitivity":"public"}},
           "tags":{"type":"array","items":{"type":"string"},"title":"Tags","x-configurator":{"sensitivity":"public"}},
           "service.name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"secret",
             "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}}},
         "outputs":{
          "config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "config.yaml":{"id":"yaml","format":"yaml","targetUpdateMode":"create","restartRequirement":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "config.env":{"id":"environment","format":"dotenv","targetUpdateMode":"create","restartRequirement":"none",
           "bindings":{"NAME":{"scope":"root","path":"/service.name"},"ENABLED":{"scope":"root","path":"/enabled"},"COUNT":{"scope":"root","path":"/count"},"RATE":{"scope":"root","path":"/rate"}}},
          "transformed.json":{"id":"transformed","format":"json","targetUpdateMode":"create","restartRequirement":"none",
           "projection":{"op":"object","members":{"name":{"op":"literal","value":"fixed"},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "conditional.json":{"id":"conditional","format":"json","targetUpdateMode":"create","restartRequirement":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"},"when":{"op":"exists","reference":{"scope":"root","path":"/enabled"}}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "repeated.json":{"id":"repeated","format":"json","targetUpdateMode":"create","restartRequirement":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/enabled"}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}}}}
        """;
}
