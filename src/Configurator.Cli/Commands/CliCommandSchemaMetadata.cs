using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core;

namespace Configurator.Cli;

public static class CliCommandSchemaMetadata
{
    public const string RelativePath = "schemas/configurator-cli.schema.json";
    public const string SchemaId = "urn:islamu:configurator:cli:v" + ConfiguratorVersion.Current;
    public const string EnvelopeVersion = "islamu-configurator-command/v" + ConfiguratorVersion.Current;
    public static IReadOnlyList<CliRegisteredCommandMetadata> Commands { get; } =
        CliCommandRegistry.Families.Select(family => new CliRegisteredCommandMetadata(
                family.Name,
                family.Operations.Select(operation => new CliRegisteredOperationMetadata(
                    operation.Name,
                    operation.Options.Select(option => option.Name)
                        .Append("--help")
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray())).ToArray(),
                family.Operations.SelectMany(operation => operation.Options.Select(option => option.Name))
                    .Append("--help")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    public static IReadOnlyList<string> Families { get; } =
        Commands.Select(command => command.Name).ToArray();
    public static IReadOnlyList<string> Operations { get; } =
        Commands.SelectMany(command => command.Operations.Select(operation => operation.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    public static IReadOnlyList<string> Options { get; } =
        Commands.SelectMany(command => command.Options)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static byte[] GenerateSchema()
    {
        JsonObject definitions = new()
        {
            ["invocation"] = ObjectSchema(["commandFamily", "operation", "mode"], new()
            {
                ["commandFamily"] = Enum(Families.Append("cli")),
                ["operation"] = Enum(Operations.Append("help").Append("version").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                ["mode"] = Enum(["machine"])
            }),
            ["help"] = ObjectSchema(["scope", "commandPath", "commands", "options"], new()
            {
                ["scope"] = Enum(["root", "branch", "leaf"]),
                ["commandPath"] = new JsonObject
                {
                    ["type"] = "array",
                    ["maxItems"] = 2,
                    ["items"] = String(1, 64, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
                },
                ["commands"] = new JsonObject
                {
                    ["type"] = "array",
                    ["maxItems"] = 32,
                    ["uniqueItems"] = true,
                    ["items"] = String(1, 64, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
                },
                ["options"] = BoundedArray("helpOption", 64)
            }),
            ["helpOption"] = ObjectSchema(["name", "requiresValue", "repeatable"], new()
            {
                ["name"] = String(3, 64, "^--[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                ["requiresValue"] = new JsonObject { ["type"] = "boolean" },
                ["repeatable"] = new JsonObject { ["type"] = "boolean" }
            }),
            ["diagnostic"] = ObjectSchema(["code", "path", "severity"], new()
            {
                ["code"] = String(1, 96, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                ["path"] = String(0, 256, "^(?:/(?:[^~/\\x00-\\x1F\\x7F]|~[01])*)*$"),
                ["severity"] = Enum(["info", "warning", "error"]),
                ["line"] = Integer(1, int.MaxValue),
                ["column"] = Integer(1, int.MaxValue),
                ["byteOffset"] = Integer(0, long.MaxValue),
                ["source"] = Ref("source"),
                ["correction"] = Ref("correction")
            }),
            ["source"] = ObjectSchema(["schemaPath", "documentIndex", "startLine", "startColumn", "endLine", "endColumn"], new()
            {
                ["schemaPath"] = String(0, 256, "^(?:/(?:[^~/\\x00-\\x1F\\x7F]|~[01])*)*$"),
                ["documentIndex"] = Integer(0, int.MaxValue),
                ["startLine"] = Integer(1, int.MaxValue),
                ["startColumn"] = Integer(1, int.MaxValue),
                ["endLine"] = Integer(1, int.MaxValue),
                ["endColumn"] = Integer(1, int.MaxValue)
            }),
            ["correction"] = ObjectSchema(["inputRevision", "fieldId", "segments"], new()
            {
                ["inputRevision"] = Integer(0, long.MaxValue),
                ["fieldId"] = String(0, 128, "^(?:[A-Za-z][A-Za-z0-9._-]*)?$"),
                ["segments"] = BoundedArray("correctionSegment", 32)
            }),
            ["correctionSegment"] = ObjectSchema(["kind"], new()
            {
                ["kind"] = Enum(["property", "arrayRow", "mapEntry"]),
                ["propertyName"] = String(0, 65536, "^[\\s\\S]*$"),
                ["index"] = Integer(0, 4095)
            }),
            ["schema"] = ObjectSchema(["kind", "id", "digest", "profile", "authoringContractVersion",
                "runtimeSchemaVersion", "answerContractVersion", "capabilities", "document"], new()
                {
                    ["kind"] = Enum(["answers", "authoring", "command", "fixtures", "runtime", "store-curation"]),
                    ["id"] = Enum([
                        "urn:islamu:configurator:answers:v0.0.1",
                        "urn:islamu:configurator:authoring:v0.0.1",
                        SchemaId,
                        Configurator.Core.Definitions.Authoring.DefinitionFixtureDocument.SchemaId,
                        "urn:islamu:configurator:setup-definition:v0.0.1",
                        CliDiscoveryHandlers.StoreCurationSchemaId
                    ]),
                    ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["profile"] = new JsonObject { ["const"] = "bounded-v0.0.1" },
                    ["authoringContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["runtimeSchemaVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["answerContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["capabilities"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 64,
                        ["uniqueItems"] = true,
                        ["items"] = Enum(Configurator.Core.Definitions.DefinitionProfile.Capabilities)
                    },
                    ["document"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["$ref"] = "https://json-schema.org/draft/2020-12/schema"
                    }
                }),
            ["schemaDescriptor"] = ObjectSchema(["kind", "id", "fileName", "digest"], new()
            {
                ["kind"] = Enum(["answers", "authoring", "command", "fixtures", "runtime", "store-curation"]),
                ["id"] = Enum([
                    "urn:islamu:configurator:answers:v0.0.1",
                    "urn:islamu:configurator:authoring:v0.0.1",
                    SchemaId,
                    Configurator.Core.Definitions.Authoring.DefinitionFixtureDocument.SchemaId,
                    "urn:islamu:configurator:setup-definition:v0.0.1",
                    CliDiscoveryHandlers.StoreCurationSchemaId
                ]),
                ["fileName"] = String(1, 128, "^[a-z0-9][a-z0-9.-]*\\.schema\\.json$"),
                ["digest"] = String(null, null, "^[0-9a-f]{64}$")
            }),
            ["schemaCatalog"] = ObjectSchema(["schemas"], new()
            {
                ["schemas"] = BoundedArray("schemaDescriptor", 16)
            }),
            ["info"] = ObjectSchema(["application", "version", "profile", "authoringContractVersion",
                "runtimeSchemaVersion", "answerContractVersion", "fixtureContractVersion", "commandEnvelopeVersion",
                "protectedWriteAvailable", "schemas", "capabilities"], new()
                {
                    ["application"] = new JsonObject { ["const"] = CliExecutableMarker.ExecutableName },
                    ["version"] = String(1, 128, "^[^\\s\\x00-\\x1F\\x7F]+$"),
                    ["profile"] = new JsonObject { ["const"] = "bounded-v0.0.1" },
                    ["authoringContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["runtimeSchemaVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["answerContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["fixtureContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                    ["commandEnvelopeVersion"] = new JsonObject { ["const"] = EnvelopeVersion },
                    ["protectedWriteAvailable"] = new JsonObject { ["type"] = "boolean" },
                    ["schemas"] = BoundedArray("schemaDescriptor", 16),
                    ["capabilities"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 64,
                        ["uniqueItems"] = true,
                        ["items"] = Enum(Configurator.Core.Definitions.DefinitionProfile.Capabilities)
                    }
                }),
            ["fieldDescription"] = ObjectSchema(["id", "schemaPointer", "kind", "nullable", "required", "sensitivity",
                "detail", "policyDeclared", "conditionalActivation", "conditionalRequirement", "hasChoices"], new()
                {
                    ["id"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$"),
                    ["schemaPointer"] = String(1, 256, "^(?:/(?:[^~/\\x00-\\x1F\\x7F]|~[01])*)+$"),
                    ["kind"] = Enum(["string", "boolean", "integer", "number", "object", "array"]),
                    ["nullable"] = new JsonObject { ["type"] = "boolean" },
                    ["required"] = new JsonObject { ["type"] = "boolean" },
                    ["sensitivity"] = Enum(["public", "sensitive", "secret"]),
                    ["detail"] = Enum(["basic", "standard", "advanced"]),
                    ["policyDeclared"] = new JsonObject { ["type"] = "boolean" },
                    ["acquisition"] = Enum(["local", "existing", "issuer"]),
                    ["conditionalActivation"] = new JsonObject { ["type"] = "boolean" },
                    ["conditionalRequirement"] = new JsonObject { ["type"] = "boolean" },
                    ["hasChoices"] = new JsonObject { ["type"] = "boolean" }
                }),
            ["definitionDescription"] = ObjectSchema(["familyId", "revision", "definitionDigest", "applicationId",
                "profile", "requiredCapabilities", "artifactIds", "locales", "applicationVersions", "entryPoints",
                "offset", "limit", "total", "retainedCount", "returnedCount", "terminal", "nextOffset", "digest", "fields"], new()
                {
                    ["familyId"] = String(36, 36, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
                    ["revision"] = Integer(1, long.MaxValue),
                    ["definitionDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["applicationId"] = String(1, 256, "^[^\\x00-\\x1F\\x7F]+$"),
                    ["profile"] = new JsonObject { ["const"] = "bounded-v0.0.1" },
                    ["requiredCapabilities"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 64,
                        ["uniqueItems"] = true,
                        ["items"] = Enum(Configurator.Core.Definitions.DefinitionProfile.Capabilities)
                    },
                    ["artifactIds"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 64,
                        ["items"] = String(1, 128, "^[A-Za-z0-9._-]+$")
                    },
                    ["locales"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 128,
                        ["items"] = String(1, 128, "^[^\\x00-\\x1F\\x7F]+$")
                    },
                    ["applicationVersions"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 128,
                        ["items"] = String(1, 128, "^[^\\x00-\\x1F\\x7F]+$")
                    },
                    ["entryPoints"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 128,
                        ["items"] = String(1, 128, "^[^\\x00-\\x1F\\x7F]+$")
                    },
                    ["offset"] = Integer(0, int.MaxValue),
                    ["limit"] = Integer(1, 16),
                    ["total"] = Integer(0, int.MaxValue),
                    ["retainedCount"] = Integer(0, int.MaxValue),
                    ["returnedCount"] = Integer(0, 16),
                    ["terminal"] = new JsonObject { ["type"] = "boolean" },
                    ["nextOffset"] = new JsonObject { ["oneOf"] = new JsonArray(Integer(0, int.MaxValue), new JsonObject { ["type"] = "null" }) },
                    ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["fields"] = BoundedArray("fieldDescription", 16)
                }),
            ["artifact"] = ObjectSchema(["id", "kind", "mediaType", "sensitivity", "pathIntent", "writeStatus", "installation", "verification"], new()
            {
                ["id"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$"),
                ["kind"] = Enum(["catalogue", "definition-artifact", "answer-snapshot", "generated-password", "doctor-report", "authoring-source", "compiled-definition", "logical-id-map", "answer-schema", "schema-document", "encrypted-profile", "comparison-profile"]),
                ["mediaType"] = String(1, 128, "^[a-z0-9][a-z0-9.+-]*/[a-z0-9][a-z0-9.+-]*(?:;v=[a-z0-9.-]+)?$"),
                ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["sensitivity"] = Enum(["public", "restricted"]),
                ["pathIntent"] = Enum(["none", "input", "output", "stdout"]),
                ["writeStatus"] = Enum(["none", "planned", "written", "not-attempted",
                    "conflict", "blocked", "failed", "unknown"]),
                ["installation"] = Enum(["not-applicable", "not-attempted", "not-established", "established", "unknown"]),
                ["verification"] = Enum(["not-applicable", "not-attempted", "succeeded", "failed", "unknown"]),
                ["completeness"] = Enum(["complete", "deferred"]),
                ["deferredFieldCount"] = Integer(0, 4096)
            }),
            ["revisionChange"] = ObjectSchema(["impact", "category", "schemaPath"], new()
            {
                ["impact"] = Enum(["behavioral", "display-only"]),
                ["category"] = String(1, 64, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                ["schemaPath"] = String(0, 256, "^(?:/(?:[^~/\\x00-\\x1F\\x7F]|~[01])*)*$"),
                ["detail"] = String(1, 96, "^[^\\x00-\\x1F\\x7F]+$")
            }),
            ["revisionDiff"] = ObjectSchema(["baselineFamilyId", "baselineRevision", "baselineDigest",
                "candidateFamilyId", "candidateRevision", "candidateDigest", "sameFamily", "canonicalIdentical",
                "hasBehavioralChanges", "hasDisplayOnlyChanges", "behavioralCount", "displayOnlyCount",
                "totalChanges", "truncated", "changes"], new()
                {
                    ["baselineFamilyId"] = String(36, 36, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
                    ["baselineRevision"] = Integer(1, long.MaxValue),
                    ["baselineDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["candidateFamilyId"] = String(36, 36, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
                    ["candidateRevision"] = Integer(1, long.MaxValue),
                    ["candidateDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["sameFamily"] = new JsonObject { ["type"] = "boolean" },
                    ["canonicalIdentical"] = new JsonObject { ["type"] = "boolean" },
                    ["hasBehavioralChanges"] = new JsonObject { ["type"] = "boolean" },
                    ["hasDisplayOnlyChanges"] = new JsonObject { ["type"] = "boolean" },
                    ["behavioralCount"] = Integer(0, int.MaxValue),
                    ["displayOnlyCount"] = Integer(0, int.MaxValue),
                    ["totalChanges"] = Integer(0, int.MaxValue),
                    ["truncated"] = new JsonObject { ["type"] = "boolean" },
                    ["changes"] = BoundedArray("revisionChange", 256)
                }),
            ["fixtureCase"] = ObjectSchema(["caseId", "kind", "outcome", "expectedRefusal",
                "observedDiagnosticCodes", "observedArtifactIds"], new()
                {
                    ["caseId"] = String(1, 128, "^[A-Za-z0-9._-]+$"),
                    ["kind"] = Enum(["prepare", "import"]),
                    ["outcome"] = Enum(["passed", "passed-expected-refusal", "failed"]),
                    ["expectedRefusal"] = new JsonObject { ["type"] = "boolean" },
                    ["failureCode"] = String(1, 96, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                    ["observedDiagnosticCodes"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 32,
                        ["items"] = String(1, 96, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
                    },
                    ["observedArtifactIds"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = 64,
                        ["items"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$")
                    }
                }),
            ["fixtureReport"] = ObjectSchema(["definitionDigest", "fixtureDigest", "engineVersion", "scope",
                "claimBoundary", "passed", "totalCases", "passedCases", "failedCases", "expectedRefusalCases", "cases"], new()
                {
                    ["definitionDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["fixtureDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["engineVersion"] = String(1, 64, "^[^\\x00-\\x1F\\x7F]+$"),
                    ["scope"] = String(1, 96, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                    ["claimBoundary"] = String(1, 256, "^[^\\x00-\\x1F\\x7F]+$"),
                    ["passed"] = new JsonObject { ["type"] = "boolean" },
                    ["totalCases"] = Integer(0, 64),
                    ["passedCases"] = Integer(0, 64),
                    ["failedCases"] = Integer(0, 64),
                    ["expectedRefusalCases"] = Integer(0, 64),
                    ["cases"] = BoundedArray("fixtureCase", 64)
                }),
            ["coverage"] = ObjectSchema(["coveredCount", "missingCount", "detailDigest"], new()
            {
                ["coveredCount"] = Integer(0, 4096),
                ["missingCount"] = Integer(0, 4096),
                ["detailDigest"] = String(null, null, "^[0-9a-f]{64}$")
            }),
            ["readiness"] = ObjectSchema(["state", "missingCount", "blockedCount"], new()
            {
                ["state"] = Enum(["ready", "incomplete", "blocked", "unassessed"]),
                ["missingCount"] = Integer(0, 4096),
                ["blockedCount"] = Integer(0, 4096)
            }),
            ["coverageEntry"] = ObjectSchema(["id", "state"], new()
            {
                ["id"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$"),
                ["state"] = Enum(["covered", "missing"])
            }),
            ["assessment"] = ObjectSchema(["definitionDigest", "artifactId", "compatibility", "coverage", "inventory",
                "autofill", "retainedCount"], new()
                {
                    ["definitionDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["artifactId"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$"),
                    ["compatibility"] = Enum(["compatible", "changes-required", "incomplete", "unsupported", "invalid-input"]),
                    ["coverage"] = Enum(["complete", "incomplete"]),
                    ["inventory"] = Enum(["complete", "stopped"]),
                    ["autofill"] = Enum(["eligible", "ineligible"]),
                    ["retainedCount"] = Integer(0, 4096),
                    ["total"] = Integer(0, 4096)
                }),
            ["finding"] = ObjectSchema(["classification", "code"], new()
            {
                ["classification"] = Enum(["missing-required", "invalid-supplied-value", "unrecognized-input",
                    "structural-output-change", "unassessed", "context-derived-value"]),
                ["code"] = String(1, 96, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$"),
                ["source"] = Ref("findingSource"),
                ["schema"] = Ref("findingSchema")
            }),
            ["findingSource"] = ObjectSchema(["ordinals"], new()
            {
                ["ordinals"] = new JsonObject { ["type"] = "array", ["maxItems"] = 32, ["items"] = Integer(0, 4095) }
            }),
            ["findingSchema"] = ObjectSchema(["fieldId", "relativePath"], new()
            {
                ["fieldId"] = String(0, 128, "^(?:[A-Za-z][A-Za-z0-9._-]*)?$"),
                ["relativePath"] = new JsonObject
                {
                    ["type"] = "array",
                    ["maxItems"] = 32,
                    ["items"] = String(0, 256, "^[\\s\\S]*$")
                }
            }),
            ["detail"] = ObjectSchema(["kind", "offset", "limit", "retainedCount", "returnedCount", "nextOffset", "terminal",
                "digest", "diagnostics", "coverage", "findings"], new()
                {
                    ["kind"] = Enum(["diagnostics", "coverage", "findings"]),
                    ["offset"] = Integer(0, 4096),
                    ["limit"] = Integer(1, 32),
                    ["retainedCount"] = Integer(0, 4096),
                    ["returnedCount"] = Integer(0, 32),
                    ["nextOffset"] = new JsonObject { ["oneOf"] = new JsonArray(Integer(0, 4096), new JsonObject { ["type"] = "null" }) },
                    ["terminal"] = new JsonObject { ["type"] = "boolean" },
                    ["total"] = Integer(0, 4096),
                    ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                    ["diagnostics"] = BoundedArray("diagnostic", 32),
                    ["coverage"] = BoundedArray("coverageEntry", 32),
                    ["findings"] = BoundedArray("finding", 32)
                })
        };
        definitions["workflow"] = ObjectSchema(["offset", "limit", "total", "digest", "optionCount",
            "nextActionCount", "suggestionCount", "items", "retainedCount", "returnedCount", "terminal", "nextOffset"], new()
            {
                ["offset"] = Integer(0, int.MaxValue),
                ["limit"] = Integer(1, 32),
                ["total"] = Integer(0, int.MaxValue),
                ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["optionCount"] = Integer(0, 64),
                ["nextActionCount"] = Integer(0, 4032),
                ["suggestionCount"] = Integer(0, int.MaxValue),
                ["items"] = BoundedArray("workflowItem", 32),
                ["retainedCount"] = Integer(0, int.MaxValue),
                ["returnedCount"] = Integer(0, 32),
                ["terminal"] = new JsonObject { ["type"] = "boolean" },
                ["nextOffset"] = new JsonObject { ["oneOf"] = new JsonArray(Integer(0, int.MaxValue), new JsonObject { ["type"] = "null" }) }
            });
        definitions["workflowItem"] = ObjectSchema(["kind", "id", "schemaPointer", "conditional"], new()
        {
            ["kind"] = Enum(["option", "next-action", "suggestion"]),
            ["id"] = String(1, 65536, "^(?:/(?:[^~/]|~[01])*)+$"),
            ["schemaPointer"] = String(1, 65536, "^(?:/(?:[^~/]|~[01])*)+$"),
            ["conditional"] = new JsonObject { ["type"] = "boolean" },
            ["optionId"] = String(0, 65536, "^[\\s\\S]*$"),
            ["targetOption"] = String(0, 65536, "^[\\s\\S]*$"),
            ["sourcePointer"] = String(1, 65536, "^(?:/(?:[^~/]|~[01])*)+$"),
            ["targetPointer"] = String(1, 65536, "^(?:/(?:[^~/]|~[01])*)+$"),
            ["priority"] = Enum(["recommended", "available", "optional"]),
            ["artifactCount"] = Integer(1, 64)
        });
        definitions["workflowItem"]!["oneOf"] = JsonNode.Parse("""
            [{"properties":{"kind":{"const":"option"},"conditional":{"const":false}},
              "required":["optionId","sourcePointer","artifactCount"],
              "not":{"anyOf":[{"required":["targetOption"]},{"required":["targetPointer"]},{"required":["priority"]}]}},
             {"properties":{"kind":{"const":"next-action"}},"required":["optionId","targetOption","priority"],
              "not":{"anyOf":[{"required":["sourcePointer"]},{"required":["targetPointer"]},{"required":["artifactCount"]}]}},
             {"properties":{"kind":{"const":"suggestion"}},"required":["sourcePointer","targetPointer"],
              "not":{"anyOf":[{"required":["optionId"]},{"required":["targetOption"]},{"required":["priority"]},{"required":["artifactCount"]}]}}]
            """);
        definitions["workflow"]!["oneOf"] = JsonNode.Parse("""
            [{"properties":{"terminal":{"const":true},"nextOffset":{"type":"null"}}},
             {"properties":{"terminal":{"const":false},"nextOffset":{"type":"integer","minimum":1,"maximum":2147483647},
               "returnedCount":{"type":"integer","minimum":1,"maximum":32}}}]
            """);
        definitions["assessment"]!["oneOf"] = new JsonArray(
            new JsonObject
            {
                ["properties"] = new JsonObject { ["inventory"] = new JsonObject { ["const"] = "complete" } },
                ["required"] = Array(["total"])
            },
            new JsonObject
            {
                ["properties"] = new JsonObject { ["inventory"] = new JsonObject { ["const"] = "stopped" } },
                ["not"] = new JsonObject { ["required"] = Array(["total"]) }
            });
        definitions["detail"]!["oneOf"] = new JsonArray(
            new JsonObject
            {
                ["properties"] = new JsonObject
                {
                    ["terminal"] = new JsonObject { ["const"] = true },
                    ["nextOffset"] = new JsonObject { ["type"] = "null" }
                }
            },
            new JsonObject
            {
                ["properties"] = new JsonObject
                {
                    ["terminal"] = new JsonObject { ["const"] = false },
                    ["nextOffset"] = Integer(1, 4096),
                    ["returnedCount"] = Integer(1, 32)
                }
            });
        definitions["diagnostic"]!["not"] = new JsonObject { ["required"] = Array(["source", "correction"]) };
        definitions["correctionSegment"]!["oneOf"] = new JsonArray(
            new JsonObject
            {
                ["properties"] = new JsonObject { ["kind"] = new JsonObject { ["const"] = "property" } },
                ["required"] = Array(["propertyName"]),
                ["not"] = new JsonObject { ["required"] = Array(["index"]) }
            },
            new JsonObject
            {
                ["properties"] = new JsonObject { ["kind"] = Enum(["arrayRow", "mapEntry"]) },
                ["required"] = Array(["index"]),
                ["not"] = new JsonObject { ["required"] = Array(["propertyName"]) }
            });
        definitions["schema"]!["oneOf"] = new JsonArray(new[]
        {
            ("answers", "urn:islamu:configurator:answers:v0.0.1"),
            ("authoring", "urn:islamu:configurator:authoring:v0.0.1"),
            ("command", SchemaId),
            ("fixtures", Configurator.Core.Definitions.Authoring.DefinitionFixtureDocument.SchemaId),
            ("runtime", "urn:islamu:configurator:setup-definition:v0.0.1"),
            ("store-curation", CliDiscoveryHandlers.StoreCurationSchemaId)
        }.Select(pair => (JsonNode?)new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["kind"] = new JsonObject { ["const"] = pair.Item1 },
                ["id"] = new JsonObject { ["const"] = pair.Item2 }
            }
        }).ToArray());
        definitions["definitionDescription"]!["oneOf"] = JsonNode.Parse("""
            [{"properties":{"terminal":{"const":true},"nextOffset":{"type":"null"}}},
             {"properties":{"terminal":{"const":false},"nextOffset":{"type":"integer","minimum":1,"maximum":2147483647},
               "returnedCount":{"type":"integer","minimum":1,"maximum":16}}}]
            """);
        JsonObject root = new()
        {
            ["_metadata"] = new JsonObject
            {
                ["about"] = Array(["Defines the closed value-safe machine response contract for islamu-configurator commands.",
                    "Generated by Configurator.CommandSchemaGenerator; edit compiled CLI metadata."]),
                ["generatedBy"] = "Configurator.CommandSchemaGenerator",
                ["authoringContractVersion"] = ConfiguratorVersion.Current,
                ["authoringDiscriminator"] = "urn:islamu:configurator:authoring:v0.0.1",
                ["runtimeSchemaVersion"] = ConfiguratorVersion.Current,
                ["commandOptions"] = Array(Options),
                ["repeatableOptions"] = Array(CliCommandRegistry.Families
                    .SelectMany(family => family.Operations).SelectMany(operation => operation.Options)
                    .Where(option => option.Repeatable).Select(option => option.Name)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                ["maximumArtifactSelections"] = 64,
                ["maximumOutputBytes"] = 65_536,
                ["maximumDetailBytes"] = 8192,
                ["commands"] = new JsonArray(Commands.Select(command => (JsonNode?)new JsonObject
                {
                    ["name"] = command.Name,
                    ["operations"] = new JsonArray(command.Operations.Select(operation => (JsonNode?)new JsonObject
                    {
                        ["name"] = operation.Name,
                        ["options"] = Array(operation.Options)
                    }).ToArray()),
                    ["options"] = Array(command.Options)
                }).ToArray())
            },
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = SchemaId,
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Array(["schemaVersion", "invocation", "status", "exitCategory", "exitCode", "dryRun",
                "completion", "preparation", "targetAcceptance", "cleanup", "delivery", "diagnosticsTotal",
                "diagnosticsOmitted", "detailDigest", "diagnostics", "artifacts", "coverage", "readiness"]),
            ["properties"] = new JsonObject
            {
                ["schemaVersion"] = new JsonObject { ["const"] = EnvelopeVersion },
                ["invocation"] = Ref("invocation"),
                ["status"] = ExitNames(),
                ["exitCategory"] = ExitNames(),
                ["exitCode"] = new JsonObject { ["enum"] = new JsonArray(System.Enum.GetValues<CliExitCode>().Select(value => (int)value).Append(130).Distinct().Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) },
                ["dryRun"] = new JsonObject { ["type"] = "boolean" },
                ["completion"] = Enum(["completed", "failed", "cancelled"]),
                ["preparation"] = new JsonObject { ["enum"] = new JsonArray(null, "ready", "incomplete", "blocked", "not-evaluated") },
                ["targetAcceptance"] = new JsonObject { ["const"] = "unassessed" },
                ["cleanup"] = Enum(["not-required", "completed", "failed", "unknown"]),
                ["delivery"] = new JsonObject { ["const"] = "attempt" },
                ["diagnosticsTotal"] = Integer(0, 4096),
                ["diagnosticsOmitted"] = Integer(0, 4096),
                ["detailDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["detail"] = Ref("detail"),
                ["assessment"] = Ref("assessment"),
                ["workflow"] = Ref("workflow"),
                ["help"] = Ref("help"),
                ["schema"] = Ref("schema"),
                ["schemaCatalog"] = Ref("schemaCatalog"),
                ["info"] = Ref("info"),
                ["definitionDescription"] = Ref("definitionDescription"),
                ["revisionDiff"] = Ref("revisionDiff"),
                ["fixtureReport"] = Ref("fixtureReport"),
                ["diagnostics"] = BoundedArray("diagnostic", 16),
                ["artifacts"] = BoundedArray("artifact", 64),
                ["coverage"] = Ref("coverage"),
                ["readiness"] = Ref("readiness"),
                ["authoringContractVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current },
                ["runtimeSchemaVersion"] = new JsonObject { ["const"] = ConfiguratorVersion.Current }
            },
            ["$defs"] = definitions
        };
        root["allOf"] = JsonNode.Parse("""
            [{"if":{"required":["workflow"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"inspect"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}}},
             {"if":{"properties":{"invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"inspect"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}},"then":{"required":["workflow"]}},
             {"if":{"required":["definitionDescription"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"describe"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}}},
             {"if":{"properties":{"invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"describe"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}},"then":{"required":["definitionDescription"]}},
             {"if":{"required":["revisionDiff"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"diff"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}}},
             {"if":{"properties":{"invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"diff"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}},"then":{"required":["revisionDiff"]}},
             {"if":{"required":["fixtureReport"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"definition"},"operation":{"const":"test"}}}},
                "not":{"required":["help"]}}},
             {"if":{"required":["schema"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"schema"},"operation":{"const":"export"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}}},
             {"if":{"properties":{"invocation":{"properties":{"commandFamily":{"const":"schema"},"operation":{"const":"export"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}},"then":{"required":["schema"]}},
             {"if":{"required":["schemaCatalog"]},"then":{"properties":{
                "invocation":{"properties":{"commandFamily":{"const":"schema"},"operation":{"const":"list"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}}},
             {"if":{"properties":{"invocation":{"properties":{"commandFamily":{"const":"schema"},"operation":{"const":"list"}}},
                "exitCode":{"const":0}},"not":{"required":["help"]}},"then":{"required":["schemaCatalog"]}},
              {"if":{"anyOf":[
                 {"properties":{"invocation":{"properties":{"commandFamily":{"const":"secret"}}}}},
                 {"properties":{"invocation":{"properties":{"commandFamily":{"const":"profile"},"operation":{"enum":["adopt","list"]}}}}}]},
              "then":{"properties":{"artifacts":{"items":{"not":{"required":["digest"]}}}}},
              "else":{"properties":{"artifacts":{"items":{"required":["digest"]}}}}}]
            """);
        string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        return Encoding.UTF8.GetBytes(json);
    }

    private static JsonObject ObjectSchema(IEnumerable<string> required, JsonObject properties) => new()
    { ["type"] = "object", ["additionalProperties"] = false, ["required"] = Array(required), ["properties"] = properties };
    private static JsonObject String(int? minimum, int? maximum, string pattern)
    {
        JsonObject result = new() { ["type"] = "string" };
        if (minimum.HasValue) result["minLength"] = minimum.Value;
        if (maximum.HasValue) result["maxLength"] = maximum.Value;
        result["pattern"] = pattern; return result;
    }
    private static JsonObject Enum(IEnumerable<string> values) => new() { ["enum"] = Array(values) };
    private static JsonObject ExitNames() => Enum(System.Enum.GetValues<CliExitCode>().Select(value => value.ToString().ToLowerInvariant()).Append("cancelled").Distinct(StringComparer.Ordinal));
    private static JsonObject Integer(long minimum, long maximum) => new()
    { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };
    private static JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
    private static JsonObject BoundedArray(string item, int maximum) => new()
    { ["type"] = "array", ["maxItems"] = maximum, ["items"] = Ref(item) };
    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
