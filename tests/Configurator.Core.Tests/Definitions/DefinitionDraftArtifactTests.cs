using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionDraftArtifactTests
{
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
            await Assert.That(result.Completions.Single()).IsEqualTo(new DefinitionDraftCompletion("config", "service.name", format == "dotenv" ? "/NAME" : "/name"));
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
    public async Task SecretDependentNotConditionWithholdsArtifactInsteadOfSelectingTopology()
    {
        string source = Encoding.UTF8.GetString(SecretDefinition().CanonicalBytes.Span);
        source = source.Replace("\"projection\":{\"members\"", "\"projection\":{\"when\":{\"op\":\"not\",\"operands\":[{\"op\":\"present\",\"field\":\"service.name\"}]},\"members\"", StringComparison.Ordinal);
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(source), "config", DefinitionFixture.Answers("{}"));
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
        document["fields"]![0]!["sensitivity"] = "secret";
        document["fields"]![0]!["type"] = "integer";
        document["fields"]![0]!.AsObject().Remove("minLength");
        document["fields"]![0]!.AsObject().Remove("maxLength");
        using DefinitionDraftArtifactResult numeric = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("{}"));
        await Assert.That(Encoding.UTF8.GetString(numeric.Artifact!.Bytes.Span)).IsEqualTo("""{"name":null}""");

        document["artifacts"]![0]!["format"] = "markdown";
        document["artifacts"]![0]!["projection"] = JsonNode.Parse(
            """{"op":"list","items":[{"op":"literal","value":"Credential: "},{"op":"copy","field":"service.name"}]}""");
        using DefinitionDraftArtifactResult markdown = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("{}"));
        await Assert.That(Encoding.UTF8.GetString(markdown.Artifact!.Bytes.Span)).IsEqualTo("Credential: ");
        await Assert.That(markdown.Completions.Single().OutputPath).IsEqualTo("/1");
    }

    [Test]
    public async Task DeferredActivationWithholdsDependentArtifactAndPreservesUnrelatedOutput()
    {
        JsonNode document = JsonNode.Parse(SecretDefinition().CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"dependent","type":"string","title":"Dependent","required":true,"sensitivity":"public",
             "disclosure":"essential","activeWhen":{"op":"not","operands":[{"op":"present","field":"service.name"}]}}
            """));
        document["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"copy","field":"dependent"}""");
        document["artifacts"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"independent","format":"json","fileName":"independent.json","applySemantics":"create","restart":"none",
             "projection":{"op":"literal","value":{"enabled":true}}}
            """));
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
        JsonNode document = JsonNode.Parse(SecretDefinition().CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"conditional","type":"string","title":"Conditional","sensitivity":"public","disclosure":"essential",
             "requiredWhen":{"op":"present","field":"service.name"}}
            """));
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"dependent","type":"string","title":"Dependent","required":true,"sensitivity":"public","disclosure":"essential",
             "activeWhen":{"op":"not","operands":[{"op":"present","field":"conditional"}]}}
            """));
        document["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"copy","field":"dependent"}""");
        if (reverseOrder) document["fields"] = new JsonArray(document["fields"]!.AsArray().Reverse().Select(item => item!.DeepClone()).ToArray());
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("""{"dependent":"public"}"""));
        await Assert.That(result.Artifact).IsNull();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("draft-dependency-unresolved");
    }

    [Test]
    public async Task ProvidedValidAnswerResolvesPresenceDespiteUnknownConditionalRequirement()
    {
        JsonNode document = JsonNode.Parse(SecretDefinition().CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"conditional","type":"string","title":"Conditional","sensitivity":"public","disclosure":"essential",
             "requiredWhen":{"op":"present","field":"service.name"}}
            """));
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"dependent","type":"string","title":"Dependent","required":true,"sensitivity":"public","disclosure":"essential",
             "activeWhen":{"op":"present","field":"conditional"}}
            """));
        document["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"copy","field":"dependent"}""");
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(
            DefinitionFixture.Parse(document.ToJsonString()), "config", DefinitionFixture.Answers("""{"conditional":"provided","dependent":"public"}"""));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.IsDraft).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)).IsEqualTo("\"public\"");
    }

    [Test]
    public async Task FinalArtifactShapeFailureClearsPreviousDraftOutputAndCompletionMetadata()
    {
        JsonNode document = JsonNode.Parse(SecretDefinition().CanonicalBytes.Span)!;
        document["artifacts"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"invalid","format":"dotenv","fileName":"invalid.env","applySemantics":"create","restart":"none",
             "projection":{"op":"literal","value":[]}}
            """));
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(
            DefinitionFixture.Parse(document.ToJsonString()), ["config", "invalid"], DefinitionFixture.Answers("{}"));
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Completions.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("dotenv-shape");
    }

    [Test]
    public async Task AggregateDraftFailureExposesNeitherPrefixNorCompletionMetadata()
    {
        JsonNode document = JsonNode.Parse(DefinitionFixture.Json)!;
        document["fields"]![0]!["maxLength"] = SetupCompositionLimits.DefaultScalarCharacters;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"secret","type":"string","title":"Secret","required":true,"sensitivity":"secret","disclosure":"essential"}
            """));
        document["artifacts"]![0]!["projection"]!["members"]!["credential"] = JsonNode.Parse("""{"op":"copy","field":"secret"}""");
        for (int index = 1; index < 64; index++)
        {
            JsonNode artifact = document["artifacts"]![0]!.DeepClone();
            artifact["id"] = "output" + index;
            artifact["fileName"] = "output" + index + ".json";
            document["artifacts"]!.AsArray().Add(artifact);
        }
        SetupDefinition definition = DefinitionFixture.Parse(document.ToJsonString());
        string answers = "{\"service.name\":\"" + new string('x', SetupCompositionLimits.DefaultScalarCharacters) + "\"}";
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(definition,
            definition.Artifacts.Select(item => item.Id), DefinitionFixture.Answers(answers));
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Completions.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("bundle-limit");
    }

    private static SetupDefinition SecretDefinition(string format = "json")
    {
        string json = DefinitionFixture.Json
            .Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"secret\"", StringComparison.Ordinal)
            .Replace("\"format\":\"json\"", $"\"format\":\"{format}\"", StringComparison.Ordinal);
        if (format == "dotenv") json = json.Replace("\"name\":{\"op\"", "\"NAME\":{\"op\"", StringComparison.Ordinal);
        return DefinitionFixture.Parse(json);
    }
}
