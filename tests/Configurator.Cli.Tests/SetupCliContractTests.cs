using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class SetupCliContractTests
{
    private static readonly string[] SourceFields = ["schemaPath", "documentIndex", "startLine", "startColumn", "endLine", "endColumn"];
    private static readonly string[] CorrectionFields = ["inputRevision", "fieldId", "segments"];
    private static readonly string[] SchemaFields = ["kind", "id", "digest", "profile", "authoringContractVersion",
        "runtimeSchemaVersion", "answerContractVersion", "capabilities", "document"];

    [Test]
    public async Task GeneratedSchemaIsClosedBoundedDeterministicAndRegistryOwned()
    {
        byte[] first = SetupCliCommandSchemaMetadata.GenerateSchema();
        await Assert.That(SetupCliCommandSchemaMetadata.GenerateSchema().AsSpan().SequenceEqual(first)).IsTrue();
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(first);
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema)).IsEmpty();
        await Assert.That(schema["_metadata"]!["commands"]!.AsArray().Select(node => node!["name"]!.GetValue<string>()))
            .IsEquivalentTo(["catalogue", "env", "artifact", "doctor", "definition"]);
        await Assert.That(schema["_metadata"]!["commandOptions"]!.AsArray().Select(node => node!.GetValue<string>()))
            .IsEquivalentTo(["--help", "--machine", "--text", "--dry-run", "--input", "--baseline",
                "--output", "--key", "--definition", "--artifact", "--format", "--expected-revision",
                "--revision-field", "--input-revision", "--answers", "--source", "--root", "--against",
                "--id-map", "--project-id", "--name", "--application-version", "--fork-from", "--from-env-example",
                "--from-schema", "--accept-bounded-semantics", "--kind", "--detail", "--offset", "--limit", "--expected-detail-digest"]);
        await Assert.That(schema["_metadata"]!["repeatableOptions"]!.AsArray().Select(node => node!.GetValue<string>()))
            .IsEquivalentTo(["--artifact"]);
        await Assert.That(schema["properties"]!["exitCode"]!["enum"]!.AsArray().Select(node => node!.GetValue<int>()))
            .IsEquivalentTo([0, 2, 3, 4, 64, 65, 70, 74, 130]);
        JsonArray definition = schema["_metadata"]!["commands"]!.AsArray()
            .Single(command => command!["name"]!.GetValue<string>() == "definition")!["operations"]!.AsArray();
        JsonArray check = definition.Single(operation => operation!["name"]!.GetValue<string>() == "check")!["options"]!.AsArray();
        JsonArray lint = definition.Single(operation => operation!["name"]!.GetValue<string>() == "lint")!["options"]!.AsArray();
        await Assert.That(lint.Select(option => option!.GetValue<string>()))
            .IsEquivalentTo(check.Select(option => option!.GetValue<string>()));
        JsonArray discovery = definition.Single(operation => operation!["name"]!.GetValue<string>() == "schema")!["options"]!.AsArray();
        await Assert.That(discovery.Select(option => option!.GetValue<string>()))
            .IsEquivalentTo(["--help", "--kind", "--machine", "--text"]);
    }

    [Test]
    public async Task MachineVersionThreeRejectsRetiredAndUnknownEnvelopesWithoutCompatibility()
    {
        foreach (string version in new[] { "islamu-configurator-command/v1", "islamu-configurator-command/v2",
            "islamu-configurator-command/v4", "2", "" })
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(
                root => root["schemaVersion"] = version))).Contains("machine-version");
        foreach (string operation in new[] { "check", "build", "lint" })
        {
            byte[] valid = SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["invocation"]!["commandFamily"] = "definition";
                root["invocation"]!["operation"] = operation;
                root["authoringContractVersion"] = 2;
                root["runtimeSchemaVersion"] = 2;
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(valid)).IsEmpty();
            JsonObject old = JsonNode.Parse(valid)!.AsObject();
            old["authoringContractVersion"] = 1;
            await Assert.That(SetupCliMachineContractVerifier.Validate(System.Text.Encoding.UTF8.GetBytes(old.ToJsonString() + "\n")))
                .Contains("machine-compiler-handshake");
        }
    }

    [Test]
    public async Task StructuredDiagnosticLocationsAreClosedBoundedAndMutuallyExclusive()
    {
        await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            AddDiagnostic(root)["source"] = SourceLocation()))).IsEmpty();
        await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            AddDiagnostic(root)["correction"] = CorrectionLocation()))).IsEmpty();
        Action<JsonObject>[] sourceMutations =
        [
            source => source["schemaPath"] = "/bad~2",
            source => source["schemaPath"] = new string('x', 257),
            source => source["schemaPath"] = null,
            source => source["documentIndex"] = -1,
            source => source["documentIndex"] = (long)int.MaxValue + 1,
            source => source["startLine"] = 0,
            source => source["startColumn"] = "1",
            source => source["endLine"] = 0,
            source => source["endColumn"] = 1,
            source => source["endColumn"] = 1.5,
            source => source["file"] = "private-path",
            source => source["value"] = "private-answer-marker"
        ];
        foreach (Action<JsonObject> mutation in sourceMutations.Concat(
            SourceFields.Select<string, Action<JsonObject>>(name => source => source.Remove(name))))
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            {
                JsonObject source = SourceLocation();
                mutation(source);
                AddDiagnostic(root)["source"] = source;
            })).Count).IsGreaterThan(0);
        Action<JsonObject>[] correctionMutations =
        [
            correction => correction["inputRevision"] = -1,
            correction => correction["inputRevision"] = 9223372036854775808m,
            correction => correction["fieldId"] = new string('a', 129),
            correction => correction["fieldId"] = "/private",
            correction => correction["segments"] = null,
            correction => correction["segments"] = new JsonArray(Enumerable.Range(0, 33)
                .Select(_ => (JsonNode?)new JsonObject { ["kind"] = "arrayRow", ["index"] = 0 }).ToArray()),
            correction => correction["key"] = "private-answer-marker"
        ];
        foreach (Action<JsonObject> mutation in correctionMutations.Concat(
            CorrectionFields.Select<string, Action<JsonObject>>(name => correction => correction.Remove(name))))
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            {
                JsonObject correction = CorrectionLocation();
                mutation(correction);
                AddDiagnostic(root)["correction"] = correction;
            })).Count).IsGreaterThan(0);
        await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
        {
            JsonObject diagnostic = AddDiagnostic(root);
            diagnostic["source"] = SourceLocation();
            diagnostic["correction"] = CorrectionLocation();
        }))).Contains("machine-diagnostic-location-kind");
        foreach (string name in new[] { "source", "correction" })
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
                AddDiagnostic(root)[name] = null)).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task TypedCorrectionSegmentsRejectAmbiguousDynamicAndUnknownShapes()
    {
        JsonObject[] rejected =
        [
            new() { ["kind"] = "property" },
            new() { ["kind"] = "property", ["propertyName"] = "declared", ["index"] = 0 },
            new() { ["kind"] = "property", ["propertyName"] = null },
            new() { ["kind"] = "property", ["propertyName"] = 1 },
            new() { ["kind"] = "property", ["propertyName"] = new string('a', 65537) },
            new() { ["kind"] = "arrayRow" },
            new() { ["kind"] = "arrayRow", ["index"] = -1 },
            new() { ["kind"] = "arrayRow", ["index"] = 4096 },
            new() { ["kind"] = "arrayRow", ["index"] = 1.5 },
            new() { ["kind"] = "arrayRow", ["index"] = 0, ["propertyName"] = "" },
            new() { ["kind"] = "mapEntry", ["index"] = "0" },
            new() { ["kind"] = "mapEntry", ["index"] = 0, ["key"] = "private-answer-marker" },
            new() { ["kind"] = "map-entry", ["index"] = 0 },
            new() { ["index"] = 0 }
        ];
        foreach (JsonObject segment in rejected)
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            {
                JsonObject correction = CorrectionLocation();
                correction["segments"] = new JsonArray(segment.DeepClone());
                AddDiagnostic(root)["correction"] = correction;
            })).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task SchemaDiscoveryRequiresItsOwnAxesKnownIdentityAndCanonicalDocumentDigest()
    {
        foreach (string kind in SetupCliContractSpecification.SchemaIds.Keys)
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(
                root => AddSchemaDiscovery(root, kind)))).IsEmpty();
        Action<JsonObject>[] mutations =
        [
            root => root.Remove("schema"),
            root => root["schema"] = null,
            root => root["schema"]!["kind"] = "capabilities",
            root => root["schema"]!["id"] = "urn:islamu:configurator:answers:v1",
            root => root["schema"]!["digest"] = new string('0', 64),
            root => root["schema"]!["digest"] = new string('A', 64),
            root => root["schema"]!["profile"] = "bounded-v1",
            root => root["schema"]!["authoringContractVersion"] = 1,
            root => root["schema"]!["runtimeSchemaVersion"] = 1,
            root => root["schema"]!["answerContractVersion"] = 2,
            root => root["schema"]!["capabilities"] = new JsonArray("schema.unknown"),
            root => root["schema"]!["capabilities"] = new JsonArray("schema.object", "schema.object"),
            root => root["schema"]!["capabilities"] = new JsonArray(7),
            root => root["schema"]!["document"] = new JsonArray(),
            root => root["schema"]!["document"]!["$id"] = "unknown",
            root => root["schema"]!["document"]!["type"] = "array",
            root => root["schema"]!["body"] = "private-answer-marker",
            root => root["authoringContractVersion"] = 2,
            root => root["invocation"]!["operation"] = "check",
            root => { root["exitCode"] = 2; root["exitCategory"] = "validation"; root["status"] = "validation"; root["completion"] = "failed"; },
            root => root["help"] = new JsonObject()
        ];
        foreach (Action<JsonObject> mutation in mutations.Concat(
            SchemaFields.Select<string, Action<JsonObject>>(name => root => root["schema"]!.AsObject().Remove(name))))
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            {
                AddSchemaDiscovery(root, "authoring");
                mutation(root);
            })).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task SchemaVerifierRejectsWeakenedStructuredFieldContracts()
    {
        Action<JsonObject>[] mutations =
        [
            schema => schema["$defs"]!["source"]!["additionalProperties"] = true,
            schema => schema["$defs"]!["source"]!["properties"]!["schemaPath"]!["maxLength"] = 257,
            schema => schema["$defs"]!["diagnostic"]!.AsObject().Remove("not"),
            schema => schema["$defs"]!["correction"]!["properties"]!["segments"]!["maxItems"] = 33,
            schema => schema["$defs"]!["correctionSegment"]!.AsObject().Remove("oneOf"),
            schema => schema["$defs"]!["correctionSegment"]!["properties"]!["index"]!["maximum"] = 4096,
            schema => schema["$defs"]!["schema"]!["properties"]!["answerContractVersion"]!["const"] = 2,
            schema => schema["$defs"]!["schema"]!["properties"]!["document"]!["$ref"] = "#/$defs/source",
            schema => schema["$defs"]!["schema"]!.AsObject().Remove("oneOf")
        ];
        foreach (Action<JsonObject> mutation in mutations)
        {
            JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
            mutation(schema);
            await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema).Count).IsGreaterThan(0);
        }
    }

    private static JsonObject SourceLocation() => new()
    {
        ["schemaPath"] = "/configuration/properties/a~1b~0c",
        ["documentIndex"] = 0,
        ["startLine"] = 1,
        ["startColumn"] = 2,
        ["endLine"] = 1,
        ["endColumn"] = 3
    };

    private static JsonObject CorrectionLocation() => new()
    {
        ["inputRevision"] = long.MaxValue,
        ["fieldId"] = "",
        ["segments"] = new JsonArray(
            new JsonObject { ["kind"] = "property", ["propertyName"] = "" },
            new JsonObject { ["kind"] = "arrayRow", ["index"] = 0 },
            new JsonObject { ["kind"] = "mapEntry", ["index"] = 4095 })
    };

    private static JsonObject AddDiagnostic(JsonObject root)
    {
        var diagnostic = new JsonObject { ["code"] = "invalid-source", ["path"] = "", ["severity"] = "error" };
        root["diagnostics"] = new JsonArray(diagnostic);
        root["diagnosticsTotal"] = 1;
        return diagnostic;
    }

    private static void AddSchemaDiscovery(JsonObject root, string kind)
    {
        string id = SetupCliContractSpecification.SchemaIds[kind];
        // This literal ordering is canonical independently of the production canonicalizer.
        string document = $$"""{"$id":"{{id}}","$schema":"https://json-schema.org/draft/2020-12/schema","type":"object"}""";
        root["invocation"]!["commandFamily"] = "definition";
        root["invocation"]!["operation"] = "schema";
        root["schema"] = new JsonObject
        {
            ["kind"] = kind,
            ["id"] = id,
            ["digest"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(document))),
            ["profile"] = "bounded-v2",
            ["authoringContractVersion"] = 2,
            ["runtimeSchemaVersion"] = 2,
            ["answerContractVersion"] = 1,
            ["capabilities"] = new JsonArray("schema.object"),
            ["document"] = JsonNode.Parse(document)
        };
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
    public async Task MachineContractRequiresIndependentNativeFactsAndRejectsStdoutInstallationClaims()
    {
        foreach (string name in new[] { "installation", "verification" })
        {
            byte[] missing = SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!.AsObject().Remove(name));
            await Assert.That(SetupCliMachineContractVerifier.Validate(missing)).Contains("machine-required:$.artifacts[]");
        }
        byte[] stdoutClaim = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["artifacts"]![0]!["pathIntent"] = "stdout";
            root["artifacts"]![0]!["writeStatus"] = "written";
            root["artifacts"]![0]!["installation"] = "established";
            root["artifacts"]![0]!["verification"] = "succeeded";
        });
        await Assert.That(SetupCliMachineContractVerifier.Validate(stdoutClaim)).Contains("machine-publication-facts");
        foreach ((string installation, string verification) in new[]
        {
            ("established", "not-attempted"), ("not-established", "succeeded"), ("not-attempted", "failed"), ("unknown", "succeeded")
        })
        {
            byte[] inconsistent = SetupCliMachineContractVerifier.Mutate(root =>
            {
                root["artifacts"]![0]!["pathIntent"] = "output";
                root["artifacts"]![0]!["writeStatus"] = "written";
                root["artifacts"]![0]!["installation"] = installation;
                root["artifacts"]![0]!["verification"] = verification;
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(inconsistent)).Contains("machine-publication-facts");
        }
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        schema["$defs"]!["artifact"]!["properties"]!["verification"]!["enum"] = new JsonArray("succeeded", "unknown");
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema)).Contains("schema-publication-vocabulary");
        schema["$defs"]!["artifact"]!["properties"]!.AsObject().Remove("installation");
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema)).Contains("schema-object-contract:$.artifacts[]");
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
                    ["code"] = "invalid-artifact",
                    ["path"] = path,
                    ["severity"] = "error",
                    ["line"] = 1,
                    ["column"] = 1,
                    ["byteOffset"] = 0
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
            ["code"] = "invalid-artifact",
            ["path"] = pathLength == 0 ? "" : "/" + new string('é', pathLength - 1),
            ["severity"] = "error"
        };
        byte[] bounded = SetupCliMachineContractVerifier.Mutate(root =>
        {
            root["diagnosticsTotal"] = 32;
            root["diagnosticsOmitted"] = 16;
            root["diagnostics"] = new JsonArray(Enumerable.Range(0, 16).Select(_ => (JsonNode)Diagnostic()).ToArray());
            root["detail"] = new JsonObject
            {
                ["kind"] = "diagnostics",
                ["offset"] = 0,
                ["limit"] = 32,
                ["total"] = 32,
                ["digest"] = root["detailDigest"]!.DeepClone(),
                ["coverage"] = new JsonArray(),
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
            ["kind"] = "coverage",
            ["offset"] = 0,
            ["limit"] = 16,
            ["total"] = root["coverage"]!["coveredCount"]!.GetValue<int>() + root["coverage"]!["missingCount"]!.GetValue<int>(),
            ["digest"] = root["detailDigest"]!.DeepClone(),
            ["diagnostics"] = new JsonArray(),
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
    public async Task MachineHelpVerifierRejectsOpenUntypedOversizedAndMisidentifiedPayloads()
    {
        byte[] rootHelp = SetupCliMachineContractVerifier.Mutate(AddRootHelp);
        await Assert.That(SetupCliMachineContractVerifier.Validate(rootHelp)).IsEmpty();
        Action<JsonObject>[] mutations =
        [
            root => root.Remove("help"),
            root => root["help"] = null,
            root => root["help"]!["body"] = "human help",
            root => root["help"]!["scope"] = "unknown",
            root => root["help"]!["commandPath"] = new JsonArray("artifact"),
            root => root["help"]!["commands"] = new JsonArray(),
            root => root["help"]!["commands"]!.AsArray().Add("cli"),
            root => root["help"]!["commands"]!.AsArray().Add("doctor"),
            root => root["help"]!["commands"] = new JsonArray(Enumerable.Range(0, 33).Select(_ => (JsonNode?)JsonValue.Create("doctor")).ToArray()),
            root => root["help"]!["options"] = new JsonArray(new JsonObject
                { ["name"] = "--machine", ["requiresValue"] = false, ["repeatable"] = false }),
            root => root["invocation"]!["commandFamily"] = "doctor"
        ];
        foreach (Action<JsonObject> mutation in mutations)
            await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineContractVerifier.Mutate(root =>
            {
                AddRootHelp(root);
                mutation(root);
            })).Count).IsGreaterThan(0);

        Action<JsonObject>[] optionMutations =
        [
            option => option.Remove("requiresValue"),
            option => option["requiresValue"] = "false",
            option => option["repeatable"] = 0,
            option => option["name"] = "--" + new string('x', 63),
            option => option["name"] = "--help\n",
            option => option["value"] = "private-answer-marker"
        ];
        foreach (Action<JsonObject> mutation in optionMutations)
        {
            byte[] malformed = SetupCliMachineContractVerifier.Mutate(root =>
            {
                AddRootHelp(root);
                root["invocation"]!["commandFamily"] = "doctor";
                root["invocation"]!["operation"] = "doctor";
                root["help"]!["scope"] = "leaf";
                root["help"]!["commandPath"] = new JsonArray("doctor");
                root["help"]!["commands"] = new JsonArray();
                var option = new JsonObject { ["name"] = "--help", ["requiresValue"] = false, ["repeatable"] = false };
                mutation(option);
                root["help"]!["options"] = new JsonArray(option);
            });
            await Assert.That(SetupCliMachineContractVerifier.Validate(malformed).Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task SchemaHelpContractRejectsUntypedOptionsAndUnboundedPayloads()
    {
        JsonObject untyped = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        untyped["$defs"]!["helpOption"]!["properties"]!["requiresValue"]!["type"] = "string";
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(untyped)).Contains("schema-help-contract");
        JsonObject unbounded = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        unbounded["$defs"]!["help"]!["properties"]!["options"]!["maxItems"] = 65;
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(unbounded)).Contains("schema-help-contract");
        JsonObject open = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        open["$defs"]!["help"]!["additionalProperties"] = true;
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(open).Count).IsGreaterThan(0);
    }

    private static void AddRootHelp(JsonObject root)
    {
        root["invocation"] = new JsonObject { ["commandFamily"] = "cli", ["operation"] = "help", ["mode"] = "machine" };
        root["preparation"] = null;
        root["artifacts"] = new JsonArray();
        root["coverage"]!["coveredCount"] = 0;
        root["help"] = new JsonObject
        {
            ["scope"] = "root",
            ["commandPath"] = new JsonArray(),
            ["commands"] = new JsonArray("catalogue", "artifact", "env", "definition", "doctor"),
            ["options"] = new JsonArray()
        };
    }

    [Test]
    public async Task MachineHelpDescribesRootBranchAndLeafWithoutReadingOrPublishingInputs()
    {
        (string[] Arguments, string Family, string Operation, string Scope, string[] Path, string[] Commands)[] cases =
        [
            (["--help", "--machine"], "cli", "help", "root", [], ["catalogue", "artifact", "env", "definition", "doctor"]),
            (["artifact", "--help", "--machine"], "artifact", "help", "branch", ["artifact"],
                ["create", "open", "validate", "format", "diff", "coverage", "export", "import", "check-existing", "prefill", "bundle-check", "bundle-create", "preview"]),
            (["artifact", "bundle-create", "--help", "--machine"], "artifact", "bundle-create", "leaf", ["artifact", "bundle-create"], [])
        ];
        foreach (var item in cases)
        {
            var io = new CliFixture();
            string[] originalFiles = io.Files.Keys.ToArray();
            await Assert.That(await io.RunAsync(item.Arguments)).IsEqualTo(SetupCliExitCode.Success);
            using JsonDocument response = JsonDocument.Parse(io.StandardOutput);
            JsonElement root = response.RootElement;
            await Assert.That(root.GetProperty("invocation").GetProperty("commandFamily").GetString()).IsEqualTo(item.Family);
            await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo(item.Operation);
            JsonElement help = root.GetProperty("help");
            await Assert.That(help.GetProperty("scope").GetString()).IsEqualTo(item.Scope);
            await Assert.That(help.GetProperty("commandPath").EnumerateArray().Select(value => value.GetString()!))
                .IsEquivalentTo(item.Path);
            await Assert.That(help.GetProperty("commands").EnumerateArray().Select(value => value.GetString()!))
                .IsEquivalentTo(item.Commands);
            if (item.Scope == "leaf")
            {
                JsonElement artifact = help.GetProperty("options").EnumerateArray()
                    .Single(option => option.GetProperty("name").GetString() == "--artifact");
                await Assert.That(artifact.GetProperty("requiresValue").GetBoolean()).IsTrue();
                await Assert.That(artifact.GetProperty("repeatable").GetBoolean()).IsTrue();
                JsonElement machine = help.GetProperty("options").EnumerateArray()
                    .Single(option => option.GetProperty("name").GetString() == "--machine");
                await Assert.That(machine.GetProperty("requiresValue").GetBoolean()).IsFalse();
                await Assert.That(machine.GetProperty("repeatable").GetBoolean()).IsFalse();
            }
            await Assert.That(root.GetProperty("artifacts").GetArrayLength()).IsEqualTo(0);
            await Assert.That(io.MaximumRead).IsEqualTo(0);
            await Assert.That(io.Files.Keys).IsEquivalentTo(originalFiles);
            await Assert.That(io.StandardError).IsEmpty();
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        }
    }

    [Test]
    public async Task EveryPublicLeafHasBoundedTypedMachineHelpWithItsActualRoute()
    {
        foreach (var family in SetupCliContractSpecification.Operations)
            foreach (string operation in family.Value)
            {
                var io = new CliFixture();
                string[] path = family.Key == "doctor" ? ["doctor"] : [family.Key, operation];
                await Assert.That(await io.RunAsync([.. path, "--help", "--machine"]))
                    .IsEqualTo(SetupCliExitCode.Success);
                using JsonDocument response = JsonDocument.Parse(io.StandardOutput);
                JsonElement root = response.RootElement;
                await Assert.That(root.GetProperty("invocation").GetProperty("commandFamily").GetString()).IsEqualTo(family.Key);
                await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo(operation);
                JsonElement help = root.GetProperty("help");
                await Assert.That(help.GetProperty("scope").GetString()).IsEqualTo("leaf");
                await Assert.That(help.GetProperty("commandPath").EnumerateArray().Select(value => value.GetString()!))
                    .IsEquivalentTo(path);
                await Assert.That(help.GetProperty("options").GetArrayLength()).IsGreaterThan(0);
                await Assert.That(help.GetProperty("options").GetArrayLength()).IsLessThanOrEqualTo(64);
                await Assert.That(help.GetProperty("commands").GetArrayLength()).IsEqualTo(0);
                await Assert.That(io.StandardOutput.Length).IsLessThanOrEqualTo(65_536);
                await Assert.That(io.MaximumRead).IsEqualTo(0);
                await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
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
