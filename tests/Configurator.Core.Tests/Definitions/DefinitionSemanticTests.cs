using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionSemanticTests
{
    [Test]
    public async Task NestedProtectedDecisionsCannotHideBehindPublicObjectMetadata()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["fields"] = JsonNode.Parse("""
            [{"id":"service.name","type":"object","title":"Options","sensitivity":"public","disclosure":"advanced",
              "properties":[{"id":"token","type":"string","title":"Token","sensitivity":"secret","disclosure":"advanced"}]}]
            """);
        var validation = DefinitionValidator.Validate(DefinitionFixture.Parse(root.ToJsonString()),
            DefinitionFixture.Answers("{}"));
        await Assert.That(validation.Fields[0].IsVisible(SetupDisplayMode.Guided)).IsTrue();
        await Assert.That(validation.Fields[0].Definition.Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    public async Task ActivationAndConditionalRequirednessShareOneValidationAcrossDisplayModes()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"enabled","type":"boolean","required":true,"sensitivity":"public","disclosure":"guided","title":"Enabled"}
            """));
        root["fields"]![0]!["required"] = false;
        root["fields"]![0]!["activeWhen"] = JsonNode.Parse("""{"op":"equals","field":"enabled","value":true}""");
        root["fields"]![0]!["requiredWhen"] = JsonNode.Parse("""{"op":"present","field":"enabled"}""");
        SetupDefinition definition = DefinitionFixture.Parse(root.ToJsonString());
        DefinitionValidationResult inactive = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"enabled":false,"service.name":"ignored"}"""));
        await Assert.That(inactive.IsReady).IsTrue();
        await Assert.That(inactive.Fields[0].IsActive).IsFalse();
        await Assert.That(inactive.Fields[0].IsVisible(SetupDisplayMode.Advanced)).IsFalse();
        await Assert.That(inactive.Fields[0].IsVisible(SetupDisplayMode.AllDeclared)).IsTrue();
        DefinitionArtifactResult omitted = DefinitionArtifactComposer.Compose(definition, "config",
            DefinitionFixture.Answers("""{"enabled":false,"service.name":"ignored"}"""));
        await Assert.That(Encoding.UTF8.GetString(omitted.Artifact!.Bytes.Span)).IsEqualTo("{}");
        DefinitionValidationResult required = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"enabled":true}"""));
        await Assert.That(required.IsReady).IsFalse();
        foreach (SetupDisplayMode mode in Enum.GetValues<SetupDisplayMode>())
            await Assert.That(required.Fields[0].IsVisible(mode)).IsTrue();
    }

    [Test]
    public async Task ActiveOptionalProtectedAndConsequenceFieldsAreNeverHiddenInGuidedMode()
    {
        foreach (string extra in new[] { "\"sensitivity\":\"secret\"", "\"sensitivity\":\"public\",\"consequence\":\"restart\"" })
        {
            SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json
                .Replace("\"required\":true", "\"required\":false", StringComparison.Ordinal)
                .Replace("\"sensitivity\":\"public\"", extra, StringComparison.Ordinal));
            var validation = DefinitionValidator.Validate(definition, DefinitionFixture.Answers("{}"));
            await Assert.That(validation.Fields[0].IsVisible(SetupDisplayMode.Guided)).IsTrue();
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
        root["fields"] = JsonNode.Parse("""
            [{"id":"names","type":"list","itemType":"string","required":true,"sensitivity":"public",
              "disclosure":"guided","title":"Names","maxItems":3}]
            """);
        root["artifacts"]![0]!["projection"] = JsonNode.Parse("""
            {"op":"repeat","field":"names","item":{"op":"object","members":{
              "index":{"op":"index"},"name":{"op":"copy","field":"$item"}}}}
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
        root["artifacts"]![0]!["format"] = "markdown";
        root["artifacts"]![0]!["projection"] = JsonNode.Parse(
            """{"op":"list","items":[{"op":"copy","field":"service.name"}]}""");
        var markdown = DefinitionArtifactComposer.Compose(DefinitionFixture.Parse(root.ToJsonString()), "config", answers);
        await Assert.That(Encoding.UTF8.GetString(markdown.Artifact!.Bytes.Span))
            .IsEqualTo("\\# Terms\n\\<script\\>x\\</script\\> \\[link\\]\\(javascript:x\\)");
    }

    [Test]
    public async Task CanonicalDefinitionBytesHaveAnIndependentGoldenVector()
    {
        const string expected = """{"applicationId":"example.service","applicationVersions":["1.0"],"artifacts":[{"applySemantics":"create","fileName":"service.json","format":"json","id":"config","projection":{"members":{"name":{"field":"service.name","op":"copy"}},"op":"object"},"restart":"process"}],"entryPoints":["setup"],"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","fields":[{"disclosure":"advanced","id":"service.name","maxLength":80,"minLength":1,"required":true,"sensitivity":"public","title":"Service name","type":"string"}],"locales":["en"],"publisher":"example","revision":1,"schemaVersion":1,"title":"Example service"}""";
        await Assert.That(Encoding.UTF8.GetString(DefinitionFixture.Parse().CanonicalBytes.Span)).IsEqualTo(expected);
    }
}
