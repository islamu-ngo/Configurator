namespace Configurator.Core.Tests.Definitions;

using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using System.Text;
using System.Text.Json;

public sealed class DefinitionDiagnosticPathTests
{
    [Test]
    public async Task AuthoringCompilePreservesOriginalJsonSchemaSpansWithoutChangingDigest()
    {
        SetupDefinition definition = DefinitionFixture.Parse(SourceMapSource);
        await Assert.That(definition.SourceMap.TryGetSpan("/configuration/type", out DefinitionSourceSpan span)).IsTrue();
        await Assert.That(span).IsEqualTo(new DefinitionSourceSpan(0, 7, 13, 7, 21));
        using JsonDocument parsed = JsonDocument.Parse(SourceMapSource);
        SetupDefinition compact = DefinitionFixture.Parse(JsonSerializer.Serialize(parsed.RootElement));
        await Assert.That(compact.Digest).IsEqualTo(definition.Digest);
        await Assert.That(compact.CanonicalBytes.ToArray().SequenceEqual(definition.CanonicalBytes.ToArray())).IsTrue();
        await Assert.That(compact.SourceMap.TryGetSpan("/configuration/type", out DefinitionSourceSpan compactSpan)).IsTrue();
        await Assert.That(compactSpan.StartLine).IsEqualTo(1);
    }

