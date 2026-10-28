using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionArtifactTests
{
    [Test]
    public async Task ExportedPredicateSchemasShareTheRootAndItemReferenceContract()
    {
        foreach (string exported in new[] { DefinitionSchema.Generate(), DefinitionAuthoringSchema.Generate() })
        {
            using JsonDocument schema = JsonDocument.Parse(exported);
            JsonElement definitions = schema.RootElement.GetProperty("$defs");
            await Assert.That(definitions.GetProperty("reference").GetProperty("properties").GetProperty("scope")
                .GetProperty("enum").EnumerateArray().Select(scope => scope.GetString()).SequenceEqual(["root", "item"])).IsTrue();
            foreach (JsonElement predicate in definitions.GetProperty("predicate").GetProperty("oneOf").EnumerateArray())
                if (predicate.GetProperty("properties").TryGetProperty("reference", out JsonElement reference))
                    await Assert.That(reference.TryGetProperty("$ref", out JsonElement shared) &&
                        shared.GetString() == "#/$defs/reference").IsTrue();
        }
    }

    [Test]
    public async Task SerializerIncompatibleMarkdownRejectsBeforeAnswerCollection()
    {
        foreach (string projection in new[]
        {
            """{"op":"literal","value":42}""",
            """{"op":"list","items":[{"op":"literal","value":42}]}""",
            """{"op":"copy","reference":{"scope":"root","path":"/service.name"}}"""
        })
        {
            JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
            source["outputs"] = JsonNode.Parse("""
                {"notes.md":{"id":"notes","format":"markdown","projection":{"op":"omit"}}}
                """);
            source["outputs"]!["notes.md"]!["projection"] = JsonNode.Parse(projection);
            await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
        }
    }

    [Test]
    public async Task RepeatedItemConditionsUseTheSameTypedPredicateKernel()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["required"] = new JsonArray("rows");
        source["configuration"]!["properties"] = JsonNode.Parse("""
            {"rows":{"type":"array","items":{"type":"object","additionalProperties":false,
             "required":["enabled","name"],"properties":{
              "enabled":{"type":"boolean"},"name":{"type":"string"}}}}}
            """);
        source["outputs"] = JsonNode.Parse("""
            {"rows.json":{"id":"rows","format":"json","projection":{
             "op":"repeat","reference":{"scope":"root","path":"/rows"},
             "item":{"op":"copy","reference":{"scope":"item","path":"/name"},
              "when":{"op":"equals","reference":{"scope":"item","path":"/enabled"},"value":true}}}}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "rows",
            DefinitionFixture.Answers("""{"rows":[{"enabled":true,"name":"one"},{"enabled":false,"name":"two"}]}"""));
        using DefinitionArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("""["one"]""");
    }

    [Test]
    public async Task NullableRepeatSourceRejectsBeforeAValidNullCanReachTheSerializer()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["required"] = new JsonArray("rows");
        source["configuration"]!["properties"] = JsonNode.Parse("""
            {"rows":{"type":["array","null"],"items":{"type":"string"}}}
            """);
        source["outputs"]!["service.json"]!["projection"] = JsonNode.Parse("""
            {"op":"repeat","reference":{"scope":"root","path":"/rows"},
             "item":{"op":"copy","reference":{"scope":"item","path":""}}}
            """);
        await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task CheckedNeutralSampleProducesIndependentNativeBytesAndReadiness()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,
            "examples", "minimal-self-hosted", "setup-definition.json"))) root = root.Parent;
        string sample = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Neutral sample missing."),
            "examples", "minimal-self-hosted");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(sample, "setup-definition.json")));
        SetupAnswerSnapshot answers = SetupAnswerSnapshotCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(sample, "answers", "complete.json")));

        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"service.name":""}""")).IsReady).IsFalse();
        await Assert.That(DefinitionValidator.Validate(definition, answers).IsReady).IsTrue();
        SetupAnswerSnapshot publicOnly = DefinitionFixture.Answers(
            """{"service.mode":"extended","metrics.enabled":true}""");
        DefinitionValidationResult partial = DefinitionValidator.Validate(definition, publicOnly);
        await Assert.That(partial.IsReady).IsFalse();
        await Assert.That(partial.Fields.Single(field => field.Id == "service.credential").IsRequired).IsTrue();
        await Assert.That(partial.Fields.Single(field => field.Id == "service.credential").IsProvided).IsFalse();
        foreach ((string artifactId, string expectedFile) in new[]
        {
            ("environment", "environment.env"),
            ("configuration", "service.json"),
            ("configuration-yaml", "service.yaml")
        })
        {
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, artifactId, answers);
            await Assert.That(result.Succeeded).IsTrue();
            byte[] expected = File.ReadAllBytes(Path.Combine(sample, "expected", expectedFile));
            await Assert.That(result.Artifact!.Bytes.Span.SequenceEqual(expected)).IsTrue();
            await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
            await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
        }
    }

    [Test]
    public async Task NeutralProjectionProducesPinnedJsonBytesAndInputRevision()
    {
        SetupDefinition definition = DefinitionFixture.Parse();
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config", DefinitionFixture.Answers());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("""{"name":"sample"}""");
        await Assert.That(result.Artifact.InputRevision).IsEqualTo(7L);
        await Assert.That(result.Artifact.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
    }

    [Test]
    public async Task MissingRequiredFieldsRemainVisibleAndCannotRender()
    {
        SetupDefinition definition = DefinitionFixture.Parse();
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, DefinitionFixture.Answers("{}"));
        await Assert.That(validation.IsReady).IsFalse();
        await Assert.That(validation.Fields.Single().HasError).IsTrue();
        await Assert.That(DefinitionArtifactComposer.Compose(definition, "config", DefinitionFixture.Answers("{}")).Artifact)
            .IsNull();
    }

    [Test]
    public async Task NativeFormatsPreserveValueTypingAndCannotInjectSyntax()
    {
        foreach ((string format, string expected) in new[]
        {
            ("json", """{"NAME":"line # text"}"""),
            ("yaml", "\"NAME\": \"line # text\"\n"),
            ("dotenv", "NAME=\"line # text\"\n")
        })
        {
            SetupDefinition definition = DefinitionFixture.Parse(DefinitionFixture.Json
                .Replace("\"format\":\"json\"", $"\"format\":\"{format}\"", StringComparison.Ordinal)
                .Replace("\"name\":{\"op\"", "\"NAME\":{\"op\"", StringComparison.Ordinal));
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "config",
                DefinitionFixture.Answers("""{"service.name":"line # text"}"""));
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo(expected);
        }
    }
}
