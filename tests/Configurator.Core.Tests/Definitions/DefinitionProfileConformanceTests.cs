using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionProfileConformanceTests
{
    [Test]
    public async Task ItemApplicabilityUsesOnlyItsRowAndInvalidControllersRemainUnknown()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"array","items":{"type":"object","additionalProperties":false,
             "properties":{
              "kind":{"type":["string","null"],"enum":["local","remote",null]},
              "endpoint":{"type":"string","x-configurator":{"applicableWhen":{
                "op":"equals","reference":{"scope":"item","path":"/kind"},"value":"remote"}}}},
             "required":["endpoint"]}}
            """);
        DefinitionValidationResult valid = DefinitionValidator.Validate(definition, DefinitionFixture.Answers("""
            {"value":[{"kind":"local","endpoint":12},{"kind":"remote","endpoint":"offline"}]}
            """));
        await Assert.That(valid.IsReady).IsTrue();
        await Assert.That(valid.TryGetEffectiveValue(DefinitionPath.Parse("/value/0/endpoint"), out _)).IsFalse();
        DefinitionValidationResult missing = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{"kind":"local"},{"kind":"remote"}]}"""));
        await Assert.That(missing.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required" &&
            diagnostic.RelativePath.SequenceEqual(["1", "endpoint"]))).IsTrue();
        DefinitionValidationResult invalid = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{"kind":42}]}"""));
        await Assert.That(invalid.IsReady).IsFalse();
        await Assert.That(invalid.Diagnostics.Any(diagnostic => diagnostic.RelativePath.Contains("endpoint"))).IsFalse();
    }

    [Test]
    public async Task RowRelationshipsAllowMutualRequirementsAndConditionalElse()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"array","items":{"type":"object","additionalProperties":false,
             "properties":{"kind":{"type":"string"},"username":{"type":"string"},"password":{"type":"string"}},
             "dependentRequired":{"username":["password"],"password":["username"]},
             "if":{"properties":{"kind":{"const":"remote"}},"required":["kind"]},
             "then":{"required":["username"]},"else":{"required":["kind"]}}}
            """);
        foreach (string rows in new[] { """[{"kind":"local"}]""",
            """[{"kind":"remote","username":"synthetic-user","password":"synthetic-secret"}]""" })
            await Assert.That(DefinitionValidator.Validate(definition,
                DefinitionFixture.Answers("{\"value\":" + rows + "}")).IsReady).IsTrue();
        DefinitionValidationResult result = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{"kind":"local","username":"synthetic-user"}]}"""));
        await Assert.That(result.IsReady).IsFalse();
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required" &&
            diagnostic.RelativePath.SequenceEqual(["0", "password"]))).IsTrue();
    }

    [Test]
    public async Task RowApplicabilityCyclesRejectWithoutEchoingNames()
    {
        await Assert.That(() => WithValueSchema("""
            {"type":"array","items":{"type":"object","additionalProperties":false,"properties":{
             "first":{"type":"string","x-configurator":{"applicableWhen":{
              "op":"exists","reference":{"scope":"item","path":"/second"}}}},
             "second":{"type":"string","x-configurator":{"applicableWhen":{
              "op":"exists","reference":{"scope":"item","path":"/first"}}}}}}}
            """)).Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task CorrectionsDistinguishPrivateMapEntriesRowsAndNumericDeclaredNames()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"object","additionalProperties":{"type":"array","items":{
             "type":"object","additionalProperties":false,"properties":{"0":{"type":"integer"}}}}}
            """);
        SetupAnswerSnapshot answers = DefinitionFixture.Answers("""
            {"value":{"synthetic-private-key":[{"0":"synthetic-private-answer"}]}}
            """);
        DefinitionDiagnostic diagnostic = DefinitionValidator.Validate(definition, answers).Diagnostics.Single();
        await Assert.That(diagnostic.Location is not null).IsTrue();
        await Assert.That(diagnostic.Location!.InputRevision).IsEqualTo(answers.InputRevision);
        await Assert.That(diagnostic.Location.Segments.Select(segment => segment.Kind).SequenceEqual([
            DefinitionLocationSegmentKind.MapEntry, DefinitionLocationSegmentKind.ArrayRow,
            DefinitionLocationSegmentKind.Property])).IsTrue();
        await Assert.That(diagnostic.Location.Segments[2].PropertyName).IsEqualTo("0");
        await Assert.That(JsonSerializer.Serialize(diagnostic)).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task MissingAndNullItemControllersHaveDifferentPresenceAndDeferredControllersAreUnknown()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"array","items":{"type":"object","additionalProperties":false,"properties":{
             "controller":{"type":["string","null"],"x-configurator":{"sensitivity":"secret"}},
             "dependent":{"type":"string","x-configurator":{"applicableWhen":{
              "op":"exists","reference":{"scope":"item","path":"/controller"}}}}},"required":["dependent"]}}
            """);
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{}]}""")).IsReady).IsTrue();
        DefinitionValidationResult explicitNull = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{"controller":null}]}"""));
        await Assert.That(explicitNull.Diagnostics.Any(diagnostic => diagnostic.RelativePath.SequenceEqual(["0", "dependent"])))
            .IsTrue();
        DefinitionValidationResult draft = DefinitionValidator.ValidateDraft(definition,
            DefinitionFixture.Answers("""{"value":[{}]}"""));
        await Assert.That(draft.Diagnostics.Count).IsEqualTo(0);
        await Assert.That(draft.Nodes.Any(node => node.RelativePath.SequenceEqual(["0", "dependent"]) && node.IsActive)).IsFalse();
    }

    [Test]
    public async Task DefiniteMissingWorkInOneRowSurvivesInvalidControlInAnother()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"array","items":{"type":"object","additionalProperties":false,"required":["dependent"],
             "properties":{"controller":{"type":"boolean"},"dependent":{"type":"string",
             "x-configurator":{"applicableWhen":{"op":"equals",
             "reference":{"scope":"item","path":"/controller"},"value":true}}}}}}
            """);
        DefinitionValidationResult result = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{"controller":true},{"controller":"synthetic-invalid"}]}"""));
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required" &&
            diagnostic.RelativePath.SequenceEqual(["0", "dependent"]))).IsTrue();
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.RelativePath.SequenceEqual(["1", "dependent"]))).IsFalse();
    }

    [Test]
    public async Task NestedRootControllersAreResolvedBeforeRowApplicability()
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"]!["properties"] = JsonNode.Parse("""
            {"value":{"type":"array","items":{"type":"object","additionalProperties":false,
             "required":["endpoint"],"properties":{"endpoint":{"type":"string",
             "x-configurator":{"applicableWhen":{"op":"equals",
             "reference":{"scope":"root","path":"/zcontroller"},"value":true}}}}}},
             "zcontroller":{"type":"boolean"}}
            """);
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        DefinitionValidationResult result = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{}],"zcontroller":true}"""));
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required" &&
            diagnostic.RelativePath.SequenceEqual(["0", "endpoint"]))).IsTrue();
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[{}],"zcontroller":false}""")).IsReady).IsTrue();
    }

    [Test]
    public async Task TypedMapsValidateRecursiveItemsWithoutPublishingPrivateEntryNames()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"object","additionalProperties":{"type":"array","items":{
              "type":"string","enum":["one","two"],"x-configurator":{"sensitivity":"public"}},
              "x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public"}}
            """);
        using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id,
            DefinitionFixture.Answers("""{"value":{"A":["one"],"a":["two"]}}""")).Artifact;
        await Assert.That(artifact is not null).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span))
            .IsEqualTo("""{"value":{"A":["one"],"a":["two"]}}""");
        DefinitionValidationResult invalid = DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":{"synthetic-private-entry":["synthetic-private-answer"]}}"""));
        await Assert.That(invalid.IsReady).IsFalse();
        string diagnostic = JsonSerializer.Serialize(invalid.Diagnostics);
        await Assert.That(diagnostic).DoesNotContain("synthetic-private-entry");
        await Assert.That(diagnostic).DoesNotContain("synthetic-private-answer");
    }

    [Test]
    public async Task NullableConstrainedItemsPreserveNullAndEnforceExactAssertions()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"array","uniqueItems":true,"items":{"type":["number","null"],
              "exclusiveMinimum":0,"exclusiveMaximum":3,"multipleOf":0.5,
              "x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public"}}
            """);
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":[null,0.5,2]}""")).IsReady).IsTrue();
        foreach (string values in new[] { "[0]", "[3]", "[0.1]", "[1,1]", "[null,null]" })
            await Assert.That(DefinitionValidator.Validate(definition,
                DefinitionFixture.Answers("{\"value\":" + values + "}")).IsReady).IsFalse();
    }

    [Test]
    public async Task DiscriminatedProvidersSelectOneClosedBranchAndRetainNullablePresence()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":["object","null"],"x-configurator":{"sensitivity":"public"},"oneOf":[
              {"type":"object","additionalProperties":false,"required":["kind","endpoint"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "kind":{"type":"string","const":"remote","x-configurator":{"sensitivity":"public"}},
                 "endpoint":{"type":"string","minLength":1,"x-configurator":{"sensitivity":"public"}}}},
              {"type":"object","additionalProperties":false,"required":["kind","path"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "kind":{"type":"string","const":"local","x-configurator":{"sensitivity":"public"}},
                 "path":{"type":"string","minLength":1,"x-configurator":{"sensitivity":"public"}}}}]}
            """);
        foreach (string value in new[] { "null", """{"kind":"remote","endpoint":"offline"}""", """{"kind":"local","path":"data"}""" })
        {
            string answers = "{\"value\":" + value + "}";
            using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id,
                DefinitionFixture.Answers(answers)).Artifact;
            await Assert.That(artifact is not null).IsTrue();
            using JsonDocument actual = JsonDocument.Parse(artifact!.Bytes);
            using JsonDocument expected = JsonDocument.Parse(answers);
            await Assert.That(JsonElement.DeepEquals(actual.RootElement, expected.RootElement)).IsTrue();
        }
        foreach (string value in new[] { """{"kind":"missing"}""", """{"kind":"remote"}""",
            """{"kind":"local","path":"data","endpoint":"extra"}""" })
            await Assert.That(DefinitionValidator.Validate(definition,
                DefinitionFixture.Answers("{\"value\":" + value + "}")).IsReady).IsFalse();
    }

    [Test]
    public async Task LocalReferencesExpandUnderOneConfigurationRootAndCyclesReject()
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
             "$defs":{"text":{"type":"string","minLength":2,"x-configurator":{"sensitivity":"public"}}},
             "properties":{"value":{"type":"array","items":{"$ref":"#/$defs/text"},
               "x-configurator":{"sensitivity":"public"}}}}
            """);
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":["ok"]}""")).IsReady).IsTrue();
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"value":["x"]}""")).IsReady).IsFalse();
        source["configuration"]!["$defs"]!["text"] = JsonNode.Parse("""{"$ref":"#/$defs/text"}""");
        await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task AssertedFormatsAreOptInAndNeverGrantNetworkOrCredentialAuthority()
    {
        SetupDefinition descriptive = WithValueSchema("""
            {"type":"string","format":"hostname","x-configurator":{"sensitivity":"public"}}
            """);
        await Assert.That(DefinitionValidator.Validate(descriptive,
            DefinitionFixture.Answers("""{"value":"not a hostname"}""")).IsReady).IsTrue();
        foreach ((string format, string valid, string invalid) in new[]
        {
            ("hostname", "service.example", "-service.example"),
            ("email-address", "operator@example.invalid", "a..b@example.invalid"),
            ("uri", "urn:example:offline", "not a URI"),
            ("http-url", "https://example.invalid/docs#section", "https://user:password@example.invalid/")
        })
        {
            SetupDefinition asserted = WithValueSchema("{\"type\":\"string\",\"format\":\"" + format +
                "\",\"x-configurator\":{\"sensitivity\":\"public\",\"assertFormat\":true}}");
            await Assert.That(DefinitionValidator.Validate(asserted,
                DefinitionFixture.Answers("{\"value\":" + JsonSerializer.Serialize(valid) + "}")).IsReady).IsTrue();
            await Assert.That(DefinitionValidator.Validate(asserted,
                DefinitionFixture.Answers("{\"value\":" + JsonSerializer.Serialize(invalid) + "}")).IsReady).IsFalse();
        }
    }

    private static SetupDefinition WithValueSchema(string schema)
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"]!["properties"] = new JsonObject { ["value"] = JsonNode.Parse(schema) };
        return SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    [Test]
    public async Task ConfigurationRootMapsAndBoundsUseTheSameRecursiveOwner()
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"] = JsonNode.Parse("""
            {"type":"object","minProperties":1,"maxProperties":2,"x-configurator":{"sensitivity":"public"},
             "additionalProperties":{"type":"integer","minimum":0,"x-configurator":{"sensitivity":"public"}}}
            """);
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id,
            DefinitionFixture.Answers("""{"A":1,"a":2}""")).Artifact;
        await Assert.That(artifact is not null).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("""{"A":1,"a":2}""");
        foreach (string answers in new[] { "{}", """{"A":1,"a":2,"b":3}""", """{"synthetic-private-entry":-1}""" })
        {
            DefinitionValidationResult result = DefinitionValidator.Validate(definition, DefinitionFixture.Answers(answers));
            await Assert.That(result.IsReady).IsFalse();
            await Assert.That(JsonSerializer.Serialize(result.Diagnostics)).DoesNotContain("synthetic-private-entry");
        }
    }

    [Test]
    public async Task ConfigurationRootProvidersValidateAndComposeTheirSelectedProperties()
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"] = JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
              {"type":"object","additionalProperties":false,"required":["kind","amount"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"count","x-configurator":{"sensitivity":"public"}},
                "amount":{"type":"integer","minimum":1,"x-configurator":{"sensitivity":"public"}}}},
              {"type":"object","additionalProperties":false,"required":["kind","name"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"name","x-configurator":{"sensitivity":"public"}},
                "name":{"type":"string","minLength":1,"x-configurator":{"sensitivity":"public"}}}}]}
            """);
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        foreach (string answers in new[] { """{"amount":2,"kind":"count"}""", """{"kind":"name","name":"sample"}""" })
        {
            using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id,
                DefinitionFixture.Answers(answers)).Artifact;
            await Assert.That(artifact is not null).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo(answers);
        }
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"kind":"count","amount":0}""")).IsReady).IsFalse();
        await Assert.That(DefinitionValidator.Validate(definition,
            DefinitionFixture.Answers("""{"kind":"unknown"}""")).IsReady).IsFalse();
    }

    [Test]
    public async Task RecursivePrivacyAndReferenceExpansionCannotLaunderPublicValues()
    {
        await Assert.That(() => WithValueSchema("""
            {"type":"object","additionalProperties":{"type":"string","x-configurator":{
             "sensitivity":"secret","initialValue":"synthetic-private-initial"}},
             "x-configurator":{"sensitivity":"public"}}
            """)).Throws<SetupDefinitionException>();
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"]!["$defs"] = JsonNode.Parse("""
            {"secret":{"type":"string","writeOnly":true,"x-configurator":{"sensitivity":"secret"}}}
            """);
        source["configuration"]!["properties"] = JsonNode.Parse("""
            {"value":{"$ref":"#/$defs/secret","writeOnly":false,"x-configurator":{
             "sensitivity":"public","suggestedValue":"synthetic-private-suggestion"}}}
            """);
        await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
            .Throws<SetupDefinitionException>();
        source["configuration"]!["$defs"] = JsonNode.Parse(
            """{"unused":{"type":"string","executable":"synthetic-executable"}}""");
        source["configuration"]!["properties"] = new JsonObject();
        await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ReferenceAmplificationIsBoundedBeforeAnExpandedSchemaEscapesAdmission()
    {
        JsonNode source = JsonNode.Parse(SourceV2)!;
        source["configuration"]!["$defs"] = new JsonObject
        {
            ["text"] = new JsonObject
            {
                ["type"] = "string", ["description"] = new string('x', 4096),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
            }
        };
        var properties = new JsonObject();
        for (int index = 0; index < 300; index++)
            properties["field" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                new JsonObject { ["$ref"] = "#/$defs/text" };
        source["configuration"]!["properties"] = properties;
        await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task DeferredMapSecretsNeverExposePrivateKeysThroughCompletionMetadata()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
              "type":"object","additionalProperties":false,"required":["label","credential"],
              "x-configurator":{"sensitivity":"public"},"properties":{
                "label":{"type":"string","x-configurator":{"sensitivity":"public"}},
                "credential":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}}
            """);
        using DefinitionDraftArtifactResult result = DefinitionDraftArtifactComposer.Compose(definition, definition.Artifacts[0].Id,
            DefinitionFixture.Answers("""{"value":{"synthetic-private-key":{"label":"public"}}}"""));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Completions.Count).IsEqualTo(1);
        await Assert.That(result.Completions[0].FieldPath).IsEqualTo("/value/@entry/0/credential");
        await Assert.That(JsonSerializer.Serialize(result.Completions)).DoesNotContain("synthetic-private-key");
        await Assert.That(JsonSerializer.Serialize(result.Diagnostics)).DoesNotContain("synthetic-private-key");
        await Assert.That(result.IsDraft).IsTrue();
    }

    [Test]
    public async Task DynamicEntryPointerOverflowRejectsWithBoundedValueFreeDiagnostics()
    {
        SetupDefinition definition = WithValueSchema("""
            {"type":"object","additionalProperties":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "x-configurator":{"sensitivity":"public"}}
            """);
        string key = "synthetic-private-key" + new string('~', 32_768);
        using JsonDocument answers = JsonDocument.Parse(JsonSerializer.Serialize(
            new Dictionary<string, Dictionary<string, string>> { ["value"] = new() { [key] = "synthetic-private-value" } }));
        var snapshot = new SetupAnswerSnapshot(1, answers.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal));
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id, snapshot);
        await Assert.That(result.Artifact).IsNull();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("validation-limit");
        await Assert.That(JsonSerializer.Serialize(result.Diagnostics)).DoesNotContain("synthetic-private");
    }

    private const string SourceV2 = """
        {
          "$schema":"urn:islamu:configurator:authoring:v2",
          "identity":{"familyId":"23808653-d359-4a11-8f28-0753b7825338","revision":1},
          "project":{"id":"synthetic.profile","name":"Synthetic profile"},
          "applicationVersions":["1.0"],
          "configuration":{
            "type":"object","additionalProperties":false,
            "x-configurator":{"sensitivity":"public"},
            "properties":{
              "mode":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"on"}},
              "settings":{
                "type":"object","additionalProperties":false,
                "x-configurator":{"sensitivity":"public"},
                "properties":{
                  "port":{"type":"integer","x-configurator":{"sensitivity":"public","initialValue":8080}}
                }
              },
              "text":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "nullable":{"type":["string","null"],"x-configurator":{"sensitivity":"public"}},
              "A":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
              "a":{"type":"integer","x-configurator":{"sensitivity":"public"}}
            }
          },
          "outputs":{"config.json":{"format":"json"}}
        }
        """;

    [Test]
    public async Task V2ResolvesInitializationOnlyInsidePresentContainers()
    {
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(SourceV2));
        foreach ((string values, string expected) in new[]
        {
            ("{}", """{"mode":"on"}"""),
            ("""{"settings":{}}""", """{"mode":"on","settings":{"port":8080}}"""),
            ("""{"A":false,"a":0,"text":"","nullable":null}""",
                """{"A":false,"a":0,"mode":"on","nullable":null,"text":""}""")
        })
        {
            SetupAnswerSnapshot snapshot = SetupAnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(
                """{"answerContractVersion":1,"values":""" + values + ""","omitted":[]}"""));
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition,
                definition.Artifacts[0].Id, snapshot);
            using DefinitionArtifact? artifact = result.Artifact;
            await Assert.That(artifact is not null).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task V2OmissionSuppressesInitializationAndResetRestoresEligibility()
    {
        SetupDefinition definition = SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(SourceV2));
        foreach ((string omissions, string expected) in new[]
        {
            ("""["/mode"]""", "{}"),
            ("[]", """{"mode":"on"}""")
        })
        {
            SetupAnswerSnapshot snapshot = SetupAnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(
                """{"answerContractVersion":1,"values":{},"omitted":""" + omissions + "}"), inputRevision: 7);
            DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition,
                definition.Artifacts[0].Id, snapshot);
            using DefinitionArtifact? artifact = result.Artifact;
            await Assert.That(artifact is not null).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo(expected);
            await Assert.That(artifact.InputRevision).IsEqualTo(7L);
        }
    }

    [Test]
    public async Task PrivateAnswerEnvelopesRejectAmbiguityAndUnknownContracts()
    {
        foreach (string envelope in new[]
        {
            """{"answerContractVersion":0,"values":{},"omitted":[]}""",
            """{"answerContractVersion":1,"answers":{},"omitted":[]}""",
            """{"answerContractVersion":1,"values":{"synthetic-private-key":1},"omitted":["/synthetic-private-key"]}""",
            """{"answerContractVersion":1,"values":{},"omitted":["/a","/a/b"]}""",
            """{"answerContractVersion":1,"values":{"A":1,"A":2},"omitted":[]}""",
            """{"answerContractVersion":1,"values":null,"omitted":[]}"""
        })
            await Assert.That(() => SetupAnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(envelope)))
                .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task CutoverRejectsPriorAndUnknownSemanticSourceContracts()
    {
        const string priorRuntime = """
            {"schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1,
             "applicationId":"example.service","applicationVersions":["1.0"],"publisher":"example",
             "title":"Example service","entryPoints":["setup"],"locales":["en"],
             "fields":[{"id":"service.name","type":"string","required":true,"sensitivity":"public",
             "disclosure":"advanced","title":"Service name","minLength":1,"maxLength":80}],
             "artifacts":[{"id":"config","format":"json","fileName":"service.json","applySemantics":"create",
             "restart":"process","projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}}]}
            """;
        await Assert.That(() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(priorRuntime)))
            .Throws<SetupDefinitionException>();
        foreach (string source in new[]
        {
            SourceV2.Replace("authoring:v2", "authoring:v1", StringComparison.Ordinal),
            SourceV2.Replace("\"type\":\"string\"", "\"type\":\"string\",\"executable\":\"synthetic\"", StringComparison.Ordinal),
            SourceV2.Replace("\"additionalProperties\":false", "\"additionalProperties\":true", StringComparison.Ordinal)
        })
            await Assert.That(() => SetupDefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source)))
                .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ConcretePointersEscapeOnceAndKeepOrdinalIdentity()
    {
        string[] segments = ["", "a/b", "~1", "a.b", "*", "A", "a", "\u00e9", "e\u0301", "\U0001f600"];
        DefinitionPath path = DefinitionPath.FromSegments(segments);
        segments[0] = "changed";
        const string pointer = "//a~1b/~01/a.b/*/A/a/\u00e9/e\u0301/\U0001f600";
        await Assert.That(path.ToPointer()).IsEqualTo(pointer);
        await Assert.That(path.Equals(DefinitionPath.Parse(pointer))).IsTrue();
        await Assert.That(new HashSet<DefinitionPath> { path }.Contains(DefinitionPath.Parse(pointer))).IsTrue();
        await Assert.That(DefinitionPath.Parse("").Segments.Count).IsEqualTo(0);
        await Assert.That(DefinitionPath.Parse("/").Segments.Count).IsEqualTo(1);
        await Assert.That(DefinitionPath.Parse("/A") == DefinitionPath.Parse("/a")).IsFalse();
        await Assert.That(DefinitionPath.Parse("/\u00e9") == DefinitionPath.Parse("/e\u0301")).IsFalse();
        await Assert.That(DefinitionPath.Parse("/synthetic-private-key").ToString().Contains("synthetic", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task PathsRejectMalformedUnicodeEscapesAndResourceOverflow()
    {
        foreach (string pointer in new[] { "not/a/pointer", "/~", "/~2", "/\ud800", "/\udc00",
            new string('/', 33), "/" + new string('x', 65_536) })
            await Assert.That(() => DefinitionPath.Parse(pointer)).Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionPath.FromSegments(Enumerable.Repeat("x", 33)))
            .Throws<SetupDefinitionException>();
        await Assert.That(DefinitionPath.Parse(new string('/', 32)).Segments.Count).IsEqualTo(32);
    }

    [Test]
    public async Task ArrayIndicesAreCanonicalOnlyWhenExplicitlyRequested()
    {
        DefinitionPath path = DefinitionPath.Parse("/0/12/01/-/-1/2147483648");
        await Assert.That(path.TryGetArrayIndex(0, out int zero) && zero == 0).IsTrue();
        await Assert.That(path.TryGetArrayIndex(1, out int twelve) && twelve == 12).IsTrue();
        for (int index = 2; index < 6; index++)
            await Assert.That(path.TryGetArrayIndex(index, out _)).IsFalse();
    }

    [Test]
    public async Task ExactJsonAndYamlShareIndependentCanonicalBytes()
    {
        const string json = """{"a":-0,"A":1.2300e+2,"":null,"é":"e\u0301","e\u0301":"é","slash/~":["\n","\ud83d\ude00"]}""";
        const string yaml = "\"a\": -0\n\"A\": 1.2300e+2\n\"\": null\n\"é\": \"e\\u0301\"\n\"e\\u0301\": \"é\"\n\"slash/~\": [\"\\n\", \"\\U0001F600\"]";
        const string expected = """{"":null,"A":123,"a":0,"e\u0301":"\u00E9","slash/~":["\n","\uD83D\uDE00"],"\u00E9":"e\u0301"}""";
        byte[] first = DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json));
        await Assert.That(Encoding.UTF8.GetString(first)).IsEqualTo(expected);
        await Assert.That(DefinitionProfile.CanonicalizeYaml(Encoding.UTF8.GetBytes(yaml)).SequenceEqual(first)).IsTrue();
        await Assert.That(DefinitionProfile.CanonicalizeJson(first).SequenceEqual(first)).IsTrue();
        foreach ((string input, string canonical) in new[]
        {
            ("-0.000e-28", "0"), ("1e-28", "0.0000000000000000000000000001"),
            ("1e28", "10000000000000000000000000000"),
            ("1234567890123456789012345678", "1234567890123456789012345678")
        })
            await Assert.That(Encoding.UTF8.GetString(DefinitionProfile.CanonicalizeJson(
                Encoding.UTF8.GetBytes("{\"n\":" + input + "}")))).IsEqualTo("{\"n\":" + canonical + "}");
    }

    [Test]
    public async Task ExactAdmissionRejectsDuplicateInvalidUnicodeAndNumbersWithoutValues()
    {
        foreach (string json in new[]
        {
            """{"synthetic-private-key":1,"synthetic-private-key":2}""",
            """{"x":1,"\u0078":2}""", """{"x":"\ud800"}""", """{"\udc00":0}""",
            """{"x":1e29}""", """{"x":1e-29}""", """{"x":0.00000000000000000000000000001}""",
            """{"x":1.2345678901234567890123456789}""", """{"x":NaN}""",
            """{"x":79228162514264337593543950336}"""
        })
        {
            try
            {
                DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json));
                throw new InvalidOperationException("Expected admission refusal.");
            }
            catch (SetupDefinitionException exception)
            {
                await Assert.That(exception.Message.Contains("synthetic", StringComparison.Ordinal)).IsFalse();
            }
        }
        await Assert.That(() => DefinitionProfile.CanonicalizeJson(new byte[] { 123, 34, 120, 34, 58, 34, 0xFF, 34, 125 }))
            .Throws<SetupDefinitionException>();
        foreach (string yaml in new[] { "\"x\": 1\n\"x\": 2", "\"x\": \"\\uD800\"", "x: &a value\ny: *a", "x: !run value", "x: 1e29" })
            await Assert.That(() => DefinitionProfile.CanonicalizeYaml(Encoding.UTF8.GetBytes(yaml)))
                .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task ExactAdmissionBoundsBytesDepthContainersScalarsAndAggregateWork()
    {
        foreach (string json in new[]
        {
            "{\"x\":\"" + new string('x', 65_537) + "\"}",
            "{\"" + new string('x', 65_537) + "\":0}",
            "{\"x\":" + new string('[', 32) + "0" + new string(']', 32) + "}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("0", 4097)) + "]}",
            "{" + string.Join(",", Enumerable.Range(0, 4097).Select(index => "\"" + index + "\":0")) + "}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("\"" + new string('x', 65_536) + "\"", 17)) + "]}",
            "{\"x\":[" + string.Join(",", Enumerable.Repeat("[" + string.Join(",", Enumerable.Repeat("0", 4096)) + "]", 16)) + "]}"
        })
            await Assert.That(() => DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(json)))
                .Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionProfile.CanonicalizeJson(new byte[4_194_305]))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => DefinitionProfile.CanonicalizeYaml(new byte[4_194_305]))
            .Throws<SetupDefinitionException>();
    }

    [Test]
    public async Task RecursiveNodesSnapshotInputsAndProvideConsumedStructuralEquality()
    {
        var leaf = new DefinitionNode(DefinitionNodeType.Text, nullable: true);
        var properties = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal) { ["A"] = leaf, ["a"] = leaf };
        var required = new List<string> { "A" };
        var first = new DefinitionNode(DefinitionNodeType.Mapping, properties: properties, required: required,
            additionalProperties: new DefinitionNode(DefinitionNodeType.Sequence, items: leaf));
        properties.Clear();
        required[0] = "a";
        var equal = new DefinitionNode(DefinitionNodeType.Mapping,
            properties: new Dictionary<string, DefinitionNode> { ["a"] = leaf, ["A"] = leaf },
            required: ["A"], additionalProperties: new DefinitionNode(DefinitionNodeType.Sequence,
                items: new DefinitionNode(DefinitionNodeType.Text, nullable: true)));
        await Assert.That(first.Properties.Count).IsEqualTo(2);
        await Assert.That(first.Required[0]).IsEqualTo("A");
        await Assert.That(first.Equals(equal)).IsTrue();
        await Assert.That(new HashSet<DefinitionNode> { first }.Contains(equal)).IsTrue();
        await Assert.That(first == new DefinitionNode(DefinitionNodeType.Mapping,
            properties: equal.Properties, required: ["a"], additionalProperties: equal.AdditionalProperties)).IsFalse();
        await Assert.That(leaf == new DefinitionNode(DefinitionNodeType.Text)).IsFalse();
        await Assert.That((leaf with { }).Equals(leaf)).IsTrue();
    }

    [Test]
    public async Task RecursiveNodesRejectInvalidShapesAndBoundExpandedTrees()
    {
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Sequence)).Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Text, items: new DefinitionNode(DefinitionNodeType.Boolean)))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping, required: ["missing"]))
            .Throws<SetupDefinitionException>();
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping,
            properties: new Dictionary<string, DefinitionNode> { ["\ud800"] = new(DefinitionNodeType.Text) }))
            .Throws<SetupDefinitionException>();
        DefinitionNode node = new(DefinitionNodeType.Text);
        for (int depth = 1; depth < 32; depth++) node = new DefinitionNode(DefinitionNodeType.Sequence, items: node);
        DefinitionNode deepest = node;
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Sequence, items: deepest)).Throws<SetupDefinitionException>();
        DefinitionNode repeated = new(DefinitionNodeType.Text);
        for (int depth = 0; depth < 4; depth++)
            repeated = new DefinitionNode(DefinitionNodeType.Mapping,
                properties: Enumerable.Range(0, 10).Select(index => KeyValuePair.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture), repeated)));
        DefinitionNode expanded = repeated;
        await Assert.That(() => new DefinitionNode(DefinitionNodeType.Mapping,
            properties: Enumerable.Range(0, 10).Select(index => KeyValuePair.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture), expanded))))
            .Throws<SetupDefinitionException>();
    }
}
