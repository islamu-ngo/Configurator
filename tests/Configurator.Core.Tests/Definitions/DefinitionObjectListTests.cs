using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionObjectListTests
{
    internal const string Field = """
        {"type":"array","title":"Rows","minItems":1,"maxItems":2,
         "x-configurator":{"sensitivity":"public","detail":"basic","uniqueBy":"/metadata/name"},
         "items":{"type":"object","additionalProperties":false,"required":["metadata","credential"],
          "x-configurator":{"sensitivity":"public"},"properties":{
           "metadata":{"type":"object","additionalProperties":false,"required":["name"],
            "x-configurator":{"sensitivity":"public","detail":"basic"},"title":"Metadata","properties":{
             "name":{"type":"string","x-configurator":{"sensitivity":"public","detail":"basic","notBlank":true},"title":"Name","minLength":1}}},
           "credential":{"type":"string","x-configurator":{"sensitivity":"secret","detail":"basic","notBlank":true},"title":"Credential","minLength":1}}}}
        """;

    internal static SetupDefinition Definition(string? field = null)
    {
        return DefinitionFixture.Parse(Source(field));
    }

    private static string Source(string? field = null)
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["configuration"]!["properties"]!["service.name"] = JsonNode.Parse(field ?? Field);
        return document.ToJsonString();
    }

    [Test]
    public async Task ObjectRowsValidateUniquenessStructureBoundsAndRecursiveSensitivity()
    {
        SetupDefinition definition = Definition();
        await Assert.That(definition.Fields.Single().Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
        await Assert.That(DefinitionValidator.Validate(definition, DefinitionFixture.Answers(
            """{"service.name":[{"metadata":{"name":"one"},"credential":"synthetic"},{"metadata":{"name":"One"},"credential":"synthetic"}]}""")).IsReady).IsTrue();
        foreach (string rows in new[]
        {
            "[]", """[{"metadata":{"name":"one"},"credential":"synthetic"},{"metadata":{"name":"one"},"credential":"synthetic"}]""",
            """[{"metadata":{"name":"one"},"credential":"synthetic","extra":false}]""",
            """[{"metadata":{"name":"one"}}]""",
            """[{"metadata":{"name":"one"},"credential":"synthetic"},{"metadata":{"name":"two"},"credential":"synthetic"},{"metadata":{"name":"three"},"credential":"synthetic"}]"""
        })
        {
            DefinitionValidationResult invalid = DefinitionValidator.Validate(definition,
                DefinitionFixture.Answers("{\"service.name\":" + rows + "}"));
            await Assert.That(invalid.IsReady).IsFalse();
            await Assert.That(string.Join(",", invalid.Diagnostics)).DoesNotContain("synthetic");
        }
    }

    [Test]
    public async Task UniqueKeyAdmissionRejectsUnknownOptionalAndNonstringLeaves()
    {
        JsonNode sequenceIntermediate = JsonNode.Parse(Field)!;
        JsonNode metadata = sequenceIntermediate["items"]!["properties"]!["metadata"]!.DeepClone();
        sequenceIntermediate["items"]!["properties"]!["metadata"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = metadata,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
        };
        foreach (string field in new[]
        {
            Field.Replace("\"uniqueBy\":\"/metadata/name\"", "\"uniqueBy\":\"\"", StringComparison.Ordinal),
            Field.Replace("\"uniqueBy\":\"/metadata/name\"", "\"uniqueBy\":\"/unknown\"", StringComparison.Ordinal),
            Field.Replace("\"required\":[\"name\"]", "\"required\":[]", StringComparison.Ordinal),
            Field.Replace("\"name\":{\"type\":\"string\"", "\"name\":{\"type\":\"boolean\"", StringComparison.Ordinal)
                .Replace(",\"notBlank\":true", "", StringComparison.Ordinal).Replace(",\"minLength\":1", "", StringComparison.Ordinal),
            sequenceIntermediate.ToJsonString(),
            """{"type":"array","items":{"type":"string"},"x-configurator":{"sensitivity":"public","detail":"basic","uniqueBy":"/name"},"title":"Rows"}"""
        }) await Assert.That(() => Definition(field)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task DraftRowsPreservePublicSiblingsAndIdentifyExactSecretLocations()
    {
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(Definition(), "config",
            DefinitionFixture.Answers("""{"service.name":[{"metadata":{"name":"one"}}]}"""));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo(
            """{"name":[{"credential":"","metadata":{"name":"one"}}]}""");
        await Assert.That(result.Completions.Single()).IsEqualTo(
            new DefinitionDraftCompletion("config", "/service.name/0/credential", "/name/0/credential"));
        using DefinitionDraftArtifactResult duplicate = DefinitionDraftArtifactComposer.Compose(Definition(), "config",
            DefinitionFixture.Answers("""{"service.name":[{"metadata":{"name":"one"}},{"metadata":{"name":"one"}}]}"""));
        await Assert.That(duplicate.Artifact).IsNull();
        await Assert.That(duplicate.Diagnostics.Single().Code).IsEqualTo("value-duplicate");
        await Assert.That(duplicate.Diagnostics.Single().FieldId).IsEqualTo("service.name");
        await Assert.That(duplicate.Diagnostics.Single().RelativePath.SequenceEqual(["1", "metadata", "name"])).IsTrue();
    }

    [Test]
    public async Task RepeatedObjectCopiesPreserveRowsAndCompletionIndexes()
    {
        JsonNode document = JsonNode.Parse(Source())!;
        document["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"repeat","reference":{"scope":"root","path":"/service.name"},"item":{"op":"copy","reference":{"scope":"item","path":""}}}""");
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config",
            DefinitionFixture.Answers("""{"service.name":[{"metadata":{"name":"one"}},{"metadata":{"name":"two"}}]}"""));
        await Assert.That(draft.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span)).IsEqualTo(
            """[{"credential":"","metadata":{"name":"one"}},{"credential":"","metadata":{"name":"two"}}]""");
        await Assert.That(draft.Completions[0].OutputPath).IsEqualTo("/0/credential");
        await Assert.That(draft.Completions[1].OutputPath).IsEqualTo("/1/credential");
    }

    [Test]
    public async Task AnswerSnapshotsEnforceAggregateNodeAndScalarBudgetsAcrossFields()
    {
        JsonElement nodes = JsonSerializer.SerializeToElement(Enumerable.Range(0, 4000).ToArray());
        var nodeAnswers = Enumerable.Range(0, 17).ToDictionary(index => "field" + index, _ => nodes, StringComparer.Ordinal);
        await Assert.That(() => new SetupAnswerSnapshot(1, nodeAnswers)).Throws<SetupDefinitionException>();
        JsonElement scalar = JsonSerializer.SerializeToElement(new string('x', 65_536));
        var scalarAnswers = Enumerable.Range(0, 17).ToDictionary(index => "field" + index, _ => scalar, StringComparer.Ordinal);
        await Assert.That(() => new SetupAnswerSnapshot(1, scalarAnswers)).Throws<SetupDefinitionException>();
    }
}
