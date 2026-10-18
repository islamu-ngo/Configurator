using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli;

public sealed record SetupCliRegisteredOperationMetadata(
    string Name,
    IReadOnlyList<string> Options);

public sealed record SetupCliRegisteredCommandMetadata(
    string Name,
    IReadOnlyList<SetupCliRegisteredOperationMetadata> Operations,
    IReadOnlyList<string> Options);

public static class SetupCliCommandSchemaMetadata
{
    public const string RelativePath = "schemas/islamu-configurator-command-v2.schema.json";
    public static IReadOnlyList<SetupCliRegisteredCommandMetadata> Commands { get; } =
        SetupCliCommandRegistry.Families.Select(family => new SetupCliRegisteredCommandMetadata(
                family.Name,
                family.Operations.Select(operation => new SetupCliRegisteredOperationMetadata(
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
                ["operation"] = Enum(Operations.Append("help")),
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
                ["byteOffset"] = Integer(0, long.MaxValue)
            }),
            ["artifact"] = ObjectSchema(["id", "kind", "mediaType", "digest", "sensitivity", "pathIntent", "writeStatus", "installation", "verification"], new()
            {
                ["id"] = String(1, 128, "^[A-Za-z][A-Za-z0-9._-]*$"),
                ["kind"] = Enum(["catalogue", "definition-artifact", "answer-snapshot", "doctor-report", "authoring-source", "compiled-definition", "logical-id-map"]),
                ["mediaType"] = String(1, 128, "^[a-z0-9][a-z0-9.+-]*/[a-z0-9][a-z0-9.+-]*(?:;v=[a-z0-9.-]+)?$"),
                ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["sensitivity"] = Enum(["public", "restricted"]),
                ["pathIntent"] = Enum(["none", "input", "output", "stdout"]),
                ["writeStatus"] = Enum(["none", "planned", "written", "not-attempted",
                    "conflict", "blocked", "failed", "unknown"]),
                ["installation"] = Enum(["not-applicable", "not-attempted", "not-established", "established", "unknown"]),
                ["verification"] = Enum(["not-applicable", "not-attempted", "succeeded", "failed", "unknown"])
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
            ["detail"] = ObjectSchema(["kind", "offset", "limit", "total", "digest", "diagnostics", "coverage"], new()
            {
                ["kind"] = Enum(["diagnostics", "coverage"]),
                ["offset"] = Integer(0, 2048),
                ["limit"] = Integer(1, 32),
                ["total"] = Integer(0, 4096),
                ["digest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["diagnostics"] = BoundedArray("diagnostic", 32),
                ["coverage"] = BoundedArray("coverageEntry", 32)
            })
        };
        JsonObject root = new()
        {
            ["_metadata"] = new JsonObject
            {
                ["about"] = Array(["Defines the closed value-safe machine response contract for islamu-configurator commands.",
                    "Generated by Configurator.CommandSchemaGenerator; edit compiled CLI metadata."]),
                ["generatedBy"] = "Configurator.CommandSchemaGenerator",
                ["authoringContractVersion"] = 1,
                ["authoringDiscriminator"] = "urn:islamu:configurator:authoring:v1",
                ["runtimeSchemaVersion"] = 1,
                ["commandOptions"] = Array(Options),
                ["repeatableOptions"] = Array(SetupCliCommandRegistry.Families
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
            ["$id"] = "https://schemas.islamu.org/configurator/command/v2/schema.json",
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Array(["schemaVersion", "invocation", "status", "exitCategory", "exitCode", "dryRun",
                "completion", "preparation", "targetAcceptance", "cleanup", "delivery", "diagnosticsTotal",
                "diagnosticsOmitted", "detailDigest", "diagnostics", "artifacts", "coverage", "readiness"]),
            ["properties"] = new JsonObject
            {
                ["schemaVersion"] = new JsonObject { ["const"] = "islamu-configurator-command/v2" },
                ["invocation"] = Ref("invocation"),
                ["status"] = ExitNames(),
                ["exitCategory"] = ExitNames(),
                ["exitCode"] = new JsonObject { ["enum"] = new JsonArray(System.Enum.GetValues<SetupCliExitCode>().Select(value => (int)value).Append(130).Distinct().Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()) },
                ["dryRun"] = new JsonObject { ["type"] = "boolean" },
                ["completion"] = Enum(["completed", "failed", "cancelled"]),
                ["preparation"] = new JsonObject { ["enum"] = new JsonArray(null, "ready", "incomplete", "blocked") },
                ["targetAcceptance"] = new JsonObject { ["const"] = "unassessed" },
                ["cleanup"] = Enum(["not-required", "completed", "failed", "unknown"]),
                ["delivery"] = new JsonObject { ["const"] = "attempt" },
                ["diagnosticsTotal"] = Integer(0, 4096),
                ["diagnosticsOmitted"] = Integer(0, 4096),
                ["detailDigest"] = String(null, null, "^[0-9a-f]{64}$"),
                ["detail"] = Ref("detail"),
                ["help"] = Ref("help"),
                ["diagnostics"] = BoundedArray("diagnostic", 16),
                ["artifacts"] = BoundedArray("artifact", 64),
                ["coverage"] = Ref("coverage"),
                ["readiness"] = Ref("readiness"),
                ["authoringContractVersion"] = new JsonObject { ["const"] = 1 },
                ["runtimeSchemaVersion"] = new JsonObject { ["const"] = 1 }
            },
            ["$defs"] = definitions
        };
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
    private static JsonObject ExitNames() => Enum(System.Enum.GetValues<SetupCliExitCode>().Select(value => value.ToString().ToLowerInvariant()).Append("cancelled").Distinct(StringComparer.Ordinal));
    private static JsonObject Integer(long minimum, long maximum) => new()
    { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };
    private static JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
    private static JsonObject BoundedArray(string item, int maximum) => new()
    { ["type"] = "array", ["maxItems"] = maximum, ["items"] = Ref(item) };
    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
