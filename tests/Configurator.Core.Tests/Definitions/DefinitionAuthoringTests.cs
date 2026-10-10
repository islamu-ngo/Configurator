using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionAuthoringTests
{
    private static readonly string[] ExpectedFieldIds = ["SERVICE_PORT", "SERVICE_TOKEN"];
    internal const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v1",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.service","name":"Example Service"},"applicationVersions":["1.0"],
         "fields":{"SERVICE_PORT":{"type":"integer","sensitivity":"public","default":8080},
                   "SERVICE_TOKEN":{"type":"string","sensitivity":"secret","required":true}},
         "outputs":{".env":{"format":"dotenv"}}}
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
        await Assert.That(definition.Artifacts.Single().Restart).IsEqualTo("none");
        await Assert.That(definition.Artifacts.Single().ApplySemantics).IsEqualTo("generationOnly");
    }

    [Test]
    public async Task PrivateDefaultsAndUnknownContractsFailWithoutEchoingValues()
    {
        foreach (string source in new[]
        {
            Source.Replace("\"sensitivity\":\"public\"", "\"sensitivity\":\"sensitive\"", StringComparison.Ordinal),
            Source.Replace("\"sensitivity\":\"public\",", "", StringComparison.Ordinal),
            Source.Replace("\"type\":\"integer\"", "\"type\":\"integer\",\"exec\":\"synthetic-private-marker\"", StringComparison.Ordinal),
            Source.Replace("\"format\":\"dotenv\"", "\"format\":\"dotenv\",\"bindings\":{\"PORT\":\"missing\"}", StringComparison.Ordinal),
            Source.Replace("\"format\":\"dotenv\"", "\"format\":\"dotenv\",\"bindings\":{},\"projection\":{\"op\":\"literal\",\"value\":1}", StringComparison.Ordinal)
        })
            await Assert.That(() => Compile(source)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task NestedPrivateDefaultsAndActivationCyclesReject()
    {
        string nested = Source.Replace("\"type\":\"integer\",\"sensitivity\":\"public\",\"default\":8080",
            "\"type\":\"object\",\"sensitivity\":\"public\",\"default\":{\"token\":\"synthetic-private-marker\"},\"properties\":{\"token\":{\"type\":\"string\",\"sensitivity\":\"secret\"}}", StringComparison.Ordinal);
        await Assert.That(() => Compile(nested)).Throws<SetupDefinitionException>();
        string protectedParent = Source.Replace("\"type\":\"integer\",\"sensitivity\":\"public\",\"default\":8080",
            "\"type\":\"object\",\"sensitivity\":\"secret\",\"properties\":{\"token\":{\"type\":\"string\",\"sensitivity\":\"public\",\"default\":\"synthetic-private-marker\"}}", StringComparison.Ordinal)
            .Replace("\"format\":\"dotenv\"", "\"format\":\"json\"", StringComparison.Ordinal);
        await Assert.That(() => Compile(protectedParent)).Throws<SetupDefinitionException>();
        string cycle = Source.Replace("\"type\":\"integer\"", "\"type\":\"integer\",\"activeWhen\":{\"op\":\"present\",\"field\":\"SERVICE_PORT\"}", StringComparison.Ordinal);
        await Assert.That(() => Compile(cycle)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task SourceCardinalityRejectsBeforeOutputExpansion()
    {
        JsonObject source = JsonNode.Parse(Source)!.AsObject();
        var fields = new JsonObject();
        for (int index = 0; index < 1025; index++)
            fields["FIELD_" + index] = new JsonObject { ["type"] = "boolean" };
        source["fields"] = fields;
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
