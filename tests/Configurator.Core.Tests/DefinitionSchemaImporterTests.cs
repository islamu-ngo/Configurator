using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests;

public sealed class DefinitionSchemaImporterTests
{
    [Test]
    public async Task ImportsClosedObjectsArraysAndEnumsWithPrivateDefaultsRemoved()
    {
        const string schema = """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
            "properties":{"token":{"type":"string","writeOnly":true,"default":"synthetic-private-default"},
            "mode":{"type":"string","enum":["a","b"]},
            "items":{"type":"array","maxItems":8,"items":{"type":"object","additionalProperties":false,
            "properties":{"enabled":{"type":"boolean"}},"required":["enabled"]}}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        var properties = imported.Configuration.GetProperty("properties");
        await Assert.That(properties.GetProperty("token").GetProperty("x-configurator").GetProperty("sensitivity").GetString()).IsEqualTo("secret");
        await Assert.That(properties.GetProperty("token").TryGetProperty("default", out _)).IsFalse();
        await Assert.That(properties.GetProperty("mode").GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(properties.GetProperty("mode").GetProperty("enum").GetArrayLength()).IsEqualTo(2);
        await Assert.That(properties.GetProperty("items").GetProperty("items").GetProperty("type").GetString()).IsEqualTo("object");
        await Assert.That(properties.GetProperty("items").GetProperty("items").GetProperty("required")[0].GetString()).IsEqualTo("enabled");
    }

    [Test]
    public async Task BoundedDomainDifferencesRequireAcknowledgmentAndRemainReported()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"required":["name"],"properties":{
            "name":{"type":"string","minLength":1,"maxLength":12},"count":{"type":"integer","minimum":0,"maximum":20}}}
            """;
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema)))
            .Throws<DefinitionSchemaImportException>();
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true);
        await Assert.That(imported.Differences).IsEquivalentTo(["schema-numeric-bounded-domain"]);
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("count").GetProperty("maximum").GetInt32()).IsEqualTo(20);
    }

    [Test]
    public async Task SuppliedLocalReferencesExpandAndCyclesReject()
    {
        const string schema = """{"type":"object","additionalProperties":false,"properties":{"flag":{"$ref":"common.json#/$defs/flag"}}}""";
        var references = new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["common.json"] = Encoding.UTF8.GetBytes("""{"$defs":{"flag":{"type":"boolean"}}}""")
        };
        await Assert.That(DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), references).Configuration.GetProperty("properties").GetProperty("flag")
            .GetProperty("type").GetString()).IsEqualTo("boolean");
        const string cycle = """{"type":"object","additionalProperties":false,"$defs":{"loop":{"$ref":"#/$defs/loop"}},"properties":{"x":{"$ref":"#/$defs/loop"}}}""";
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(cycle), acceptBoundedSemantics: true))
            .Throws<DefinitionSchemaImportException>();
    }

    [Test]
    public async Task UnsupportedAssertionsAndReferencesCannotBeAcknowledgedAway()
    {
        string[] fields =
        [
            """{"type":"string","pattern":".*"}""",
            """{"type":["string","null"]}""",
            """{"anyOf":[{"type":"string"},{"type":"boolean"}]}""",
            """{"type":"object","additionalProperties":true}""",
            """{"type":"array","prefixItems":[{"type":"boolean"}]}""",
            """{"$ref":"https://example.invalid/schema.json"}""",
            """{"$ref":"../outside.json"}""",
            """{"$ref":"common%20schema.json"}""",
            """{"$ref":"#/$defs/flag~2"}""",
            """{"type":"string","x-assert-format":true}"""
        ];
        foreach (string field in fields)
        {
            string schema = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"x\":" + field + "}}";
            await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true))
                .Throws<DefinitionSchemaImportException>();
        }
    }

    [Test]
    public async Task ReferencedAndRootWriteOnlyAnnotationsKeepSecretFloor()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"writeOnly":true,
            "$defs":{"flag":{"type":"boolean","writeOnly":true}},
            "properties":{"first":{"$ref":"#/$defs/flag","writeOnly":false},"second":{"type":"string"}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("first").GetProperty("x-configurator").GetProperty("sensitivity").GetString()).IsEqualTo("secret");
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("second").GetProperty("x-configurator").GetProperty("sensitivity").GetString()).IsEqualTo("secret");
    }

    [Test]
    public async Task ReferenceExpansionRejectsAggregateMetadataAmplification()
    {
        var properties = new JsonObject();
        for (int index = 0; index < 1024; index++)
            properties["field" + index] = new JsonObject { ["$ref"] = "#/$defs/text" };
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["$defs"] = new JsonObject
            {
                ["text"] = new JsonObject { ["type"] = "string", ["description"] = new string('x', 4096) }
            }
        };
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema.ToJsonString())))
            .Throws<DefinitionSchemaImportException>();
    }

    [Test]
    public async Task DescriptiveFormatAnnotationsCompileWithoutBecomingValidators()
    {
        const string schema = """{"type":"object","additionalProperties":false,"properties":{"address":{"type":"string","description":"Server address","format":"uri"}}}""";
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        var source = new JsonObject
        {
            ["$schema"] = DefinitionAuthoringCodec.SchemaId,
            ["identity"] = new JsonObject { ["familyId"] = "69cdb122-41d2-4fab-9b82-7929a889c726", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.service", ["name"] = "Example Service" },
            ["applicationVersions"] = new JsonArray("1.0.0"),
            ["configuration"] = JsonNode.Parse(imported.Configuration.GetRawText()),
            ["outputs"] = new JsonObject { ["config.json"] = new JsonObject { ["format"] = "json" } }
        };
        var definition = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        await Assert.That(definition.Fields[0].Help).IsEqualTo("Server address; Target schema format annotation: uri");
        await Assert.That(definition.Fields[0].Format).IsNull();
    }

    [Test]
    public async Task ResourceIdentifiersCannotSilentlyChangeLocalReferenceResolution()
    {
        const string schema = """
            {"$id":"https://example.invalid/root.json","type":"object","additionalProperties":false,
            "properties":{"flag":{"$ref":"common.json"}}}
            """;
        var references = new Dictionary<string, ReadOnlyMemory<byte>> { ["common.json"] = Encoding.UTF8.GetBytes("""{"type":"boolean"}""") };
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), references)).Throws<DefinitionSchemaImportException>();
        const string nested = """
            {"type":"object","additionalProperties":false,"$defs":{"flag":{"type":"boolean"}},
            "properties":{"nested":{"$id":"https://example.invalid/nested.json","$ref":"#/$defs/flag"}}}
            """;
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(nested))).Throws<DefinitionSchemaImportException>();
    }

    [Test]
    public async Task ReferencesCannotCrossNestedResourceScopes()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"$defs":{
            "B":{"type":"boolean"},"Outer":{"$id":"nested.json","$defs":{
            "B":{"type":"string"},"Inner":{"$ref":"#/$defs/B"}}}},
            "properties":{"flag":{"$ref":"#/$defs/Outer/$defs/Inner"}}}
            """;
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema))).Throws<DefinitionSchemaImportException>();
    }

    [Test]
    public async Task DefinitionMapNamesAreNotSchemaResourceKeywords()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"$defs":{
            "$id":{"type":"boolean"},"properties":{"type":"boolean"},
            "$defs":{"type":"boolean"},"flag":{"type":"boolean"}},
            "properties":{"flag":{"$ref":"#/$defs/flag"},
            "reserved":{"$ref":"#/$defs/$id"},
            "namedProperties":{"$ref":"#/$defs/properties"},
            "namedDefinitions":{"$ref":"#/$defs/$defs"}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        await Assert.That(imported.Configuration.GetProperty("properties").EnumerateObject().Count()).IsEqualTo(4);
        foreach (var field in imported.Configuration.GetProperty("properties").EnumerateObject())
            await Assert.That(field.Value.GetProperty("type").GetString()).IsEqualTo("boolean");
    }
}
