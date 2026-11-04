namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionDeploymentAdmissionTests
{
    [Test]
    public async Task ScopedEnvironmentDestinationRequiresExplicitSurfaceAndDialect()
    {
        await Assert.That(() => DefinitionDeploymentScopeTests.Create(source =>
        {
            source["outputs"]!["worker.json"]!["format"] = "dotenv";
            source["outputs"]!["worker.json"]!["bindings"] = JsonNode.Parse("""{"WORKER_CREDENTIAL":{"scope":"root","path":"/workerSecret"}}""");
        })).Throws<DefinitionException>();
    }

    [Test]
    public async Task ClosedDeploymentMetadataRejectsUnknownMembersAndUnboundedRegistries()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["deployment"]!["execute"] = "synthetic-command",
            root => root["deployment"]!["components"]![0]!["credential"] = "synthetic-marker",
            root => root["deployment"]!["recipes"]![0]!["launch"] = "synthetic-command",
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["trustedMask"] = true,
            root => root["deployment"]!["recipes"]!.AsArray().Clear(),
            root => root["deployment"]!["components"] = new JsonArray(Enumerable.Range(0, 65).Select(index =>
                (JsonNode)new JsonObject { ["id"] = "component" + index, ["title"] = "Component" }).ToArray()),
            root => root["deployment"]!["recipes"] = new JsonArray(Enumerable.Range(0, 17).Select(index =>
            {
                JsonNode recipe = root["deployment"]!["recipes"]![0]!.DeepClone();
                recipe["id"] = "recipe" + index;
                return recipe;
            }).ToArray()),
            root => root["deployment"]!["recipes"]![0]!["scopes"] = new JsonArray(Enumerable.Range(0, 17).Select(index =>
            {
                JsonNode scope = root["deployment"]!["recipes"]![0]!["scopes"]![0]!.DeepClone();
                scope["id"] = "scope" + index;
                return scope;
            }).ToArray())
        })
            await Assert.That(() => DefinitionDeploymentScopeTests.Create(mutate)).Throws<DefinitionException>();
    }

    [Test]
    public async Task SelectorAdmissionRejectsProtectedConditionalAmbiguousAndIncompatibleDefaults()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["deployment"]!["scopePointer"] = "/recipe",
            root => root["deployment"]!["recipePointer"] = "/missing",
            root => root["configuration"]!["properties"]!["recipe"]!["x-configurator"]!["sensitivity"] = "secret",
            root => root["configuration"]!["properties"]!["recipe"]!["readOnly"] = true,
            root => root["configuration"]!["properties"]!["recipe"]!["writeOnly"] = true,
            root => root["configuration"]!["properties"]!["recipe"]!["x-configurator"]!.AsObject().Remove("initialValue"),
            root => root["configuration"]!["properties"]!["recipe"]!["x-configurator"]!["applicableWhen"] =
                JsonNode.Parse("""{"op":"exists","reference":{"scope":"root","path":"/endpoint"}}"""),
            root => root["configuration"]!["properties"]!["scope"]!["enum"]!.AsArray().Add("unknown"),
            root => root["deployment"]!["recipes"]![0]!["scopes"]!.AsArray().RemoveAt(0),
            root => root["deployment"]!["when"] =
                JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/recipe"},"value":"separate"}"""),
            root => root["deployment"]!["when"] =
                JsonNode.Parse("""{"op":"exists","reference":{"scope":"root","path":"/workerSecret"}}""")
        })
            await Assert.That(() => DefinitionDeploymentScopeTests.Create(mutate)).Throws<DefinitionException>();
    }

    [Test]
    public async Task ScopeAdmissionRejectsUnknownConsumersReferencesPrerequisitesAndDuplicateIdentities()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["deployment"]!["components"]!.AsArray().Add(root["deployment"]!["components"]![0]!.DeepClone()),
            root => root["deployment"]!["recipes"]!.AsArray().Add(root["deployment"]!["recipes"]![0]!.DeepClone()),
            root => root["deployment"]!["recipes"]![0]!["scopes"]!.AsArray().Add(
                root["deployment"]!["recipes"]![0]!["scopes"]![0]!.DeepClone()),
            root => root["deployment"]!["recipes"]![0]!["scopes"]![1]!["componentIds"] = new JsonArray("dashboard"),
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["componentIds"] = new JsonArray("unknown"),
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["artifactIds"] = new JsonArray("unknown"),
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["artifactIds"] = new JsonArray(),
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["primaryArtifactId"] = "unknown",
            root => root["deployment"]!["recipes"]![0]!["scopes"]![0]!["prerequisitePointers"] = new JsonArray("/unknown"),
            root => root["outputs"]!["worker.json"]!["consumerIds"] = new JsonArray("unknown"),
            root => root["outputs"]!["worker.json"]!.AsObject().Remove("consumerIds")
        })
            await Assert.That(() => DefinitionDeploymentScopeTests.Create(mutate)).Throws<DefinitionException>();
    }

    [Test]
    public async Task DestinationAdmissionRejectsUnsafePortableNamesAndIncompatibleUsage()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => RenameOutput(root, "../worker.json"),
            root => RenameOutput(root, "/worker.json"),
            root => RenameOutput(root, "worker\\settings.json"),
            root => RenameOutput(root, "CON.json"),
            root => RenameOutput(root, "Dashboard.json"),
            root => root["outputs"]!["worker.json"]!["environmentDialect"] = "docker-run",
            root => root["outputs"]!["worker.json"]!["surface"] = "process-environment",
            root => root["outputs"]!["worker.json"]!["unknownUsage"] = true
        })
            await Assert.That(() => DefinitionDeploymentScopeTests.Create(mutate)).Throws<DefinitionException>();
    }

    [Test]
    public async Task RuntimeAdmissionRecomputesDeploymentCapabilityRatherThanTrustingDeclaration()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create();
        JsonObject runtime = JsonNode.Parse(definition.CanonicalBytes.Span)!.AsObject();
        runtime["requiredCapabilities"] = new JsonArray(definition.RequiredCapabilities
            .Where(value => value != "deployment.scope").Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        await Assert.That(() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())))
            .Throws<DefinitionException>();
    }

    [Test]
    public async Task PrerequisiteLimitAppliesAcrossScopesInsteadOfOnlyToIndividualArrays()
    {
        Action<JsonObject> boundary = root =>
        {
            JsonObject properties = root["configuration"]!["properties"]!.AsObject();
            for (int index = properties.Count; index < 1024; index++)
                properties["setting" + index] = new JsonObject { ["type"] = "boolean" };
            root["deployment"]!["recipes"]![0]!["scopes"]![0]!["prerequisitePointers"] = new JsonArray(
                properties.Select(pair => (JsonNode?)JsonValue.Create("/" + pair.Key)).ToArray());
        };
        await Assert.That(DefinitionDeploymentScopeTests.Create(boundary).RequiredCapabilities
            .Contains("deployment.scope", StringComparer.Ordinal)).IsTrue();
        await Assert.That(() => DefinitionDeploymentScopeTests.Create(root =>
        {
            boundary(root);
            root["deployment"]!["recipes"]![0]!["scopes"]![1]!["prerequisitePointers"] = new JsonArray("/endpoint");
        })).Throws<DefinitionException>();
    }

    [Test]
    public async Task EnvironmentSurfaceRejectsAnInconsistentOrUnknownDialect()
    {
        foreach (Action<JsonNode> mutate in new Action<JsonNode>[]
        {
            output => { output["surface"] = "process-environment"; output["environmentDialect"] = "compose"; },
            output => { output["surface"] = "compose-input"; output["environmentDialect"] = "docker-run"; },
            output => output["surface"] = "compose-input",
            output => output["environmentDialect"] = "powershell"
        })
        {
            JsonObject source = JsonNode.Parse(DefinitionAuthoringTests.Source)!.AsObject();
            mutate(source["outputs"]![".env"]!);
            await Assert.That(() => DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())))
                .Throws<DefinitionException>();
        }
    }

    [Test]
    public async Task PrerequisitePointersRetainLiteralEscapedPropertyIdentity()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create(root =>
        {
            root["configuration"]!["properties"]!["a/b~c<>"] = new JsonObject { ["type"] = "boolean" };
            root["deployment"]!["recipes"]![0]!["scopes"]![0]!["prerequisitePointers"] = new JsonArray("/a~1b~0c<>");
        });
        await Assert.That(definition.Deployment!.Recipes[0].Scopes[0].PrerequisitePointers.Single())
            .IsEqualTo("/a~1b~0c<>");
    }

    private static void RenameOutput(JsonObject root, string fileName)
    {
        JsonObject outputs = root["outputs"]!.AsObject();
        JsonNode output = outputs["worker.json"]!.DeepClone();
        outputs.Remove("worker.json");
        outputs[fileName] = output;
    }
}
