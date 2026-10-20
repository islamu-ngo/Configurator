using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionObjectListTests
{
    internal const string Field = """
        {"id":"service.name","type":"list","itemType":"object","required":true,
         "sensitivity":"public","disclosure":"essential","title":"Rows","minItems":1,"maxItems":2,
         "uniqueBy":["metadata","name"],"properties":[
          {"id":"metadata","type":"object","required":true,"sensitivity":"public","disclosure":"essential","title":"Metadata","properties":[
           {"id":"name","type":"string","required":true,"sensitivity":"public","disclosure":"essential","title":"Name","minLength":1}]},
          {"id":"credential","type":"string","required":true,"sensitivity":"secret","disclosure":"essential","title":"Credential","minLength":1}]}
        """;

    internal static SetupDefinition Definition(string? field = null)
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["fields"] = JsonNode.Parse("[" + (field ?? Field) + "]");
        return DefinitionFixture.Parse(document.ToJsonString());
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
        foreach (string field in new[]
        {
            Field.Replace("[\"metadata\",\"name\"]", "[]", StringComparison.Ordinal),
            Field.Replace("[\"metadata\",\"name\"]", "[\"unknown\"]", StringComparison.Ordinal),
            Field.Replace("\"id\":\"name\",\"type\":\"string\",\"required\":true", "\"id\":\"name\",\"type\":\"string\",\"required\":false", StringComparison.Ordinal),
            Field.Replace("\"type\":\"string\",\"required\":true,\"sensitivity\":\"public\"", "\"type\":\"boolean\",\"required\":true,\"sensitivity\":\"public\"", StringComparison.Ordinal)
                .Replace(",\"minLength\":1", "", StringComparison.Ordinal),
            Field.Replace("\"id\":\"metadata\",\"type\":\"object\"", "\"id\":\"metadata\",\"type\":\"list\",\"itemType\":\"object\"", StringComparison.Ordinal),
            """{"id":"service.name","type":"list","itemType":"string","sensitivity":"public","disclosure":"essential","title":"Rows","uniqueBy":["name"]}"""
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
            new DefinitionDraftCompletion("config", "service.name/0/credential", "/name/0/credential"));
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
        JsonNode document = JsonNode.Parse(Definition().CanonicalBytes.Span)!;
        document["artifacts"]![0]!["projection"] = JsonNode.Parse(
            """{"op":"repeat","field":"service.name","item":{"op":"copy","field":"$item"}}""");
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
