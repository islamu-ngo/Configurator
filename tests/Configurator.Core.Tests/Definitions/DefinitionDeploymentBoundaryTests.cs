namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionDeploymentBoundaryTests
{
    [Test]
    [Arguments("endpoint", "/shared/endpoint")]
    [Arguments("api.endpoint", "/shared/api.endpoint")]
    [Arguments("a/b~c", "/shared/a~1b~0c")]
    public async Task StructuralAncestorDoesNotActivateRequiredSibling(string leaf, string location)
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["shared"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"required":["otherCredential"],
                 "properties":{"otherCredential":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}
                """);
            source["configuration"]!["properties"]!["shared"]!["properties"]![leaf] =
                JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
            source["configuration"]!["properties"]!["shared"]!["required"]!.AsArray().Add(leaf);
            source["configuration"]!["required"]!.AsArray().Add("shared");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] = location;
        });
        var shared = new JsonObject { [leaf] = "synthetic-public" };
        AnswerSnapshot answers = Snapshot(new JsonObject { ["scope"] = "dashboard", ["shared"] = shared });
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifacts.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[0].Bytes.Span))
            .IsEqualTo("{\"endpoint\":\"synthetic-public\"}");
        await Assert.That(DefinitionValidator.Validate(definition, answers)
            .TryGetEffectiveValue(DefinitionPath.Parse("/shared/otherCredential"), out _)).IsFalse();
    }

    [Test]
    public async Task RepeatScopeResolvesEachRowWithoutActivatingCredentialSibling()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["rows"] = JsonNode.Parse("""
                {"type":"array","maxItems":4,"items":{"type":"object","additionalProperties":false,
                 "required":["endpoint","credential"],"properties":{
                 "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "credential":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}}
                """);
            source["configuration"]!["required"]!.AsArray().Add("rows");
            JsonObject output = source["outputs"]!["dashboard.json"]!.AsObject();
            output.Remove("bindings");
            output["projection"] = JsonNode.Parse("""
                {"op":"repeat","reference":{"scope":"root","path":"/rows"},
                 "item":{"op":"copy","reference":{"scope":"item","path":"/endpoint"}}}
                """);
        });
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition,
            Snapshot(JsonNode.Parse("""{"scope":"dashboard","rows":[{"endpoint":"first"},{"endpoint":"second"}]}""")!.AsObject()));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts.Single().Bytes.Span))
            .IsEqualTo("[\"first\",\"second\"]");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task PresenceClosureRequiresPeerOnlyForActiveSelectedTrigger(bool active)
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["enabled"] = JsonNode.Parse("""{"type":"boolean"}""");
            source["configuration"]!["properties"]!["peer"] = JsonNode.Parse("""{"type":"string"}""");
            source["configuration"]!["properties"]!["endpoint"]!["x-configurator"]!["applicableWhen"] =
                JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}""");
            source["configuration"]!["dependentRequired"] = JsonNode.Parse("""{"endpoint":["peer"]}""");
        });
        AnswerSnapshot answers = Snapshot(new JsonObject
        {
            ["scope"] = "dashboard", ["endpoint"] = "synthetic-retained", ["enabled"] = active
        });
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, answers);
        await Assert.That(result.IsReady).IsEqualTo(!active);
        await Assert.That(result.Diagnostics.Any(item => item.FieldId == "peer" && item.Code == "value-required"))
            .IsEqualTo(active);
    }

    [Test]
    [Arguments("remote", "remotePeer")]
    [Arguments("local", "localPeer")]
    public async Task ConditionalPresenceClosureUsesTheEffectiveBranch(string endpoint, string peer)
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["remotePeer"] = JsonNode.Parse("""{"type":"string"}""");
            source["configuration"]!["properties"]!["localPeer"] = JsonNode.Parse("""{"type":"string"}""");
            source["configuration"]!["if"] = JsonNode.Parse("""{"properties":{"endpoint":{"const":"remote"}},"required":["endpoint"]}""");
            source["configuration"]!["then"] = JsonNode.Parse("""{"required":["remotePeer"]}""");
            source["configuration"]!["else"] = JsonNode.Parse("""{"required":["localPeer"]}""");
        });
        var values = new JsonObject { ["scope"] = "dashboard", ["endpoint"] = endpoint };
        DefinitionValidationResult missing = DefinitionValidator.Validate(definition, Snapshot(values));
        await Assert.That(missing.IsReady).IsFalse();
        await Assert.That(missing.Diagnostics.Any(item => item.FieldId == peer && item.Code == "value-required")).IsTrue();
        values[peer] = "synthetic-peer";
        await Assert.That(DefinitionValidator.Validate(definition, Snapshot(values)).IsReady).IsTrue();
    }

    [Test]
    public async Task NarrowingRetainsGlobalUnknownAndExplicitSelectorOmissionErrors()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create();
        var values = new JsonObject { ["scope"] = "dashboard", ["endpoint"] = "synthetic", ["synthetic-unknown"] = "private" };
        await Assert.That(DefinitionValidator.Validate(definition, Snapshot(values)).IsReady).IsFalse();
        values.Remove("synthetic-unknown");
        AnswerSnapshot omitted = new(2, Snapshot(values).Values, [DefinitionPath.Parse("/recipe")]);
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, omitted);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PortableDeliveryCollisionAndConsumerScopeMismatchRejectAtAdmission()
    {
        await Assert.That(() => DefinitionDeploymentScopeTests.Create(source =>
        {
            source["outputs"]!["dashboard.json"]!["consumerIds"] = new JsonArray("worker");
        })).Throws<DefinitionException>();
        await Assert.That(() => DefinitionDeploymentScopeTests.Create(source =>
        {
            JsonNode dashboard = source["outputs"]!["dashboard.json"]!.DeepClone();
            source["outputs"]!.AsObject().Remove("dashboard.json");
            source["outputs"]!["WORKER.json"] = dashboard;
        })).Throws<DefinitionException>();
    }

    [Test]
    [Arguments("{\"a.b\":\"first\",\"a/b\":\"second\"}")]
    [Arguments("{}")]
    public async Task SelectedTypedMapUsesConcreteEntriesAndExcludesOtherRecipient(string payload)
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["payload"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":{"type":"string","minLength":1,
                 "x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public"}}
                """);
            source["configuration"]!["required"]!.AsArray().Add("payload");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] = "/payload";
        });
        JsonObject values = new() { ["scope"] = "dashboard", ["payload"] = JsonNode.Parse(payload) };
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, Snapshot(values));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts.Single().Bytes.Span))
            .IsEqualTo("{\"endpoint\":" + payload + "}");
    }

    [Test]
    public async Task SelectedVariantRequiresItsBranchWithoutOtherRecipientObligations()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["payload"] = JsonNode.Parse("""
                {"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
                 {"type":"object","additionalProperties":false,"required":["kind","endpoint"],"x-configurator":{"sensitivity":"public"},
                  "properties":{"kind":{"type":"string","const":"remote","x-configurator":{"sensitivity":"public"}},"endpoint":{"type":"string"}}},
                 {"type":"object","additionalProperties":false,"required":["kind","path"],"x-configurator":{"sensitivity":"public"},
                  "properties":{"kind":{"type":"string","const":"local","x-configurator":{"sensitivity":"public"}},"path":{"type":"string"}}}]}
                """);
            source["configuration"]!["required"]!.AsArray().Add("payload");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] = "/payload";
        });
        var values = new JsonObject { ["scope"] = "dashboard", ["payload"] = JsonNode.Parse("""{"kind":"local","path":"synthetic"}""") };
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, Snapshot(values));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts.Single().Bytes.Span))
            .IsEqualTo("{\"endpoint\":{\"kind\":\"local\",\"path\":\"synthetic\"}}");
        values["payload"]!.AsObject().Remove("path");
        await Assert.That(DefinitionValidator.Validate(definition, Snapshot(values)).IsReady).IsFalse();
    }

    [Test]
    public async Task ContextBootstrapUsesInitializedNestedPublicTask()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["mode"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"required":["task"],
                 "x-configurator":{"sensitivity":"public"},"properties":{
                 "task":{"type":"string","enum":["environment","manifest"],
                  "x-configurator":{"sensitivity":"public","initialValue":"environment"}}}}
                """);
            source["configuration"]!["required"]!.AsArray().Add("mode");
            source["deployment"]!["when"] = JsonNode.Parse("""
                {"op":"equals","reference":{"scope":"root","path":"/mode/task"},"value":"environment"}
                """);
        });
        AnswerSnapshot answers = Snapshot(new JsonObject
        {
            ["scope"] = "dashboard", ["endpoint"] = "synthetic", ["mode"] = new JsonObject(),
            ["workerSecret"] = "synthetic-retained-admin"
        });
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifacts.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts.Single().Bytes.Span))
            .IsEqualTo("{\"endpoint\":\"synthetic\"}");
    }

    [Test]
    public async Task InvalidSelectorDiagnosticsDoNotSerializeUnadmittedAnswerText()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create();
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, Snapshot(new JsonObject
        {
            ["scope"] = "synthetic-private-unadmitted-selection", ["endpoint"] = "synthetic"
        }));
        await Assert.That(result.IsReady).IsFalse();
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private-unadmitted-selection");
    }

    [Test]
    public async Task MissingSiblingSecretDoesNotMakePublicNestedGuardUnknownInDraft()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["shared"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"required":["endpoint","credential"],
                 "x-configurator":{"sensitivity":"public"},"properties":{
                 "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "credential":{"type":"string","x-configurator":{"sensitivity":"secret"}},
                 "localPeer":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "remotePeer":{"type":"string","x-configurator":{"sensitivity":"public"}}},
                 "if":{"properties":{"endpoint":{"const":"remote"}},"required":["endpoint"]},
                 "then":{"required":["remotePeer"]},"else":{"required":["localPeer"]}}
                """);
            source["configuration"]!["required"]!.AsArray().Add("shared");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] = "/shared";
        });
        AnswerSnapshot answers = Snapshot(JsonNode.Parse("""
            {"scope":"dashboard","shared":{"endpoint":"local","localPeer":"synthetic"}}
            """)!.AsObject());
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "dashboard", answers);
        await Assert.That(draft.Succeeded).IsTrue();
        await Assert.That(draft.IsDraft).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task NumericRowProjectionNeverMakesExcludedRowCredentialsEffective(int selectedRow)
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(source =>
        {
            source["configuration"]!["properties"]!["rows"] = JsonNode.Parse("""
                {"type":"array","maxItems":4,"items":{"type":"object","additionalProperties":false,
                 "required":["endpoint"],"properties":{
                 "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}},
                 "credential":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}}
                """);
            source["configuration"]!["required"]!.AsArray().Add("rows");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] =
                selectedRow == 0 ? "/rows/0/endpoint" : "/rows/1/endpoint";
        });
        AnswerSnapshot answers = Snapshot(JsonNode.Parse("""
            {"scope":"dashboard","rows":[{"endpoint":"first","credential":"synthetic-first-row-admin"},
             {"endpoint":"second","credential":"synthetic-second-row-admin"}]}
            """)!.AsObject());
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers);
        await Assert.That(validation.IsReady).IsTrue();
        await Assert.That(validation.TryGetEffectiveValue(DefinitionPath.Parse("/rows/0/credential"), out _)).IsFalse();
        await Assert.That(validation.TryGetEffectiveValue(DefinitionPath.Parse("/rows/1/credential"), out _)).IsFalse();
        using DefinitionBundleResult bundle = DefinitionBundleComposer.ComposeActive(definition, answers);
        await Assert.That(bundle.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(bundle.Artifacts.Single().Bytes.Span))
            .IsEqualTo(selectedRow == 0 ? "{\"endpoint\":\"first\"}" : "{\"endpoint\":\"second\"}");
    }

    private static AnswerSnapshot Snapshot(JsonObject values)
    {
        using JsonDocument document = JsonDocument.Parse(values.ToJsonString());
        return new AnswerSnapshot(1, document.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal));
    }
}