    [Test]
    public async Task AuthoringCompilePreservesYamlSchemaSpansFromOriginalEvents()
    {
        SetupDefinition definition = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseYaml(
            Encoding.UTF8.GetBytes("""
            $schema: urn:islamu:configurator:authoring:v2
            identity: {familyId: 0d9f1857-93ad-4b7d-8be1-bd9e7223c587, revision: 1}
            project: {id: example, name: Locations}
            applicationVersions: ["1"]
            configuration:
              type: object
              additionalProperties: false
              properties:
                "0": {type: integer}
            outputs:
              service.json: {format: json}
            """)));
        await Assert.That(definition.SourceMap.TryGetSpan("/configuration/type", out DefinitionSourceSpan span)).IsTrue();
        await Assert.That(span).IsEqualTo(new DefinitionSourceSpan(0, 6, 9, 6, 15));
        await Assert.That(definition.SourceMap.TryGetSpan("/configuration/properties/0/type", out _)).IsTrue();
    }

    [Test]
    public async Task UnsupportedSourceMemberReportsEnclosingSchemaWithoutUnknownCanaries()
    {
        string source = SourceMapSource.Replace("\"type\": \"integer\"",
            "\"type\": \"integer\", \"synthetic-private-key\": \"synthetic-private-value\"", StringComparison.Ordinal);
        SetupDefinitionException? failure = null;
        try { _ = DefinitionFixture.Parse(source); }
        catch (SetupDefinitionException exception) { failure = exception; }
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.SchemaPath).IsEqualTo("/configuration/properties/0");
        await Assert.That(failure.SourceSpan.HasValue).IsTrue();
        await Assert.That(failure.ToString()).DoesNotContain("synthetic-private");
        await Assert.That(JsonSerializer.Serialize(new { failure.Code, failure.SchemaPath, failure.SourceSpan }))
            .DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task BadReferenceReportsKnownSchemaPositionWithoutReferenceValue()
    {
        string source = SourceMapSource.Replace("\"type\": \"integer\"",
            "\"$ref\": \"#/synthetic-private-reference\"", StringComparison.Ordinal);
        SetupDefinitionException? failure = null;
        try { _ = DefinitionFixture.Parse(source); }
        catch (SetupDefinitionException exception) { failure = exception; }
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.SchemaPath).IsEqualTo("/configuration/properties/0");
        await Assert.That(failure.SourceSpan.HasValue).IsTrue();
        await Assert.That(failure.ToString()).DoesNotContain("synthetic-private-reference");
    }

    [Test]
    public async Task SourceMapDoesNotIndexLiteralValuesOrTheirPrivateKeys()
    {
        string source = SourceMapSource.Replace("\"type\": \"object\"",
            "\"type\": \"object\", \"x-configurator\":{\"sensitivity\":\"public\"}", StringComparison.Ordinal)
            .Replace("\"type\": \"integer\"",
            "\"type\": \"object\", \"additionalProperties\":{\"type\":\"string\",\"x-configurator\":{\"sensitivity\":\"public\"}}, \"x-configurator\":{\"sensitivity\":\"public\"}, \"default\": {\"synthetic-private-key\":\"synthetic-private-value\"}",
            StringComparison.Ordinal);
        DefinitionAuthoringDocument document = DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source));
        SetupDefinitionException? failure = null;
        try { _ = DefinitionAuthoringCompiler.Compile(document); }
        catch (SetupDefinitionException exception) { failure = exception; }
        // The descriptive default is public and schema-valid; its value tree is not source-map structure.
        await Assert.That(failure).IsNull();
        SetupDefinition definition = DefinitionAuthoringCompiler.Compile(document);
        await Assert.That(definition.SourceMap.TryGetSpan("/configuration/properties/0/default", out _)).IsTrue();
        await Assert.That(JsonSerializer.Serialize(definition.SourceMap)).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task UnmappedOversizedDeclarationFallsBackWithoutLeakingItsName()
    {
        string privateName = "synthetic-private-key" + new string('x', 65_515);
        string source = SourceMapSource.Replace("\"0\"", JsonSerializer.Serialize(privateName), StringComparison.Ordinal)
            .Replace("\"integer\"", "\"unsupported\"", StringComparison.Ordinal);
        SetupDefinitionException? failure = null;
        try { _ = DefinitionFixture.Parse(source); }
        catch (SetupDefinitionException exception) { failure = exception; }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.SchemaPath).IsEqualTo("/configuration/properties");
        await Assert.That(failure.SourceSpan.HasValue).IsTrue();
        await Assert.That(JsonSerializer.Serialize(new { failure.Code, failure.SchemaPath, failure.SourceSpan }))
            .DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task PublicSchemaPointersEscapeDeclaredNamesWithoutFollowingLiteralTrees()
    {
        string source = SourceMapSource.Replace("\"0\"", "\"a/~.0\"", StringComparison.Ordinal);
        DefinitionAuthoringDocument document = DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source));
        await Assert.That(document.SourceMap.TryGetSpan("/configuration/properties/a~1~0.0/type", out DefinitionSourceSpan span)).IsTrue();
        await Assert.That(span).IsEqualTo(new DefinitionSourceSpan(0, 10, 26, 10, 35));
        await Assert.That(document.SourceMap.TryGetSpan("/configuration/properties/a/~.0/type", out _)).IsFalse();
    }

    [Test]
    public async Task TypedCorrectionCoordinatesSeparateNumericPropertiesRowsAndMapEntries()
    {
        DefinitionLocationSegment[] segments =
        [
            DefinitionLocationSegment.Property("0"),
            DefinitionLocationSegment.ArrayRow(0),
            DefinitionLocationSegment.MapEntry(1)
        ];
        var location = new DefinitionCorrectionLocation(7, segments);
        var diagnostic = new DefinitionDiagnostic("value-invalid", "options", location);
        segments[0] = DefinitionLocationSegment.Property("changed");
        await Assert.That(diagnostic.Location!.Segments[0].Kind).IsEqualTo(DefinitionLocationSegmentKind.Property);
        await Assert.That(diagnostic.Location.Segments[0].PropertyName).IsEqualTo("0");
        await Assert.That(diagnostic.Location.Segments[1].Kind).IsEqualTo(DefinitionLocationSegmentKind.ArrayRow);
        await Assert.That(diagnostic.Location.Segments[2].Kind).IsEqualTo(DefinitionLocationSegmentKind.MapEntry);
        await Assert.That(diagnostic.Location.InputRevision).IsEqualTo(7);
        await Assert.That(diagnostic).IsNotEqualTo(new DefinitionDiagnostic("value-invalid", "options",
            new DefinitionCorrectionLocation(8, location.Segments)));
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["0", "0", "@entry", "1"])).IsTrue();
    }

    [Test]
    public async Task ContainerNodeCannotDemoteNestedSecretSensitivity()
    {
        System.Text.Json.Nodes.JsonObject source = System.Text.Json.Nodes.JsonNode.Parse(Source)!.AsObject();
        source["configuration"]!["properties"]!["options"]!["properties"]!["dotted.name"]!["x-configurator"]!["sensitivity"] = "secret";
        DefinitionValidationResult result = DefinitionValidator.Validate(DefinitionFixture.Parse(source.ToJsonString()),
            DefinitionFixture.Answers("""{"rows":[],"options":{"dotted.name":1}}"""));

        await Assert.That(result.Nodes.Single(node => node.FieldId == "options" && node.RelativePath.Count == 0).Sensitivity)
            .IsEqualTo(SetupFieldSensitivity.Secret);
    }

    [Test]
    public async Task DiagnosticEqualityUsesLiteralSegmentValues()
    {
        var first = new DefinitionDiagnostic("value-invalid", "rows", ["1", "name"]);
        var equal = new DefinitionDiagnostic("value-invalid", "rows", ["1", "name"]);
        var distinct = new DefinitionDiagnostic("value-invalid", "rows", ["1.name"]);
        await Assert.That(first).IsEqualTo(equal);
        await Assert.That(first.GetHashCode()).IsEqualTo(equal.GetHashCode());
        await Assert.That(first).IsNotEqualTo(distinct);
    }

    [Test]
    public async Task DuplicateRowKeepsRootIdentityAndIdentifiesItsDeclaredKey()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[{"name":"synthetic-private-row"},{"name":"synthetic-private-row"}],"options":{"dotted.name":1}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Code).IsEqualTo("value-duplicate");
        await Assert.That(diagnostic.FieldId).IsEqualTo("rows");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["1", "name"])).IsTrue();
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private-row");
    }

    [Test]
    public async Task LiteralDottedPropertyIsOneCorrectionSegment()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[],"options":{"dotted.name":"synthetic-private-value"}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("options");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["dotted.name"])).IsTrue();
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private-value");
    }

    [Test]
    public async Task UnknownAnswerPropertiesNeverBecomeDiagnosticPathSegments()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[],"options":{"dotted.name":1,"synthetic-private-key":"synthetic-private-value"}}
            """));

        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("options");
        await Assert.That(diagnostic.RelativePath.Count).IsEqualTo(0);
        await Assert.That(diagnostic.ToString()).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task RequiredNestedEmptyStringCannotClaimReadiness()
    {
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(),
            DefinitionFixture.Answers("""
            {"rows":[{"name":""}],"options":{"dotted.name":1}}
            """));

        await Assert.That(result.IsReady).IsFalse();
        DefinitionDiagnostic diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.FieldId).IsEqualTo("rows");
        await Assert.That(diagnostic.RelativePath.SequenceEqual(["0", "name"])).IsTrue();
        await Assert.That(diagnostic.Code).IsEqualTo("value-invalid");
    }

    [Test]
    public async Task DiagnosticSnapshotsCallerOwnedPathSegments()
    {
        string[] source = ["1", "name"];
        var diagnostic = new DefinitionDiagnostic("value-invalid", "rows", source);
        source[0] = "changed";

        await Assert.That(diagnostic.RelativePath.SequenceEqual(["1", "name"])).IsTrue();
    }

    private static SetupDefinition Definition() => DefinitionFixture.Parse(Source);

    private const string SourceMapSource = """
    {
      "$schema": "urn:islamu:configurator:authoring:v2",
      "identity": {"familyId":"0d9f1857-93ad-4b7d-8be1-bd9e7223c587","revision":1},
      "project": {"id":"example","name":"Locations"},
      "applicationVersions": ["1"],
      "configuration": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "0": { "type": "integer" }
        }
      },
      "outputs": {"service.json":{"format":"json"}}
    }
    """;

    private const string Source = """
    {"$schema":"urn:islamu:configurator:authoring:v2",
     "identity":{"familyId":"0d9f1857-93ad-4b7d-8be1-bd9e7223c587","revision":1},
     "project":{"id":"example","name":"Paths","publisher":"example"},
     "applicationVersions":["1"],"entryPoints":["setup"],
     "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
      "properties":{
       "rows":{"type":"array","maxItems":3,"title":"Rows","x-configurator":{"sensitivity":"public","detail":"advanced","uniqueBy":"/name"},
         "items":{"type":"object","additionalProperties":false,"required":["name"],"x-configurator":{"sensitivity":"public"},
          "properties":{"name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","notBlank":true}}}}},
       "options":{"type":"object","title":"Options","additionalProperties":false,"required":["dotted.name"],
         "x-configurator":{"sensitivity":"public"},"properties":{
          "dotted.name":{"type":"integer","title":"Number","x-configurator":{"sensitivity":"public"}}}}}},
     "outputs":{"service.json":{"id":"config","format":"json",
       "targetUpdateMode":"create","restartRequirement":"process","projection":{"op":"literal","value":{}}}}}
    """;
}
