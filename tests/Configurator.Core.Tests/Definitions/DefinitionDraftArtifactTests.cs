using System.Text;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionDraftArtifactTests
{
    [Test]
    public async Task DraftBundleRetainsOrderedBytesCompletionsAndWithheldDiagnosticsInOnePass()
    {
        JsonNode document = JsonNode.Parse(SecretSource())!;
        document["outputs"]!["service1.json"] = JsonNode.Parse("""
            {"id":"independent","format":"json","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"literal","value":{"enabled":true}}}
            """);
        document["outputs"]!["service2.json"] = JsonNode.Parse("""
            {"id":"withheld","format":"json","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"literal","value":true,"when":{"op":"exists","reference":{"scope":"root","path":"/service.name"}}}}
            """);
        document["outputs"]!["service3.json"] = JsonNode.Parse("""
            {"id":"omitted","format":"json","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"omit"}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(document.ToJsonString());
        using var activity = new Activity(nameof(DraftBundleRetainsOrderedBytesCompletionsAndWithheldDiagnosticsInOnePass));
        activity.Start();
        using var measurement = new DefinitionBundleTests.PreparationMeasurement(activity);
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(
            definition, ["omitted", "withheld", "independent", "config"], DefinitionFixture.Answers("{}"));

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.IsDraft).IsTrue();
        await Assert.That(string.Join(',', result.Artifacts.Select(item => item.ArtifactId))).IsEqualTo("config,independent");
        await Assert.That(result.Artifacts[0].Bytes.Span.SequenceEqual("""{"name":""}"""u8)).IsTrue();
        await Assert.That(result.Artifacts[1].Bytes.Span.SequenceEqual("""{"enabled":true}"""u8)).IsTrue();
        await Assert.That(result.Artifacts[0].Digest.Value)
            .IsEqualTo("1390696a77e5d6f4375e9b36450c26bb67e99662fcd31b71cecd8ceda332040e");
        await Assert.That(result.Completions.Single()).IsEqualTo(new DefinitionDraftCompletion("config", "/service.name", "/name"));
        await Assert.That(string.Join(',', result.Diagnostics.Select(item => item.Code)))
            .IsEqualTo("draft-dependency-unresolved,artifact-omitted");
        await Assert.That(result.Diagnostics[0].FieldId).IsEqualTo("service.name");
        await Assert.That(result.Diagnostics[1].FieldId).IsEqualTo("");
        await Assert.That(string.Join(',', measurement.Composed)).IsEqualTo("config,independent");
        await Assert.That(measurement.Validations).IsEqualTo(1);
    }

    [Test]
    public async Task MissingSecretsProduceExplicitPlaceholdersWithoutWeakeningCompleteComposition()
    {
        foreach ((string format, string expected) in new[]
        {
            ("json", """{"name":""}"""), ("yaml", "\"name\": \"\"\n"), ("dotenv", "# Draft: fill deferred secret keys before use.\nNAME=\n")
        })
        {
            SetupDefinition definition = SecretDefinition(format);
            SetupAnswerSnapshot answers = DefinitionFixture.Answers("{}");
            await Assert.That(DefinitionArtifactComposer.Compose(definition, "config", answers).Succeeded).IsFalse();
            using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(definition, "config", answers);
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.IsDraft).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo(expected);
            await Assert.That(result.Completions.Single()).IsEqualTo(new DefinitionDraftCompletion("config", "/service.name", format == "dotenv" ? "/NAME" : "/name"));
            await Assert.That(result.Artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
        }
    }

    [Test]
    public async Task MissingAndInvalidNonsecretAnswersCannotBecomeDrafts()
    {
        foreach (string answers in new[] { "{}", """{"service.name":false}""" })
        {
            using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
                DefinitionFixture.Parse(), "config", DefinitionFixture.Answers(answers));
            await Assert.That(result.Artifact).IsNull();
        }
    }

    [Test]
    public async Task ExplicitlyInvalidSecretsNeverBecomeDeferredPlaceholders()
    {
        foreach (string answers in new[] { """{"service.name":""}""", """{"service.name":"   "}""", """{"service.name":false}""" })
        {
            using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
                SecretDefinition(), "config", DefinitionFixture.Answers(answers));
            await Assert.That(result.Artifact).IsNull();
            await Assert.That(result.Completions).IsEmpty();
            await Assert.That(result.Diagnostics.Any(item => item.Code == "value-invalid")).IsTrue();
        }
    }

    [Test]
    public async Task SecretDependentNotConditionWithholdsArtifactInsteadOfSelectingTopology()
    {
        JsonNode source = JsonNode.Parse(SecretSource())!;
        source["outputs"]!["service.json"]!["projection"]!["when"] = JsonNode.Parse(
            """{"op":"not","operands":[{"op":"exists","reference":{"scope":"root","path":"/service.name"}}]}""");
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(source.ToJsonString()), "config", DefinitionFixture.Answers("{}"));
        await Assert.That(result.Artifact).IsNull();
        await Assert.That(result.Diagnostics.Any(item => item.Code == "draft-dependency-unresolved")).IsTrue();
    }

    [Test]
    public async Task DisposedOutputRejectsReadsAndCallerCloneRemainsIndependent()
    {
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            SecretDefinition(), "config", DefinitionFixture.Answers("{}"));
        byte[] clone = result.Artifact!.Bytes.ToArray();
        result.Dispose();
        await Assert.That(() => result.Artifact.Bytes).Throws<ObjectDisposedException>();
        await Assert.That(Encoding.UTF8.GetString(clone)).IsEqualTo("""{"name":""}""");
    }

    [Test]
    public async Task DeferredNumericLeavesUseNullAndMarkdownUsesDeclaredSections()
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonNode field = document["configuration"]!["properties"]!["service.name"]!;
        field["x-configurator"]!["sensitivity"] = "secret";
        field["type"] = "integer";
        field.AsObject().Remove("minLength");
        field.AsObject().Remove("maxLength");
        using DefinitionDraftArtifactResult numeric = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("{}"));
        await Assert.That(Encoding.UTF8.GetString(numeric.Artifact!.Bytes.Span)).IsEqualTo("""{"name":null}""");

        field["type"] = "string";
        document["outputs"]!["service.json"]!["format"] = "markdown";
        document["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"list","items":[{"op":"literal","value":"Credential: "},{"op":"copy","reference":{"scope":"root","path":"/service.name"}}]}""");
        using DefinitionDraftArtifactResult markdown = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("{}"));
        await Assert.That(Encoding.UTF8.GetString(markdown.Artifact!.Bytes.Span)).IsEqualTo("Credential: ");
        await Assert.That(markdown.Completions.Single().OutputPath).IsEqualTo("/1");
    }

    [Test]
    public async Task DeferredActivationWithholdsDependentArtifactAndPreservesUnrelatedOutput()
    {
        JsonNode document = JsonNode.Parse(SecretSource())!;
        document["configuration"]!["properties"]!["dependent"] = JsonNode.Parse("""
            {"type":"string","title":"Dependent","x-configurator":{"sensitivity":"public","detail":"basic",
             "applicableWhen":{"op":"not","operands":[{"op":"exists","reference":{"scope":"root","path":"/service.name"}}]}}}
            """);
        document["configuration"]!["required"]!.AsArray().Add("dependent");
        document["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"copy","reference":{"scope":"root","path":"/dependent"}}""");
        document["outputs"]!["service1.json"] = JsonNode.Parse("""
            {"id":"independent","format":"json","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"literal","value":{"enabled":true}}}
            """);
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(
            DefinitionFixture.Parse(document.ToJsonString()), ["config", "independent"], DefinitionFixture.Answers("{}"));
        await Assert.That(result.Artifacts.Count).IsEqualTo(1);
        await Assert.That(result.Artifacts.Single().ArtifactId).IsEqualTo("independent");
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("draft-dependency-unresolved");
        await Assert.That(result.Completions.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeferredRequiredDependencyPropagatesBeforeDependentActivation(bool reverseOrder)
    {
        JsonNode document = JsonNode.Parse(SecretSource())!;
        document["configuration"]!["properties"]!["conditional"] = JsonNode.Parse("""
            {"type":"string","title":"Conditional","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """);
        document["configuration"]!["properties"]!["dependent"] = JsonNode.Parse("""
            {"type":"string","title":"Dependent","x-configurator":{"sensitivity":"public","detail":"basic",
             "applicableWhen":{"op":"not","operands":[{"op":"exists","reference":{"scope":"root","path":"/conditional"}}]}}}
            """);
        document["configuration"]!["if"] = JsonNode.Parse("""{"required":["service.name"]}""");
        document["configuration"]!["then"] = JsonNode.Parse("""{"required":["conditional"]}""");
        document["configuration"]!["required"]!.AsArray().Add("dependent");
        document["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"copy","reference":{"scope":"root","path":"/dependent"}}""");
        if (reverseOrder)
        {
            var reversed = new JsonObject();
            foreach ((string name, JsonNode? value) in document["configuration"]!["properties"]!.AsObject().Reverse())
                reversed.Add(name, value?.DeepClone());
            document["configuration"]!["properties"] = reversed;
        }
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("""{"dependent":"public"}"""));
        await Assert.That(result.Artifact).IsNull();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("draft-dependency-unresolved");
    }

    [Test]
    public async Task ProvidedValidAnswerResolvesPresenceDespiteUnknownConditionalRequirement()
    {
        JsonNode document = JsonNode.Parse(SecretSource())!;
        document["configuration"]!["properties"]!["conditional"] = JsonNode.Parse("""
            {"type":"string","title":"Conditional","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """);
        document["configuration"]!["properties"]!["dependent"] = JsonNode.Parse("""
            {"type":"string","title":"Dependent","x-configurator":{"sensitivity":"public","detail":"basic",
             "applicableWhen":{"op":"exists","reference":{"scope":"root","path":"/conditional"}}}}
            """);
        document["configuration"]!["if"] = JsonNode.Parse("""{"required":["service.name"]}""");
        document["configuration"]!["then"] = JsonNode.Parse("""{"required":["conditional"]}""");
        document["configuration"]!["required"]!.AsArray().Add("dependent");
        document["outputs"]!["service.json"]!["projection"] = JsonNode.Parse(
            """{"op":"copy","reference":{"scope":"root","path":"/dependent"}}""");
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("""{"conditional":"provided","dependent":"public"}"""));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.IsDraft).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("\"public\"");
    }

    [Test]
    public async Task MutualPresenceRequirementsDoNotInventMissingWork()
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["configuration"]!["required"] = new JsonArray();
        document["configuration"]!["properties"]!["other"] = JsonNode.Parse(
            """{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        document["configuration"]!["dependentRequired"] = JsonNode.Parse(
            """{"service.name":["other"],"other":["service.name"]}""");
        SetupDefinition definition = DefinitionFixture.Parse(document.ToJsonString());
        await Assert.That(DefinitionValidator.Validate(definition, DefinitionFixture.Answers("{}")).IsReady).IsTrue();
        DefinitionValidationResult missing = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"other":"provided"}"""));
        await Assert.That(missing.IsReady).IsFalse();
        await Assert.That(missing.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required" &&
            diagnostic.FieldId == "service.name")).IsTrue();
    }

    [Test]
    public async Task RelationshipFeedbackCannotEraseTheMissingWorkThatCausedUncertainty()
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["configuration"]!["required"] = new JsonArray();
        document["configuration"]!["properties"]!["other"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public","applicableWhen":{
             "op":"not","operands":[{"op":"exists","reference":{"scope":"root","path":"/service.name"}}]}}}
            """);
        document["configuration"]!["dependentRequired"] = JsonNode.Parse("""{"other":["service.name"]}""");
        DefinitionValidationResult validation = DefinitionValidator.Validate(
            DefinitionFixture.Parse(document.ToJsonString()), DefinitionFixture.Answers("""{"other":"provided"}"""));
        await Assert.That(validation.IsReady).IsFalse();
        await Assert.That(validation.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required")).IsTrue();
    }

    [Test]
    public async Task FinalArtifactBudgetFailureClearsPreviousDraftOutputAndCompletionMetadata()
    {
        JsonNode document = JsonNode.Parse(SecretSource())!;
        document["configuration"]!["properties"]!["blob"] = JsonNode.Parse("""
            {"type":"string","maxLength":65536,"x-configurator":{"sensitivity":"public"}}
            """);
        var members = new JsonObject();
        for (int index = 0; index < 17; index++)
            members[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = JsonNode.Parse(
                """{"op":"copy","reference":{"scope":"root","path":"/blob"}}""");
        document["outputs"]!["service1.json"] = new JsonObject
        {
            ["id"] = "invalid", ["format"] = "json",
            ["projection"] = new JsonObject { ["op"] = "object", ["members"] = members }
        };
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(
            DefinitionFixture.Parse(document.ToJsonString()), ["config", "invalid"], DefinitionFixture.Answers(
                JsonSerializer.Serialize(new Dictionary<string, string> { ["blob"] = new('x', 65_536) })));
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Completions.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("artifact-limit");
    }

    [Test]
    public async Task AggregateDraftFailureExposesNeitherPrefixNorCompletionMetadata()
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["configuration"]!["properties"]!["service.name"]!["maxLength"] = SetupCompositionLimits.DefaultScalarCharacters;
        document["configuration"]!["properties"]!["secret"] = JsonNode.Parse("""
            {"type":"string","title":"Secret","x-configurator":{"sensitivity":"secret","detail":"basic"}}
            """);
        document["configuration"]!["required"]!.AsArray().Add("secret");
        document["outputs"]!["service.json"]!["projection"]!["members"]!["credential"] = JsonNode.Parse(
            """{"op":"copy","reference":{"scope":"root","path":"/secret"}}""");
        for (int index = 1; index < 64; index++)
        {
            JsonNode artifact = document["outputs"]!["service.json"]!.DeepClone();
            artifact["id"] = "output" + index;
            document["outputs"]!["service" + index + ".json"] = artifact;
        }
        SetupDefinition definition = DefinitionFixture.Parse(document.ToJsonString());
        string answers = "{\"service.name\":\"" + new string('x', SetupCompositionLimits.DefaultScalarCharacters) + "\"}";
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(definition,
            definition.Artifacts.Select(item => item.Id), DefinitionFixture.Answers(answers));
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Completions.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("bundle-limit");
    }

    private static SetupDefinition SecretDefinition(string format = "json") => DefinitionFixture.Parse(SecretSource(format));

    private static string SecretSource(string format = "json")
    {
        string json = DefinitionFixture.Json
            .Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"secret\"", StringComparison.Ordinal)
            .Replace("\"detail\":\"advanced\"", "\"detail\":\"advanced\",\"notBlank\":true", StringComparison.Ordinal)
            .Replace("\"format\":\"json\"", $"\"format\":\"{format}\"", StringComparison.Ordinal);
        if (format == "dotenv") json = json.Replace("\"name\":{\"op\"", "\"NAME\":{\"op\"", StringComparison.Ordinal);
        return json;
    }
}
