using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionArtifactVocabularyTests
{
    [Test]
    [Arguments("dotenv", DefinitionArtifactFormat.Dotenv)]
    [Arguments("json", DefinitionArtifactFormat.Json)]
    [Arguments("yaml", DefinitionArtifactFormat.Yaml)]
    [Arguments("markdown", DefinitionArtifactFormat.Markdown)]
    public async Task AdmittedFormatUsesTheExactToken(string token, DefinitionArtifactFormat expected)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject artifact = source["outputs"]!["service.json"]!.AsObject();
        artifact["format"] = token;
        if (token == "markdown") artifact["projection"] = JsonNode.Parse("""{"op":"literal","value":["# Title"]}""");
        Definition definition = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(definition.Artifacts.Single().Format).IsEqualTo(expected);
        await Assert.That(expected.ToToken()).IsEqualTo(token);
        await Assert.That(DefinitionCodec.ParseYaml(definition.CanonicalBytes).Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    [Arguments("format", "\"JSON\"", "vocabulary-invalid")]
    [Arguments("format", "\"synthetic-private-token\"", "vocabulary-invalid")]
    [Arguments("format", "1", "definition-invalid")]
    [Arguments("format", "null", "vocabulary-invalid")]
    [Arguments("targetUpdateMode", "\"generation-only\"", "vocabulary-invalid")]
    [Arguments("targetUpdateMode", "0", "definition-invalid")]
    [Arguments("restartRequirement", "\"PROCESS\"", "vocabulary-invalid")]
    [Arguments("restartRequirement", "null", "vocabulary-invalid")]
    public async Task RuntimeTokensRejectUnknownCasingNumbersAndNull(string property, string value, string code)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Parse().CanonicalBytes.Span)!.AsObject();
        source["artifacts"]![0]![property] = JsonNode.Parse(value);
        DefinitionException? failure = null;
        try { _ = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())); }
        catch (DefinitionException exception) { failure = exception; }
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Code).IsEqualTo(code);
        await Assert.That(failure.ToString()).DoesNotContain("synthetic-private-token");
    }

    [Test]
    [Arguments("format")]
    [Arguments("targetUpdateMode")]
    [Arguments("restartRequirement")]
    public async Task RuntimeArtifactTokensAreRequired(string property)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Parse().CanonicalBytes.Span)!.AsObject();
        source["artifacts"]![0]!.AsObject().Remove(property);
        await Assert.That(() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
            .Throws<DefinitionException>();
    }

    [Test]
    public async Task UndefinedEnumValuesCannotProduceWireTokens()
    {
        await Assert.That(() => default(DefinitionArtifactFormat).ToToken()).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => ((DefinitionArtifactFormat)999).ToToken()).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => default(DefinitionTargetUpdateMode).ToToken()).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => ((DefinitionRestartRequirement)999).ToToken()).Throws<ArgumentOutOfRangeException>();
        await Assert.That(DefinitionTargetUpdateMode.GenerationOnly.ToToken()).IsEqualTo("generationOnly");
    }
}
