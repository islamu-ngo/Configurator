using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class SetupCliContractTests
{
    [Test]
    public async Task GeneratedSchemaIsClosedBoundedDeterministicAndRegistryOwned()
    {
        byte[] first = SetupCliCommandSchemaMetadata.GenerateSchema();
        await Assert.That(SetupCliCommandSchemaMetadata.GenerateSchema()).IsEquivalentTo(first);
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(first);
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema)).IsEmpty();
        await Assert.That(schema["_metadata"]!["commands"]!.AsArray().Select(node => node!["name"]!.GetValue<string>()))
            .IsEquivalentTo(["catalogue", "env", "artifact", "doctor", "definition"]);
        await Assert.That(schema["_metadata"]!["commandOptions"]!.AsArray().Select(node => node!.GetValue<string>()))
            .IsEquivalentTo(["--help", "--machine", "--text", "--dry-run", "--input", "--baseline",
                "--output", "--key", "--definition", "--artifact", "--format", "--expected-revision",
                "--revision-field", "--input-revision", "--answers", "--source", "--root", "--against",
                "--id-map", "--project-id", "--name", "--application-version", "--fork-from", "--from-env-example",
                "--from-schema", "--accept-bounded-semantics", "--detail", "--offset", "--limit", "--expected-detail-digest"]);
        await Assert.That(schema["_metadata"]!["repeatableOptions"]!.AsArray().Select(node => node!.GetValue<string>()))
            .IsEquivalentTo(["--artifact"]);
        await Assert.That(schema["properties"]!["exitCode"]!["enum"]!.AsArray().Select(node => node!.GetValue<int>()))
            .IsEquivalentTo([0, 2, 3, 4, 64, 65, 70, 74, 130]);
    }

    [Test]
    public async Task SchemaVerifierRejectsUndeclaredArtifactOperation()
    {
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        JsonNode artifact = schema["_metadata"]!["commands"]!.AsArray()
            .Single(command => command!["name"]!.GetValue<string>() == "artifact")!;
        artifact["operations"]![0]!["name"] = "apply";
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema))
            .Contains("schema-operation-vocabulary");
    }

    [Test]
    public async Task SchemaVerifierRejectsLegacySummaryShapesAndUnboundedInlineDiagnostics()
    {
        JsonObject legacy = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        legacy["$defs"]!["artifact"]!["properties"]!["coverage"] = new JsonObject { ["$ref"] = "#/$defs/coverage" };
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(legacy)).Contains("schema-object-contract:$.artifacts[]");
        JsonObject unbounded = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        unbounded["properties"]!["diagnostics"]!["maxItems"] = 128;
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(unbounded)).Contains("schema-response-bounds");
    }

    [Test]
    public async Task MachineVerifierRejectsFramingControlsAndValueBearingObjects()
    {
        byte[] good = SetupCliMachineContractVerifier.GoodFixture();
        await Assert.That(SetupCliMachineContractVerifier.Validate(good)).IsEmpty();
        byte[][] vectors =
        [
            [.. good.AsSpan(0, good.Length - 1), .. "{}\n"u8],
            [0xEF, 0xBB, 0xBF, .. good],
            [0x1B, .. good],
            SetupCliMachineContractVerifier.Mutate(root => root["message"] = "localized"),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnostics"] = new JsonArray(new JsonObject
            {
                ["code"] = "invalid-artifact", ["path"] = "/input", ["severity"] = "error", ["value"] = "present"
            })),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["body"] = "present"),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["bytes"] = 1)
        ];
        foreach (byte[] vector in vectors)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task MachineVerifierRejectsDigestExitOrderingReadinessAndBoundsBreakers()
    {
        byte[][] vectors =
        [
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["digest"] = "ABC"),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["digest"] = new string('a', 64) + "\n"),
            SetupCliMachineContractVerifier.Mutate(root => root["status"] = "unknown"),
            SetupCliMachineContractVerifier.Mutate(root => root["exitCode"] = 4),
            SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["coverage"]!["coveredCount"] = 2;
                AddCoverageDetail(root)["coverage"] = new JsonArray(CoverageEntry("z"), CoverageEntry("a"));
            }),
            SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["coverage"]!["coveredCount"] = 2;
                AddCoverageDetail(root)["coverage"] = new JsonArray(CoverageEntry("a"), CoverageEntry("a"));
            }),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"] = new JsonObject
            {
                ["state"] = "ready", ["missingCount"] = 1, ["blockedCount"] = 0
            }),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnostics"] = new JsonArray(new JsonObject
            {
                ["code"] = new string('a', 97), ["path"] = "/input", ["severity"] = "error"
            }))
        ];
        foreach (byte[] vector in vectors)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task MachineVerifierRequiresEveryCompactSummaryField()
    {
        foreach (string name in new[] { "completion", "preparation", "targetAcceptance", "cleanup", "delivery",
            "diagnosticsTotal", "diagnosticsOmitted", "detailDigest" })
        {
            byte[] missing = SetupCliMachineContractVerifier.Mutate(root => root.Remove(name));
            await Assert.That(SetupCliMachineContractVerifier.Validate(missing)).Contains("machine-required:$");
        }
        byte[][] invalid =
        [
            SetupCliMachineContractVerifier.Mutate(root => root["diagnosticsTotal"] = 1),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnosticsOmitted"] = -1),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnosticsTotal"] = 4097),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["coveredCount"] = -1),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["missingCount"] = "1"),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["detailDigest"] = new string('a', 64)),
            SetupCliMachineContractVerifier.Mutate(root => root["detailDigest"] = "missing"),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["coveredKeys"] = new JsonArray("service.name")),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["coverage"] = root["coverage"]!.DeepClone()),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["readiness"] = root["readiness"]!.DeepClone())
        ];
        foreach (byte[] vector in invalid)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task MachineVerifierRejectsLifecycleAndReadinessContradictions()
    {
        byte[][] invalid =
        [
            SetupCliMachineContractVerifier.Mutate(root => root["completion"] = "cancelled"),
            SetupCliMachineContractVerifier.Mutate(root => root["completion"] = "failed"),
            SetupCliMachineContractVerifier.Mutate(root => root["preparation"] = "accepted"),
            SetupCliMachineContractVerifier.Mutate(root => root["targetAcceptance"] = "accepted"),
            SetupCliMachineContractVerifier.Mutate(root => root["delivery"] = "delivered"),
            SetupCliMachineContractVerifier.Mutate(root => root["cleanup"] = "success"),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"]!["state"] = "incomplete"),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"]!["state"] = "blocked"),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"]!["blockedCount"] = 1),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"]!["missingCount"] = -1),
            SetupCliMachineContractVerifier.Mutate(root => { root["readiness"]!["state"] = "unassessed"; root["readiness"]!["missingCount"] = 1; })
        ];
        foreach (byte[] vector in invalid)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);

        byte[] cancelled = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["status"] = "cancelled"; root["exitCategory"] = "cancelled"; root["exitCode"] = 130;
            root["completion"] = "cancelled"; root["preparation"] = null;
            root["readiness"]!["state"] = "unassessed";
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(cancelled)).IsEmpty();
        byte[] preparedButNotAccepted = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["preparation"] = "ready";
            root["readiness"]!["state"] = "incomplete";
            root["readiness"]!["missingCount"] = 1;
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(preparedButNotAccepted)).IsEmpty();
    }

    [Test]
    public async Task MachineVerifierAcceptsRfc6901PointersAndLocatedDiagnosticsButRejectsMalformedOnes()
    {
        foreach (string path in new[] { "", "/", "/fields/0/name", "/a~1b/~0", "/unicode/é" })
        {
            byte[] located = SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["diagnosticsTotal"] = 1;
                root["diagnostics"] = new JsonArray(new JsonObject
                {
                    ["code"] = "invalid-artifact", ["path"] = path, ["severity"] = "error",
                    ["line"] = 1, ["column"] = 1, ["byteOffset"] = 0
                });
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(located)).IsEmpty();
        }
        foreach (string path in new[] { "$.input", "input", "/bad~2escape", "/bad~", "/" + new string('x', 256) })
        {
            byte[] malformed = SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["diagnosticsTotal"] = 1;
                root["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "invalid", ["path"] = path, ["severity"] = "error" });
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(malformed)).Contains("machine-diagnostic-shape");
        }
        foreach (string name in new[] { "line", "column", "byteOffset" })
        {
            byte[] malformed = SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["diagnosticsTotal"] = 1;
                root["diagnostics"] = new JsonArray(new JsonObject
                { ["code"] = "invalid", ["path"] = "", ["severity"] = "error", [name] = -1 });
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(malformed)).Contains("machine-diagnostic-location");
        }
        byte[] nullPath = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["diagnosticsTotal"] = 1;
            root["diagnostics"] = new JsonArray(new JsonObject { ["code"] = "invalid", ["path"] = null, ["severity"] = "error" });
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(nullPath)).Contains("machine-diagnostic-shape");
    }

    [Test]
    public async Task MachineVerifierRejectsDetailPageBoundsDigestAndPayloadBreakers()
    {
        await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root => AddCoverageDetail(root)))).IsEmpty();
        Action<JsonObject>[] mutations =
        [
            detail => detail["offset"] = -1,
            detail => detail["offset"] = 2049,
            detail => detail["limit"] = 0,
            detail => detail["limit"] = 33,
            detail => detail["total"] = 4097,
            detail => detail["digest"] = new string('a', 64),
            detail => detail["kind"] = "answers",
            detail => detail["coverage"]![0]!["state"] = "ready",
            detail => detail["coverage"]![0]!["id"] = new string('x', 129),
            detail => detail["coverage"]![0]!["id"] = "é",
            detail => detail["coverage"]![0]!["value"] = "present",
            detail => detail["offset"] = 1,
            detail => detail["diagnostics"] = new JsonArray(new JsonObject
                { ["code"] = "invalid", ["path"] = "", ["severity"] = "error" })
        ];
        foreach (Action<JsonObject> mutation in mutations)
        {
            byte[] vector = SetupCliMachineContractVerifier.Mutate(root => mutation(AddCoverageDetail(root)));
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task MachineVerifierEnforcesSixteenInlineDiagnosticsAndSharedDetailByteBudget()
    {
        JsonObject Diagnostic(int pathLength = 0) => new()
        {
            ["code"] = "invalid-artifact", ["path"] = pathLength == 0 ? "" : "/" + new string('é', pathLength - 1), ["severity"] = "error"
        };
        byte[] bounded = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["diagnosticsTotal"] = 32;
            root["diagnosticsOmitted"] = 16;
            root["diagnostics"] = new JsonArray(Enumerable.Range(0, 16).Select(_ => (JsonNode)Diagnostic()).ToArray());
            root["detail"] = new JsonObject
            {
                ["kind"] = "diagnostics", ["offset"] = 0, ["limit"] = 32, ["total"] = 32,
                ["digest"] = root["detailDigest"]!.DeepClone(), ["coverage"] = new JsonArray(),
                ["diagnostics"] = new JsonArray(Enumerable.Range(0, 32).Select(_ => (JsonNode)Diagnostic()).ToArray())
            };
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(bounded)).IsEmpty();
        byte[] inlineOverflow = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["diagnosticsTotal"] = 17;
            root["diagnostics"] = new JsonArray(Enumerable.Range(0, 17).Select(_ => (JsonNode)Diagnostic()).ToArray());
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(inlineOverflow)).Contains("machine-diagnostic-count");
        JsonObject oversized = JsonNode.Parse(bounded)!.AsObject();
        oversized["detail"]!["diagnostics"] = new JsonArray(Enumerable.Range(0, 32).Select(_ => (JsonNode)Diagnostic(256)).ToArray());
        byte[] detailOverflow = System.Text.Encoding.UTF8.GetBytes(oversized.ToJsonString() + "\n");
        await Assert.That(SetupCliMachineContractVerifier.Validate(detailOverflow)).Contains("machine-detail-bounds");
        await Assert.That(SetupCliMachineContractVerifier.Validate([.. bounded, .. new byte[65_536]])).Contains("machine-framing");
    }

    private static JsonObject AddCoverageDetail(JsonObject root)
    {
        var detail = new JsonObject
        {
            ["kind"] = "coverage", ["offset"] = 0, ["limit"] = 16,
            ["total"] = root["coverage"]!["coveredCount"]!.GetValue<int>() + root["coverage"]!["missingCount"]!.GetValue<int>(),
            ["digest"] = root["detailDigest"]!.DeepClone(), ["diagnostics"] = new JsonArray(),
            ["coverage"] = new JsonArray(CoverageEntry("service.name"))
        };
        root["detail"] = detail;
        return detail;
    }

    private static JsonObject CoverageEntry(string id) => new() { ["id"] = id, ["state"] = "covered" };

    [Test]
    public async Task ActualCommandGrammarRejectsAliasesUnknownDuplicateMixedAndMissingOptions()
    {
        string[][] rejected =
        [
            ["manifest", "create", "--machine"],
            ["tenant-package", "validate", "--machine"],
            ["portability", "export-operator-identity", "--machine"],
            ["legal", "preview", "--machine"],
            ["catalog", "list", "--machine"],
            ["tui", "--machine"],
            ["artifact", "show", "--machine"],
            ["doctor", "--machine", "--text"],
            ["doctor", "--machine", "--machine"],
            ["doctor", "--machine", "--unknown"],
            ["doctor", "--machine", "tail"],
            ["catalogue", "list", "--machine"],
            ["catalogue", "show", "--definition", "definition.json", "--dry-run", "--machine"],
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config", "--input", "answers.json", "--machine"],
            ["artifact", "validate", "--definition", "-", "--artifact", "config", "--input", "answers.json", "--machine"],
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config", "--input", "-", "--baseline", "-", "--machine"],
            ["artifact", "bundle-check", "--definition", "definition.json", "--artifacts", "config,yaml", "--input", "answers.json", "--machine"]
        ];
        foreach (string[] arguments in rejected)
        {
            var io = new CliFixture();
            await Assert.That(await io.RunAsync(arguments)).IsEqualTo(SetupCliExitCode.Usage);
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            await Assert.That(io.StandardError).IsEmpty();
        }
    }

    [Test]
    public async Task HelpBypassesInputsButStillHasOneMachineEnvelope()
    {
        foreach (var family in SetupCliCommandSchemaMetadata.Commands)
            foreach (var operation in family.Operations)
            {
                var io = new CliFixture();
                string[] args = family.Name == "doctor"
                    ? ["doctor", "--help", "--machine"] : [family.Name, operation.Name, "--help", "--machine"];
                await Assert.That(await io.RunAsync(args)).IsEqualTo(SetupCliExitCode.Success);
                await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            }
    }

    [Test]
    public async Task CompiledCliClosureHasNoEventOrTransportAssembly()
    {
        var pending = new Queue<System.Reflection.Assembly>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(typeof(SetupCliApplication).Assembly);
        while (pending.TryDequeue(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                string name = reference.Name!;
                await Assert.That(name.StartsWith("Event.", StringComparison.Ordinal)
                    || name.StartsWith("Explore.", StringComparison.Ordinal)
                    || name.Contains("Wire.Contracts", StringComparison.Ordinal)
                    || name.Contains("SetupLive", StringComparison.Ordinal)).IsFalse();
                if (name.StartsWith("Configurator.", StringComparison.Ordinal) && visited.Add(name))
                    pending.Enqueue(System.Reflection.Assembly.Load(reference));
            }
        }
        await Assert.That(typeof(SetupCliApplication).Assembly.EntryPoint).IsNotNull();
        await Assert.That(SetupCliExecutableMarker.ExecutableName).IsEqualTo("islamu-configurator");
    }

    [Test]
    public async Task MachineReadinessNeverClaimsHostAcceptance()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("open")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument result = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(result.RootElement.GetProperty("readiness").GetProperty("state").GetString()).IsEqualTo("incomplete");
        await Assert.That(result.RootElement.GetProperty("readiness").GetProperty("missingCount").GetInt32()).IsGreaterThan(0);
        await Assert.That(result.RootElement.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("sensitivity").GetString())
            .IsEqualTo("restricted");
    }
}
