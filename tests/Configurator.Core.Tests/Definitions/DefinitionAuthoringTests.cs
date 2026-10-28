using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionAuthoringTests
{
    private static readonly string[] ExpectedFieldIds = ["SERVICE_PORT", "SERVICE_TOKEN"];
    internal const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.service","name":"Example Service"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"required":["SERVICE_TOKEN"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "SERVICE_PORT":{"type":"integer","x-configurator":{"sensitivity":"public","initialValue":8080}},
             "SERVICE_TOKEN":{"type":"string","x-configurator":{"sensitivity":"secret","notBlank":true}}}},
         "outputs":{".env":{"format":"dotenv","bindings":{
           "SERVICE_PORT":{"scope":"root","path":"/SERVICE_PORT"},
           "SERVICE_TOKEN":{"scope":"root","path":"/SERVICE_TOKEN"}}}}}
        """;

    internal static SetupDefinition Compile(string source = Source) =>
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));

    [Test]
    public async Task CompactSourceGeneratesAdmittedMetadataAndLiteralOutputKeys()
    {
        SetupDefinition definition = Compile();
        await Assert.That(definition.ApplicationId).IsEqualTo("example.service");
        await Assert.That(definition.Fields.Select(field => field.Id).ToArray()).IsEquivalentTo(ExpectedFieldIds);
        await Assert.That(definition.Artifacts.Single().FileName).IsEqualTo(".env");
        await Assert.That(definition.Artifacts.Single().Restart).IsEqualTo("unspecified");
        await Assert.That(definition.Artifacts.Single().ApplySemantics).IsEqualTo("unspecified");
    }

    [Test]
    public async Task PrivateDefaultsAndUnknownContractsFailWithoutEchoingValues()
    {
        foreach (string source in new[]
        {
            Source.Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"sensitive\"", StringComparison.Ordinal),
            Source.Replace("\"sensitivity\":\"public\",", "", StringComparison.Ordinal)
                .Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"sensitive\"", StringComparison.Ordinal),
            Source.Replace("\"type\":\"integer\"", "\"type\":\"integer\",\"exec\":\"synthetic-private-marker\"", StringComparison.Ordinal),
            Source.Replace("\"path\":\"/SERVICE_PORT\"", "\"path\":\"/missing\"", StringComparison.Ordinal),
            Source.Replace("\"format\":\"dotenv\"", "\"format\":\"dotenv\",\"projection\":{\"op\":\"literal\",\"value\":1}", StringComparison.Ordinal)
        })
            await Assert.That(() => Compile(source)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task NestedPrivateDefaultsAndActivationCyclesReject()
    {
        string nested = Source.Replace("\"type\":\"integer\",\"x-configurator\":{\"sensitivity\":\"public\",\"initialValue\":8080}",
            "\"type\":\"object\",\"additionalProperties\":false,\"x-configurator\":{\"sensitivity\":\"public\"},\"default\":{\"token\":\"synthetic-private-marker\"},\"properties\":{\"token\":{\"type\":\"string\",\"x-configurator\":{\"sensitivity\":\"secret\"}}}", StringComparison.Ordinal);
        JsonObject nestedSource = JsonNode.Parse(nested)!.AsObject();
        nestedSource["outputs"] = JsonNode.Parse("""{"service.json":{"format":"json"}}""");
        await Assert.That(() => Compile(nestedSource.ToJsonString())).Throws<SetupDefinitionException>();
        string protectedParent = Source.Replace("\"type\":\"integer\",\"x-configurator\":{\"sensitivity\":\"public\",\"initialValue\":8080}",
            "\"type\":\"object\",\"additionalProperties\":false,\"x-configurator\":{\"sensitivity\":\"secret\"},\"properties\":{\"token\":{\"type\":\"string\",\"default\":\"synthetic-private-marker\",\"x-configurator\":{\"sensitivity\":\"public\"}}}", StringComparison.Ordinal)
            .Replace("\"format\":\"dotenv\"", "\"format\":\"json\"", StringComparison.Ordinal);
        JsonObject protectedSource = JsonNode.Parse(protectedParent)!.AsObject();
        protectedSource["outputs"] = JsonNode.Parse("""{"service.json":{"format":"json"}}""");
        await Assert.That(() => Compile(protectedSource.ToJsonString())).Throws<SetupDefinitionException>();
        string cycle = Source.Replace("\"initialValue\":8080", "\"initialValue\":8080,\"applicableWhen\":{\"op\":\"exists\",\"reference\":{\"scope\":\"root\",\"path\":\"/SERVICE_PORT\"}}", StringComparison.Ordinal);
        await Assert.That(() => Compile(cycle)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task SourceCardinalityRejectsBeforeOutputExpansion()
    {
        JsonObject source = JsonNode.Parse(Source)!.AsObject();
        var fields = new JsonObject();
        for (int index = 0; index < 1025; index++)
            fields["FIELD_" + index] = new JsonObject { ["type"] = "boolean" };
        source["configuration"]!["properties"] = fields;
        source["configuration"]!["required"] = new JsonArray();
        try
        {
            _ = Compile(source.ToJsonString());
            throw new InvalidOperationException("Expected bounded source rejection.");
        }
        catch (SetupDefinitionException exception)
        {
            await Assert.That(exception.Code).IsEqualTo("document-limit");
        }
    }
}
