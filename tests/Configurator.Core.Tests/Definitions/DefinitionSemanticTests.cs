using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionSemanticTests
{
    [Test]
    public async Task RequiredContainerAndItsRequiredLeafRepresentOneActionableObligation()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["properties"]!["service.name"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["name"],
             "properties":{"name":{"type":"string","x-configurator":{"sensitivity":"public"}}},
             "x-configurator":{"sensitivity":"public"}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        foreach ((string answers, bool ready) in new[]
        {
            ("{}", false), ("""{"service.name":{}}""", false),
            ("""{"service.name":{"name":"supplied"}}""", true)
        })
        {
            DefinitionValidationResult result = DefinitionValidator.Validate(definition, DefinitionFixture.Answers(answers));
            await Assert.That(result.IsReady).IsEqualTo(ready);
            await Assert.That(result.Nodes.Count(node => node.IsObligation)).IsEqualTo(1);
            await Assert.That(result.Nodes.Count(node => node.IsObligation && node.IsObligationReady)).IsEqualTo(ready ? 1 : 0);
        }
    }

    [Test]
    public async Task NestedProtectedDecisionsRetainTheirEffectiveSensitivity()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["configuration"]!["properties"]!["service.name"] = JsonNode.Parse("""
            {"type":"object","title":"Options","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"advanced"},
              "properties":{"token":{"type":"string","title":"Token","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}}
            """);
        var validation = DefinitionValidator.Validate(DefinitionFixture.Parse(root.ToJsonString()),
            DefinitionFixture.Answers("{}"));
        await Assert.That(validation.Fields[0].IsActive).IsTrue();
        await Assert.That(validation.Fields[0].Definition.Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    public async Task ActivationAndConditionalRequirednessControlValidationAndComposition()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["configuration"]!["properties"]!["enabled"] = JsonNode.Parse("""
            {"type":"boolean","x-configurator":{"sensitivity":"public"},"title":"Enabled"}
            """);
        root["configuration"]!["required"]!.AsArray().Add("enabled");
        root["configuration"]!["properties"]!["service.name"]!["x-configurator"]!["applicableWhen"] =
            JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}""");
        SetupDefinition definition = DefinitionFixture.Parse(root.ToJsonString());
        DefinitionValidationResult inactive = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"enabled":false,"service.name":"ignored"}"""));
        await Assert.That(inactive.IsReady).IsTrue();
        await Assert.That(inactive.Fields.Single(field => field.Id == "service.name").IsActive).IsFalse();
        await Assert.That(inactive.Fields.Single(field => field.Id == "service.name").IsRequired).IsFalse();
        DefinitionArtifactResult omitted = DefinitionArtifactComposer.Compose(definition, "config",
            DefinitionFixture.Answers("""{"enabled":false,"service.name":"ignored"}"""));
        await Assert.That(Encoding.UTF8.GetString(omitted.Artifact!.Bytes.Span)).IsEqualTo("{}");
        DefinitionValidationResult required = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"enabled":true}"""));
        await Assert.That(required.IsReady).IsFalse();
        await Assert.That(required.Fields.Single(field => field.Id == "service.name").IsRequired).IsTrue();
        await Assert.That(required.Fields.Single(field => field.Id == "service.name").HasError).IsTrue();
    }

    [Test]
    public async Task OptionalProtectionAndOutputAdviceDoNotInventRequiredObligations()
    {
        foreach (string sensitivity in new[] { "secret", "public" })
        {
            SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json
                .Replace("\"required\":[\"service.name\"]", "\"required\":[]", StringComparison.Ordinal)
                .Replace("\"sensitivity\":\"public\"", $"\"sensitivity\":\"{sensitivity}\"", StringComparison.Ordinal));
            var validation = DefinitionValidator.Validate(definition, DefinitionFixture.Answers("{}"));
            await Assert.That(validation.Fields[0].IsActive).IsTrue();
            await Assert.That(validation.Fields[0].IsRequired).IsFalse();
            await Assert.That(validation.IsReady).IsTrue();
        }
    }

    [Test]
    public async Task SnapshotSurvivesCallerMutationAndDocumentDisposal()
    {
        using JsonDocument source = JsonDocument.Parse("""{"service.name":"original"}""");
        var values = source.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value);
        var snapshot = new SetupAnswerSnapshot(42, values);
        values.Clear();
        source.Dispose();
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(DefinitionFixture.Parse(), "config", snapshot);
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("""{"name":"original"}""");
        await Assert.That(result.Artifact.InputRevision).IsEqualTo(42L);
    }

    [Test]
    public async Task RepeatedScalarProjectionUsesStableZeroBasedIndexes()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["configuration"]!["properties"] = JsonNode.Parse("""
            {"names":{"type":"array","items":{"type":"string"},"x-configurator":{"sensitivity":"public"},
              "title":"Names","maxItems":3}}
            """);
        root["configuration"]!["required"] = new JsonArray("names");
        root["outputs"]!["service.json"]!["projection"] = JsonNode.Parse("""
            {"op":"repeat","reference":{"scope":"root","path":"/names"},"item":{"op":"object","members":{
              "index":{"op":"index"},"name":{"op":"copy","reference":{"scope":"item","path":""}}}}}
            """);
        var definition = DefinitionFixture.Parse(root.ToJsonString());
        var result = DefinitionArtifactComposer.Compose(definition, "config",
            DefinitionFixture.Answers("""{"names":["alpha","beta"]}"""));
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span))
            .IsEqualTo("""[{"index":0,"name":"alpha"},{"index":1,"name":"beta"}]""");
        await Assert.That(DefinitionArtifactComposer.Compose(definition, "config",
            DefinitionFixture.Answers("""{"names":["a","b","c","d"]}""")).Succeeded).IsFalse();
    }

    [Test]
    public async Task StructuredLegalMarkdownIsDataAndNativeMarkdownEscapesActiveSyntax()
    {
        SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json.Replace(
            "\"maxLength\":80", "\"maxLength\":1000", StringComparison.Ordinal));
        var answers = DefinitionFixture.Answers("""{"service.name":"# Terms\n<script>x</script> [link](javascript:x)"}""");
        var result = DefinitionArtifactComposer.Compose(definition, "config", answers);
        using JsonDocument output = JsonDocument.Parse(result.Artifact!.Bytes);
        await Assert.That(output.RootElement.GetProperty("name").GetString())
            .IsEqualTo("# Terms\n<script>x</script> [link](javascript:x)");
        await Assert.That(result.Artifact.DefinitionRevision).IsEqualTo(1L);
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["outputs"]!["service.json"]!["format"] = "markdown";
        root["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"list","items":[{"op":"copy","reference":{"scope":"root","path":"/service.name"}}]}""");
        var markdown = DefinitionArtifactComposer.Compose(DefinitionFixture.Parse(root.ToJsonString()), "config", answers);
        await Assert.That(Encoding.UTF8.GetString(markdown.Artifact!.Bytes.Span))
            .IsEqualTo("\\# Terms\n\\<script\\>x\\</script\\> \\[link\\]\\(javascript:x\\)");
    }

    [Test]
    public async Task CanonicalDefinitionBytesHaveAnIndependentGoldenVector()
    {
        const string expected = """{"applicationVersions":["1.0"],"artifacts":[{"fileName":"service.json","format":"json","id":"config","projection":{"members":{"name":{"op":"copy","reference":{"path":"/service.name","scope":"root"}}},"op":"object"},"restartRequirement":"process","targetUpdateMode":"create"}],"configuration":{"additionalProperties":false,"properties":{"service.name":{"maxLength":80,"minLength":1,"title":"Service name","type":"string","x-configurator":{"detail":"advanced","sensitivity":"public"}}},"required":["service.name"],"type":"object","x-configurator":{"sensitivity":"public"}},"entryPoints":["setup"],"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","locales":["en"],"profile":"bounded-v2","project":{"id":"example.service","name":"Example service","publisher":"example"},"requiredCapabilities":["projection.copy","projection.object","schema.bounds","schema.object","schema.string"],"revision":1,"schemaVersion":2}""";
        await Assert.That(Encoding.UTF8.GetString(DefinitionFixture.Parse().CanonicalBytes.Span)).IsEqualTo(expected);
    }
}
