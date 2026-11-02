using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionSecretReferenceTests
{
    [Test]
    [Arguments("public")]
    [Arguments("invalid-label")]
    [Arguments(null)]
    public async Task ReferencesCannotHideContradictoryOrUnknownLabelsBehindSecretMetadata(string? sensitivity)
    {
        JsonObject source = Source(new JsonObject
        {
            ["sensitivity"] = sensitivity,
            ["secret"] = JsonNode.Parse("""{"kind":"password","acquisition":"existing"}""")
        });
        await Assert.That(() => DefinitionFixture.Parse(source.ToJsonString())).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task AnnotationOnlyReferencePreservesTheTargetsPublicClassification()
    {
        SetupDefinition definition = DefinitionFixture.Parse(Source(new JsonObject { ["detail"] = "advanced" }).ToJsonString());
        await Assert.That(definition.Fields.Single().Sensitivity).IsEqualTo(SetupFieldSensitivity.Public);
    }

    private static JsonObject Source(JsonObject metadata)
    {
        JsonObject source = JsonNode.Parse(DefinitionFixture.Json)!.AsObject();
        JsonObject configuration = source["configuration"]!.AsObject();
        configuration["$defs"] = JsonNode.Parse("""{"text":{"type":"string","x-configurator":{"sensitivity":"public"}}}""");
        configuration["properties"]!["service.name"] = new JsonObject { ["$ref"] = "#/$defs/text", ["x-configurator"] = metadata };
        return source;
    }
}
