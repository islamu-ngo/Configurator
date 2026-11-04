namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionDeploymentScopeTests
{
    [Test]
    public async Task DashboardScopeExcludesWorkerCredentialsAndRejectsForgedWorkerOutput()
    {
        Definition definition = Create();
        var answers = Answers("dashboard", worker: "synthetic-worker-admin-sentinel");
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, answers);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Artifacts.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[0].Bytes.Span)).IsEqualTo("{\"endpoint\":\"synthetic-endpoint\"}");
        DefinitionArtifactResult forged = DefinitionArtifactComposer.Compose(definition, "worker", answers);
        await Assert.That(forged.Succeeded).IsFalse();
        await Assert.That(forged.Diagnostics.Any(item => item.Code == "artifact-context")).IsTrue();
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "dashboard", answers);
        await Assert.That(draft.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span).Contains("synthetic-worker-admin-sentinel", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task SharedAdmittedDefinitionKeepsIndependentWorkspaceScopesAndBytes()
    {
        Definition definition = Create();
        var all = Answers("all", "synthetic-worker-admin-sentinel");
        var dashboard = Answers("dashboard");
        using DefinitionBundleResult first = DefinitionBundleComposer.ComposeActive(definition, all);
        using DefinitionBundleResult second = DefinitionBundleComposer.ComposeActive(definition, dashboard);
        using DefinitionBundleResult again = DefinitionBundleComposer.ComposeActive(definition, all);
        await Assert.That(first.Succeeded && second.Succeeded && again.Succeeded).IsTrue();
        await Assert.That(first.Artifacts.Count).IsEqualTo(2);
        await Assert.That(second.Artifacts.Count).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(second.Artifacts[0].Bytes.Span)).IsEqualTo("{\"endpoint\":\"synthetic-endpoint\"}");
        await Assert.That(Encoding.UTF8.GetString(first.Artifacts.Single(artifact => artifact.ArtifactId == "worker").Bytes.Span))
            .IsEqualTo("{\"credential\":\"synthetic-worker-admin-sentinel\"}");
        await Assert.That(again.Artifacts.Select(item => item.Digest.Value).SequenceEqual(first.Artifacts.Select(item => item.Digest.Value), StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    public async Task DefaultsSelectAllAndSubsetExportCannotBypassSelectedScopeRequirements()
    {
        Definition definition = Create();
        var supplied = Answers("all").Values.Where(pair => pair.Key != "scope")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var answers = new AnswerSnapshot(1, supplied);
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, answers);
        await Assert.That(result.ConfigurationContext!.RecipeId).IsEqualTo("separate");
        await Assert.That(result.ConfigurationContext.ScopeId).IsEqualTo("all");
        await Assert.That(result.IsReady).IsFalse();
        DefinitionArtifactResult subset = DefinitionArtifactComposer.Compose(definition, "dashboard", answers);
        await Assert.That(subset.Succeeded).IsFalse();
        using DefinitionDraftBundleResult draft = DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, answers);
        await Assert.That(draft.Artifacts.Count).IsEqualTo(2);
        await Assert.That(draft.Completions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task InvalidOrOmittedSelectorsBlockCompleteAndDraftOutput()
    {
        Definition definition = Create();
        foreach (AnswerSnapshot answers in new[]
        {
            Answers("unknown"),
            new AnswerSnapshot(1, Answers("dashboard").Values, [DefinitionPath.Parse("/recipe")])
        })
        {
            using DefinitionBundleResult complete = DefinitionBundleComposer.ComposeActive(definition, answers);
            using DefinitionDraftBundleResult draft = DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, answers);
            await Assert.That(complete.Succeeded).IsFalse();
            await Assert.That(complete.Artifacts.Count).IsEqualTo(0);
            await Assert.That(draft.Artifacts.Count).IsEqualTo(0);
            await Assert.That(complete.Diagnostics.Any(item => item.Code == "deployment-context")).IsTrue();
        }
    }

    [Test]
    public async Task DeferredPresenceControllerCannotClaimACompleteDependencyClosure()
    {
        Definition definition = Create(source =>
        {
            source["configuration"]!["properties"]!["routing"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret"}}""");
            source["configuration"]!["properties"]!["remotePeer"] = JsonNode.Parse("""{"type":"string"}""");
            source["configuration"]!["required"]!.AsArray().Add("routing");
            source["configuration"]!["if"] = JsonNode.Parse("""{"required":["routing"],"properties":{"routing":{"const":"remote"}}}""");
            source["configuration"]!["then"] = JsonNode.Parse("""{"required":["remotePeer"]}""");
            source["deployment"]!["recipes"]![0]!["scopes"]![2]!["prerequisitePointers"] = new JsonArray("/routing");
        });
        AnswerSnapshot answers = Answers("dashboard");
        await Assert.That(DefinitionScopeResolver.Resolve(definition, answers)!.IsReady).IsTrue();
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, answers);
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Any(item => item.Code == "scope-dependency-unresolved")).IsTrue();
    }

    [Test]
    public async Task DeferredDependentRequiredTriggerCannotOmitItsPeerFromTheScope()
    {
        Definition definition = Create(source =>
        {
            source["configuration"]!["properties"]!["routing"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret"}}""");
            source["configuration"]!["properties"]!["peer"] = JsonNode.Parse("""{"type":"string"}""");
            source["configuration"]!["required"]!.AsArray().Add("routing");
            source["configuration"]!["dependentRequired"] = JsonNode.Parse("""{"routing":["peer"]}""");
            source["outputs"]!["dashboard.json"]!["bindings"]!["/routing"] = JsonNode.Parse("""{"scope":"root","path":"/routing"}""");
        });
        using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, Answers("dashboard"));
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Any(item => item.Code == "scope-dependency-unresolved")).IsTrue();
    }

    [Test]
    public async Task InactiveUnclassifiableVariantCannotHideUnknownStructure()
    {
        Definition definition = Create(source => source["configuration"]!["properties"]!["payload"] = JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}}}},
             {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}}}}]}
            """));
        var values = Answers("dashboard").Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        values.Add("payload", JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
        { ["kind"] = "invalid", ["synthetic-private-unknown-key"] = "synthetic-private-value" }));
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, new AnswerSnapshot(1, values));
        await Assert.That(result.IsReady).IsFalse();
        await Assert.That(result.Diagnostics.Any(item => item.Code == "value-variant")).IsTrue();
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("synthetic-private-unknown-key");
    }

    [Test]
    public async Task InapplicableDeploymentDoesNotImposeSelectorValidationOnManifest()
    {
        Definition definition = Create(source =>
        {
            source["configuration"]!["required"]!.AsArray().Clear();
            source["configuration"]!["properties"]!["operation"] = JsonNode.Parse("""
                {"type":"string","enum":["environment","manifest"],
                 "x-configurator":{"sensitivity":"public","initialValue":"manifest"}}
                """);
            source["deployment"]!["when"] = JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/operation"},"value":"environment"}""");
            source["outputs"]!["manifest.json"] = JsonNode.Parse("""{"id":"manifest","format":"json","projection":{"op":"literal","value":{"manifest":true}}}""");
        });
        var answers = new AnswerSnapshot(1, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        { ["scope"] = JsonSerializer.SerializeToElement("synthetic-inactive-selection") }, [DefinitionPath.Parse("/recipe")]);
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, "manifest", answers);
        using DefinitionArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("{\"manifest\":true}");
        await Assert.That(DefinitionValidator.Validate(definition, answers).ConfigurationContext).IsNull();
    }

    [Test]
    public async Task ExhaustedScopeTraversalRejectsWithoutAdoptingAPartialArtifactSet()
    {
        Definition definition = Create(source =>
        {
            var groups = new JsonObject();
            for (int group = 0; group < 16; group++)
            {
                var members = new JsonObject();
                for (int item = 0; item < 1000; item++) members["value" + item] = new JsonObject { ["type"] = "string" };
                groups["group" + group] = new JsonObject
                {
                    ["type"] = "object", ["additionalProperties"] = false, ["properties"] = members
                };
            }
            source["configuration"]!["properties"]!["large"] = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false, ["properties"] = groups
            };
            source["outputs"]!["dashboard.json"]!["bindings"]!["/endpoint"]!["path"] = "/large";
        });
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(definition, Answers("dashboard"));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts.Count).IsEqualTo(0);
        await Assert.That(result.Diagnostics.Any(item => item.Code == "validation-limit")).IsTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => DefinitionScopeResolver.Resolve(definition, Answers("dashboard"), cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    internal static Definition Create(Action<JsonObject>? change = null)
    {
        JsonObject source = JsonNode.Parse("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"bd9f7421-620a-4308-8e98-e835c2fda911","revision":1},
         "project":{"id":"scope-fixture","name":"Scope fixture"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["recipe","scope","workerSecret","endpoint"],"properties":{
          "recipe":{"type":"string","enum":["separate"],"x-configurator":{"sensitivity":"public","initialValue":"separate"}},
          "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
          "workerSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
         "deployment":{"recipePointer":"/recipe","scopePointer":"/scope","components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
          "recipes":[{"id":"separate","title":"Separate","scopes":[
           {"id":"all","title":"All","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
           {"id":"worker","title":"Worker","componentIds":["worker"],"artifactIds":["worker"]},
           {"id":"dashboard","title":"Dashboard","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]},
         "outputs":{
          "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"bindings":{"/credential":{"scope":"root","path":"/workerSecret"}}},
          "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"bindings":{"/endpoint":{"scope":"root","path":"/endpoint"}}}}}
        """)!.AsObject();
        change?.Invoke(source);
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    internal static AnswerSnapshot Answers(string scope, string? worker = null) => new(1,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["scope"] = JsonSerializer.SerializeToElement(scope),
            ["endpoint"] = JsonSerializer.SerializeToElement("synthetic-endpoint")
        }.Concat(worker is null ? [] : new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        { ["workerSecret"] = JsonSerializer.SerializeToElement(worker) }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
}
