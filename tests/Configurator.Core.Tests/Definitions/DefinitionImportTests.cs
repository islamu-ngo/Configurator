using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionImportTests
{
    [Test]
    public async Task InvalidNestedImportRetainsOnlyTheAdmittedLiteralDiagnosticPath()
    {
        JsonNode source = JsonNode.Parse(Definition().CanonicalBytes.Span)!;
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
        {"id":"options","type":"object","title":"Options","sensitivity":"public","disclosure":"guided",
         "properties":[{"id":"db.host","type":"string","title":"Host","maxLength":3,"sensitivity":"public","disclosure":"guided"}]}
        """));
        source["artifacts"]![0]!["projection"]!["members"]!["options"] = JsonNode.Parse("""{"op":"copy","field":"options"}""");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
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
        await Assert.That(DefinitionImport.Assess(definition, "unsupported-env",
            "ENABLED=true\n"u8.ToArray(), Empty()).Succeeded).IsFalse();
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

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {"schemaVersion":1,"familyId":"2b51dfa0-024c-4d9b-a5ae-e38da01ec862","revision":1,
         "applicationId":"example.service","applicationVersions":["1"],"publisher":"example",
         "title":"Example","entryPoints":["setup"],"locales":["en"],
         "fields":[
          {"id":"enabled","type":"boolean","title":"Enabled","sensitivity":"public","disclosure":"guided"},
          {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
          {"id":"rate","type":"number","title":"Rate","sensitivity":"public","disclosure":"guided"},
          {"id":"tags","type":"list","itemType":"string","title":"Tags","sensitivity":"public","disclosure":"guided"},
          {"id":"service.name","type":"string","title":"Name","sensitivity":"secret","disclosure":"guided",
           "activeWhen":{"op":"equals","field":"enabled","value":true}}],
         "artifacts":[
          {"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"yaml","format":"yaml","fileName":"config.yaml","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"environment","format":"dotenv","fileName":"config.env","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"NAME":{"op":"copy","field":"service.name"},"ENABLED":{"op":"copy","field":"enabled"},"COUNT":{"op":"copy","field":"count"},"RATE":{"op":"copy","field":"rate"}}}},
          {"id":"unsupported-env","format":"dotenv","fileName":"unsupported.env","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"TAGS":{"op":"copy","field":"tags"},"ENABLED":{"op":"copy","field":"enabled"}}}},
          {"id":"transformed","format":"json","fileName":"transformed.json","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"name":{"op":"literal","value":"fixed"},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"conditional","format":"json","fileName":"conditional.json","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name","when":{"op":"present","field":"enabled"}},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"repeated","format":"json","fileName":"repeated.json","applySemantics":"create","restart":"none",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"enabled"},"enabled":{"op":"copy","field":"enabled"}}}}]}
        """));
}
