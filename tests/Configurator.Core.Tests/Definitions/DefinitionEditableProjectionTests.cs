namespace Configurator.Core.Tests.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionEditableProjectionTests
{
    [Test]
    public async Task ActiveConditionalCopyCapturesLiteralDottedPointerWithoutBroadeningWholeInverse()
    {
        Definition definition = Definition();
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"enabled":true,"service.name":"synthetic"}""", 9));
        await Assert.That(scope.InputRevision).IsEqualTo(9);
        await Assert.That(scope.Mappings.Count).IsEqualTo(1);
        await Assert.That(scope.Mappings[0].Key).IsEqualTo("NAME");
        await Assert.That(scope.Mappings[0].Path.ToPointer()).IsEqualTo("/service.name");
        await Assert.That(DefinitionImport.CanImport(definition, "environment")).IsFalse();
        await Assert.That(scope.Coverage.Single(item => item.Key == "ENABLED").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Controller);
        await Assert.That(scope.Coverage.Single(item => item.Key == "FIXED").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.ReadOnly);
    }

    [Test]
    public async Task RevalidationRejectsRevisionIdentityAndChangedActivationWithoutRebasing()
    {
        Definition definition = Definition();
        AnswerSnapshot original = DefinitionFixture.Answers("""{"enabled":true,"service.name":"synthetic"}""", 9);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment", original);
        await Assert.That(scope.IsCurrent(definition, original)).IsTrue();
        await Assert.That(scope.IsCurrent(definition,
            DefinitionFixture.Answers("""{"enabled":true,"service.name":"synthetic"}""", 10))).IsFalse();
        await Assert.That(scope.IsCurrent(definition,
            DefinitionFixture.Answers("""{"enabled":false,"service.name":"synthetic"}""", 9))).IsFalse();
        Definition changed = Build("""{"service.name":{"type":"string"}}""",
            """{"OTHER":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}""");
        await Assert.That(scope.IsCurrent(changed, original)).IsFalse();
        await Assert.That(scope.Mappings.Single().Key).IsEqualTo("NAME");
        await Assert.That(original.Values["enabled"].GetBoolean()).IsTrue();
    }

    [Test]
    [Arguments("""{"enabled":false}""", DefinitionEditableCoverageKind.Inactive)]
    [Arguments("""{"enabled":"invalid"}""", DefinitionEditableCoverageKind.ConditionUnresolved)]
    [Arguments("""{}""", DefinitionEditableCoverageKind.Inactive)]
    public async Task InactiveAndUnresolvedConditionsCannotAcquireEditableKeys(string answers,
        DefinitionEditableCoverageKind expected)
    {
        var scope = DefinitionImport.CaptureEditableScope(Definition(), "environment", DefinitionFixture.Answers(answers));
        await Assert.That(scope.Mappings.Count).IsEqualTo(0);
        await Assert.That(scope.Coverage.Single(item => item.Key == "NAME").Kind).IsEqualTo(expected);
        await Assert.That(scope.TryConvertValue("NAME", "pasted", out _)).IsFalse();
    }

    [Test]
    public async Task ProtectedAndAmbiguousAssignmentsRemainExplicitWithoutSerializingAnswers()
    {
        Definition definition = Build("""
            {"plain":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "private":{"type":"string","x-configurator":{"sensitivity":"secret"}}}
            """, """
            {"ONE":{"op":"copy","reference":{"scope":"root","path":"/plain"}},
             "TWO":{"op":"copy","reference":{"scope":"root","path":"/plain"}},
             "PRIVATE":{"op":"copy","reference":{"scope":"root","path":"/private"}},
             "FIXED":{"op":"literal","value":"definition-value"}}
            """);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"plain":"ordinary-sentinel","private":"protected-sentinel"}"""));
        await Assert.That(scope.Mappings.Count).IsEqualTo(0);
        await Assert.That(scope.Coverage.Single(item => item.Key == "ONE").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Ambiguous);
        await Assert.That(scope.Coverage.Single(item => item.Key == "TWO").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Ambiguous);
        await Assert.That(scope.Coverage.Single(item => item.Key == "PRIVATE").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Protected);
        await Assert.That(JsonSerializer.Serialize(scope)).DoesNotContain("protected-sentinel");
        await Assert.That(JsonSerializer.Serialize(scope)).DoesNotContain("ordinary-sentinel");
        await Assert.That(scope.TryConvertValue("PRIVATE", "new", out _)).IsFalse();
        await Assert.That(scope.TryConvertValue("FIXED", "new", out _)).IsFalse();
        await Assert.That(scope.TryConvertValue("UNKNOWN", "new", out _)).IsFalse();
    }

    [Test]
    public async Task SourceApplicabilityUsesOwningValidatorAndFreezesNestedController()
    {
        Definition definition = Build("""
            {"switches":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
              "properties":{"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}}}},
             "options":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
              "value":{"type":"string","x-configurator":{"sensitivity":"public",
                "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/switches/enabled"},"value":true}}}}}}
            """, """
            {"ENABLE":{"op":"copy","reference":{"scope":"root","path":"/switches/enabled"}},
             "VALUE":{"op":"copy","reference":{"scope":"root","path":"/options/value"}}}
            """);
        var active = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"switches":{"enabled":true},"options":{"value":"synthetic"}}"""));
        await Assert.That(active.Mappings.Select(item => item.Key).SequenceEqual(["VALUE"])).IsTrue();
        await Assert.That(active.Coverage.Single(item => item.Key == "ENABLE").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Controller);
        var inactive = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"switches":{"enabled":false},"options":{"value":"synthetic"}}"""));
        await Assert.That(inactive.Mappings.Count).IsEqualTo(0);
        await Assert.That(inactive.Coverage.Single(item => item.Key == "VALUE").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Inactive);
    }

    [Test]
    public async Task LiteralDotsSlashesAndTildesRemainDistinctFromNestedPaths()
    {
        Definition definition = Build("""
            {"a.b":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "a":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
              "properties":{"b":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
             "slash/~":{"type":"integer","x-configurator":{"sensitivity":"public"}}}
            """, """
            {"LITERAL":{"op":"copy","reference":{"scope":"root","path":"/a.b"}},
             "NESTED":{"op":"copy","reference":{"scope":"root","path":"/a/b"}},
             "ESCAPED":{"op":"copy","reference":{"scope":"root","path":"/slash~1~0"}}}
            """);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"a.b":"literal","a":{"b":"nested"},"slash/~":2}"""));
        await Assert.That(scope.Mappings.Single(item => item.Key == "LITERAL").Path.ToPointer()).IsEqualTo("/a.b");
        await Assert.That(scope.Mappings.Single(item => item.Key == "NESTED").Path.ToPointer()).IsEqualTo("/a/b");
        await Assert.That(scope.Mappings.Single(item => item.Key == "ESCAPED").Path.ToPointer()).IsEqualTo("/slash~1~0");
    }

    [Test]
    public async Task ExactScalarInverseConversionPreservesFalseZeroAndEmptyWithoutGuessing()
    {
        Definition definition = Build("""
            {"text":{"type":"string","maxLength":5,"x-configurator":{"sensitivity":"public"}},
             "boolean":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
             "integer":{"type":"integer","x-configurator":{"sensitivity":"public"}},
             "number":{"type":"number","x-configurator":{"sensitivity":"public"}}}
            """, """
            {"TEXT":{"op":"copy","reference":{"scope":"root","path":"/text"}},
             "BOOLEAN":{"op":"copy","reference":{"scope":"root","path":"/boolean"}},
             "INTEGER":{"op":"copy","reference":{"scope":"root","path":"/integer"}},
             "NUMBER":{"op":"copy","reference":{"scope":"root","path":"/number"}}}
            """);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment", DefinitionFixture.Answers("{}"));
        await Assert.That(scope.TryConvertValue("TEXT", "", out JsonElement text)).IsTrue();
        await Assert.That(text.GetString()).IsEqualTo("");
        await Assert.That(scope.TryConvertValue("BOOLEAN", "false", out JsonElement boolean)).IsTrue();
        await Assert.That(boolean.GetBoolean()).IsFalse();
        await Assert.That(scope.TryConvertValue("INTEGER", "0", out JsonElement integer)).IsTrue();
        await Assert.That(integer.GetInt64()).IsEqualTo(0);
        await Assert.That(scope.TryConvertValue("NUMBER", "1.25", out JsonElement number)).IsTrue();
        await Assert.That(number.GetDecimal()).IsEqualTo(1.25m);
        foreach ((string key, string value) in new[] { ("TEXT", "too-long"), ("BOOLEAN", "False"),
            ("BOOLEAN", "0"), ("INTEGER", "01"), ("NUMBER", "1e2"), ("NUMBER", "null") })
            await Assert.That(scope.TryConvertValue(key, value, out _)).IsFalse();
    }

    [Test]
    public async Task CancellationRejectsCaptureAndRevalidation()
    {
        Definition definition = Definition();
        var answers = DefinitionFixture.Answers("""{"enabled":true}""");
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment", answers);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => DefinitionImport.CaptureEditableScope(definition, "environment", answers,
            cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(() => scope.IsCurrent(definition, answers, cancellation.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CaseCollisionsAndArrayIndexedSourcesDoNotBecomeEditable()
    {
        Definition definition = Build("""
            {"first":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "second":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "items":{"type":"array","x-configurator":{"sensitivity":"public"},
               "items":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
            """, """
            {"NAME":{"op":"copy","reference":{"scope":"root","path":"/first"}},
             "name":{"op":"copy","reference":{"scope":"root","path":"/second"}},
             "ITEM":{"op":"copy","reference":{"scope":"root","path":"/items/0"}}}
            """);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"first":"one","second":"two","items":["three"]}"""));
        await Assert.That(scope.Mappings.Count).IsEqualTo(0);
        await Assert.That(scope.Coverage.Single(item => item.Key == "NAME").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Ambiguous);
        await Assert.That(scope.Coverage.Single(item => item.Key == "name").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Ambiguous);
        await Assert.That(scope.Coverage.Single(item => item.Key == "ITEM").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Unsupported);
    }

    [Test]
    public async Task BundledEventExposesPublicEnvironmentScopeAndSupportedWholeInverse()
    {
        using Stream source = typeof(DefinitionEditableProjectionTests).Assembly.GetManifestResourceStream("EventDefinition")!;
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        Definition definition = DefinitionCodec.ParseJson(buffer.ToArray());
        AnswerSnapshot context = DefinitionFixture.Answers("""{"operation":"environment","deployment":{"recipe":"standalone","scope":"all"},"hosting":{},"platform":{"authenticationProvider":"local"},"secrets":{"provider":"Environment"}}""");
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment-standalone", context);
        await Assert.That(scope.IsSupported).IsTrue();
        await Assert.That(scope.Mappings.Single(item => item.Key == "ASPNETCORE_ENVIRONMENT").Path.ToPointer())
            .IsEqualTo("/hosting/aspnetcoreEnvironment");
        await Assert.That(scope.Coverage.Any(item => item.Kind == DefinitionEditableCoverageKind.Protected)).IsTrue();
        await Assert.That(scope.Mappings.Any(item => item.Path.ToPointer() == "/operation")).IsFalse();
        DefinitionImportCapability capability = DefinitionImport.GetCapability(definition, "environment-standalone", context);
        await Assert.That(capability.StrictAutofillSupported).IsTrue().Because(capability.Code);
    }

    [Test]
    public async Task TransitiveActivationControllersStayOutsideCapturedMappings()
    {
        Definition definition = Build("""
            {"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
             "mode":{"type":"boolean","x-configurator":{"sensitivity":"public","applicableWhen":{
               "op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}},
             "text":{"type":"string","x-configurator":{"sensitivity":"public"}}}
            """, """
            {"ENABLE":{"op":"copy","reference":{"scope":"root","path":"/enabled"}},
             "MODE":{"op":"copy","reference":{"scope":"root","path":"/mode"}},
             "TEXT":{"op":"copy","reference":{"scope":"root","path":"/text"},
               "when":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":true}}}
            """);
        var scope = DefinitionImport.CaptureEditableScope(definition, "environment",
            DefinitionFixture.Answers("""{"enabled":true,"mode":true,"text":"synthetic"}"""));
        await Assert.That(scope.Mappings.Select(item => item.Key).SequenceEqual(["TEXT"])).IsTrue();
        await Assert.That(scope.Controllers.Select(path => path.ToPointer()).SequenceEqual(["/enabled", "/mode"])).IsTrue();
        await Assert.That(scope.Coverage.Single(item => item.Key == "ENABLE").Kind)
            .IsEqualTo(DefinitionEditableCoverageKind.Controller);
    }

    internal static Definition Build(string properties, string members)
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["properties"] = JsonNode.Parse(properties);
        source["configuration"]!["required"] = new JsonArray();
        source["outputs"] = JsonNode.Parse("""
            {"settings.env":{"id":"environment","format":"dotenv","projection":{"op":"object","members":{}}}}
            """);
        source["outputs"]!["settings.env"]!["projection"]!["members"] = JsonNode.Parse(members);
        return DefinitionFixture.Parse(source.ToJsonString());
    }

    internal static Definition Definition()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["properties"] = JsonNode.Parse("""
            {"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
             "service.name":{"type":"string","x-configurator":{"sensitivity":"public"}}}
            """);
        source["outputs"] = JsonNode.Parse("""
            {"settings.env":{"id":"environment","format":"dotenv","projection":{"op":"object","members":{
              "ENABLED":{"op":"copy","reference":{"scope":"root","path":"/enabled"}},
              "NAME":{"op":"copy","reference":{"scope":"root","path":"/service.name"},
                "when":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}},
              "FIXED":{"op":"literal","value":"fixed"}}}}}
            """);
        return DefinitionFixture.Parse(source.ToJsonString());
    }
}
