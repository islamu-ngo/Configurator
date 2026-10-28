using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests;

public sealed class DefinitionSchemaImporterTests
{
    [Test]
    public async Task NullableProviderDomainChangeRequiresExplicitAcknowledgment()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{"provider":{
              "type":["object","null"],"oneOf":[
                {"type":"object","additionalProperties":false,"required":["kind"],"properties":{
                  "kind":{"type":"string","const":"local"}}},
                {"type":"object","additionalProperties":false,"required":["kind"],"properties":{
                  "kind":{"type":"string","const":"remote"}}}]}}}
            """;
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema)))
            .Throws<DefinitionSchemaImportException>();
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema),
            acceptBoundedSemantics: true);
        await Assert.That(imported.Transformations.Contains(
            new("converted", "schema-nullable-variant-domain", "/properties/provider/oneOf", 0))).IsTrue();
    }

    [Test]
    public async Task UnusedDefinitionsCannotHideUnsupportedAssertionsFromTheTransformationLedger()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{},"$defs":{
              "unused":{"type":"string","pattern":"synthetic-private-pattern","default":"synthetic-private-default"}}}
            """;
        try
        {
            DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true);
            throw new InvalidOperationException("Expected unsupported assertion refusal.");
        }
        catch (DefinitionSchemaImportException exception)
        {
            await Assert.That(exception.Transformations.Contains(
                new("unsupported", "schema-keyword-unsupported", "/$defs/unused/pattern", 0))).IsTrue();
            await Assert.That(System.Text.Json.JsonSerializer.Serialize(exception.Transformations))
                .DoesNotContain("synthetic-private");
        }
    }

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
        await Assert.That(imported.Transformations.Where(item => item.Code == "schema-numeric-bounded-domain").ToArray())
            .IsEquivalentTo([new DefinitionSchemaTransformation("converted", "schema-numeric-bounded-domain", "/properties/count/type", 0)]);
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
            """{"type":["string","boolean","null"]}""",
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
        await Assert.That(definition.Fields[0].Help).IsEqualTo("Server address");
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("address").GetProperty("format").GetString()).IsEqualTo("uri");
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("address").TryGetProperty("x-configurator", out var extension)
            && extension.TryGetProperty("assertFormat", out _)).IsFalse();
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

    [Test]
    public async Task RecursiveScalarItemsTypedMapsAndNullableValuesRetainConstraints()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{
              "flags":{"type":"array","uniqueItems":true,"items":{"type":"boolean","enum":[true,false]}},
              "names":{"type":"array","items":{"type":["string","null"],"minLength":2,"enum":["ab","cd",null]}},
              "numbers":{"type":"object","minProperties":1,"maxProperties":4,
                "additionalProperties":{"type":"array","items":{"type":["number","null"],
                  "exclusiveMinimum":0,"exclusiveMaximum":10,"multipleOf":0.25}}},
              "nothing":{"type":"array","items":{"type":"null"}}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true);
        var properties = imported.Configuration.GetProperty("properties");
        await Assert.That(properties.GetProperty("flags").GetProperty("uniqueItems").GetBoolean()).IsTrue();
        await Assert.That(properties.GetProperty("names").GetProperty("items").GetProperty("minLength").GetInt32()).IsEqualTo(2);
        await Assert.That(properties.GetProperty("nothing").GetProperty("items").GetProperty("enum")[0].ValueKind)
            .IsEqualTo(System.Text.Json.JsonValueKind.Null);
        await Assert.That(imported.Transformations.Any(entry => entry.Code == "schema-null-type-converted"
            && entry.Classification == "converted")).IsTrue();
        var item = properties.GetProperty("numbers").GetProperty("additionalProperties").GetProperty("items");
        await Assert.That(item.GetProperty("multipleOf").GetDecimal()).IsEqualTo(0.25m);
        await Assert.That(item.GetProperty("exclusiveMaximum").GetInt32()).IsEqualTo(10);
        await Assert.That(imported.Transformations.Any(entry => entry.Code == "schema-numeric-bounded-domain"
            && entry.Location == "/properties/numbers/additionalProperties/items/type")).IsTrue();
    }

    [Test]
    public async Task DiscriminatedClosedObjectBranchesUseCoreAdmission()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{"provider":{"type":"object","oneOf":[
              {"type":"object","additionalProperties":false,"required":["kind","host"],"properties":{
                "kind":{"type":"string","const":"local"},"host":{"type":"string"}}},
              {"type":"object","additionalProperties":false,"required":["kind","token"],"properties":{
                "kind":{"type":"string","const":"remote"},"token":{"type":"string","writeOnly":true}}}
            ]}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        await Assert.That(imported.Configuration.GetProperty("properties").GetProperty("provider").GetProperty("oneOf").GetArrayLength()).IsEqualTo(2);
        string ambiguous = schema.Replace("\"const\":\"remote\"", "\"const\":\"local\"", StringComparison.Ordinal);
        await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(ambiguous), acceptBoundedSemantics: true))
            .Throws<DefinitionSchemaImportException>();
    }

    [Test]
    public async Task TransformationsDiscardPrivateValuesAndRetainAnnotationsWithoutInitialization()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{"a/b~c":{"type":"string",
              "default":"synthetic-default-canary","examples":["synthetic-example-canary"],
              "format":"uri","readOnly":true,"deprecated":true}}}
            """;
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
        string output = imported.Configuration.GetRawText();
        await Assert.That(output.Contains("synthetic-", StringComparison.Ordinal)).IsFalse();
        await Assert.That(output.Contains("initialValue", StringComparison.Ordinal)).IsFalse();
        await Assert.That(imported.Transformations.Contains(new("discarded", "schema-default-discarded", "/properties/a~1b~0c/default", 0))).IsTrue();
        await Assert.That(imported.Transformations.Contains(new("discarded", "schema-examples-discarded", "/properties/a~1b~0c/examples", 0))).IsTrue();
        await Assert.That(imported.Transformations.Contains(new("advisory", "schema-format-advisory", "/properties/a~1b~0c/format", 0))).IsTrue();
        var field = imported.Configuration.GetProperty("properties").GetProperty("a/b~c");
        await Assert.That(field.GetProperty("format").GetString()).IsEqualTo("uri");
        await Assert.That(field.GetProperty("readOnly").GetBoolean()).IsTrue();
        await Assert.That(field.GetProperty("deprecated").GetBoolean()).IsTrue();
        await Assert.That(imported.Transformations is ICollection<DefinitionSchemaTransformation> { IsReadOnly: true }).IsTrue();
    }

    [Test]
    public async Task UnsupportedAssertionsReportSafeLocationsEvenWhenAcknowledged()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{"entry":{"type":"string",
              "pattern":"synthetic-pattern-canary","default":"synthetic-default-canary"}}}
            """;
        try
        {
            _ = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true);
            Assert.Fail("Unsupported assertions must reject.");
        }
        catch (DefinitionSchemaImportException exception)
        {
            await Assert.That(exception.Transformations.Contains(new("unsupported", "schema-keyword-unsupported", "/properties/entry/pattern", 0))).IsTrue();
            await Assert.That(exception.Transformations.Any(item => item.Classification == "discarded")).IsTrue();
            await Assert.That(exception.ToString().Contains("synthetic-", StringComparison.Ordinal)).IsFalse();
            await Assert.That(exception.Transformations.Any(item => item.ToString().Contains("synthetic-", StringComparison.Ordinal))).IsFalse();
        }
    }

    [Test]
    public async Task SuppliedDocumentOrdinalsAndNestedSecretInheritanceDoNotExposeReferenceNames()
    {
        const string schema = """
            {"type":"object","additionalProperties":false,"properties":{"map":{"$ref":"private-directory/common.json#/$defs/map","writeOnly":true}}}
            """;
        var references = new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["private-directory/common.json"] = Encoding.UTF8.GetBytes("""
                {"$defs":{"map":{"type":"object","additionalProperties":{"type":"array","items":{"type":"string",
                  "writeOnly":false,"default":"synthetic-default-canary"}}}}}
                """)
        };
        DefinitionSchemaImportResult imported = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), references);
        var item = imported.Configuration.GetProperty("properties").GetProperty("map").GetProperty("additionalProperties").GetProperty("items");
        await Assert.That(item.GetProperty("x-configurator").GetProperty("sensitivity").GetString()).IsEqualTo("secret");
        await Assert.That(imported.Transformations.Contains(new("discarded", "schema-default-discarded", "/$defs/map/additionalProperties/items/default", 1))).IsTrue();
        await Assert.That(imported.Transformations.Any(entry => entry.ToString().Contains("private-directory", StringComparison.Ordinal))).IsFalse();
        await Assert.That(item.TryGetProperty("default", out _)).IsFalse();
    }

    [Test]
    public async Task CoreSemanticRejectionsCannotBeAcknowledgedAway()
    {
        string[] fields =
        [
            """{"type":"number","multipleOf":0}""",
            """{"type":"number","exclusiveMinimum":5,"maximum":4}""",
            """{"type":"array","items":{"type":"string","enum":["a",true]}}""",
            """{"type":"object","additionalProperties":{"type":"string","minLength":-1}}""",
            """
            {"type":"object","oneOf":[
                {"type":"object","additionalProperties":false,"properties":{"kind":{"type":"string","const":"a"}}},
                {"type":"object","additionalProperties":false,"properties":{"kind":{"type":"string","const":"b"}}}]}
            """
        ];
        foreach (string field in fields)
        {
            string schema = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"value\":" + field + "}}";
            await Assert.That(() => DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema), acceptBoundedSemantics: true))
                .Throws<DefinitionSchemaImportException>();
        }
    }

    [Test]
    public async Task NumericAcknowledgmentFailureCarriesImmutableLocationLedger()
    {
        const string schema = """{"type":"object","additionalProperties":false,"properties":{"count":{"type":"integer"}}}""";
        try
        {
            _ = DefinitionSchemaImporter.Import(Encoding.UTF8.GetBytes(schema));
            Assert.Fail("Numeric profile conversion requires explicit acknowledgment.");
        }
        catch (DefinitionSchemaImportException exception)
        {
            await Assert.That(exception.Code).IsEqualTo("schema-bounded-semantics-required");
            await Assert.That(exception.Transformations.Contains(new("converted", "schema-numeric-bounded-domain", "/properties/count/type", 0))).IsTrue();
            await Assert.That(exception.Transformations is ICollection<DefinitionSchemaTransformation> { IsReadOnly: true }).IsTrue();
            await Assert.That(exception.Transformations.Any(item => item.Classification == "unsupported")).IsFalse();
        }
    }
}
