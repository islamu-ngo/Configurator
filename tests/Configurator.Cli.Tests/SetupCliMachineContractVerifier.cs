using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Configurator.Cli.Tests;

internal static partial class SetupCliMachineContractVerifier
{
    private const int MaximumOutputBytes = 65_536;
    private static readonly Dictionary<string, string[]> Required = new(StringComparer.Ordinal)
    {
        ["$"] = ["schemaVersion", "invocation", "status", "exitCategory", "exitCode", "dryRun", "completion", "preparation",
            "targetAcceptance", "cleanup", "delivery", "diagnosticsTotal", "diagnosticsOmitted", "detailDigest", "diagnostics", "artifacts", "coverage", "readiness"],
        ["$.invocation"] = ["commandFamily", "operation", "mode"],
        ["$.diagnostics[]"] = ["code", "path", "severity"],
        ["$.artifacts[]"] = ["id", "kind", "mediaType", "digest", "sensitivity", "pathIntent", "writeStatus"],
        ["$.coverage"] = ["coveredCount", "missingCount", "detailDigest"],
        ["$.readiness"] = ["state", "missingCount", "blockedCount"],
        ["$.detail"] = ["kind", "offset", "limit", "total", "digest", "diagnostics", "coverage"],
        ["$.detail.coverage[]"] = ["id", "state"]
    };

    private static readonly Dictionary<string, string[]> Allowed = Required.ToDictionary(
        item => item.Key, item => item.Key switch
        {
            "$" => [.. item.Value, "authoringContractVersion", "runtimeSchemaVersion", "detail"],
            "$.diagnostics[]" => [.. item.Value, "line", "column", "byteOffset"],
            _ => item.Value
        },
        StringComparer.Ordinal);
    private static readonly string[] ForbiddenPublicNames =
    [
        "secret", "value", "message", "body", "content", "token", "password", "credential",
        "connection", "host", "url", "user", "tenant-target", "live", "apply", "exception"
    ];

    internal static JsonObject ParseSchema(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] == 0xEF || bytes[^1] != (byte)'\n' || bytes.Contains((byte)'\r'))
        {
            throw new InvalidDataException("schema-bytes-not-deterministic");
        }
        return JsonNode.Parse(bytes)?.AsObject() ?? throw new InvalidDataException("schema-object-required");
    }

    internal static IReadOnlyList<string> InspectSchema(JsonObject schema)
    {
        var errors = new List<string>();
        if (schema["$id"]?.GetValue<string>() != "https://schemas.islamu.org/configurator/command/v2/schema.json")
        {
            errors.Add("schema-id");
        }
        if (schema["_metadata"]?["generatedBy"]?.GetValue<string>() != "Configurator.CommandSchemaGenerator")
        {
            errors.Add("schema-metadata");
        }

        InspectNode(schema, "$", errors, new HashSet<JsonNode>(ReferenceEqualityComparer.Instance));
        var objects = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["$"] = schema,
            ["$.invocation"] = schema["$defs"]!["invocation"]!.AsObject(),
            ["$.diagnostics[]"] = schema["$defs"]!["diagnostic"]!.AsObject(),
            ["$.artifacts[]"] = schema["$defs"]!["artifact"]!.AsObject(),
            ["$.coverage"] = schema["$defs"]!["coverage"]!.AsObject(),
            ["$.readiness"] = schema["$defs"]!["readiness"]!.AsObject(),
            ["$.detail"] = schema["$defs"]!["detail"]!.AsObject(),
            ["$.detail.coverage[]"] = schema["$defs"]!["coverageEntry"]!.AsObject()
        };
        foreach ((string path, JsonObject obj) in objects)
        {
            if (!obj["required"]!.AsArray().Select(node => node!.GetValue<string>()).ToHashSet(StringComparer.Ordinal).SetEquals(Required[path])
                || !obj["properties"]!.AsObject().Select(property => property.Key).ToHashSet(StringComparer.Ordinal).SetEquals(Allowed[path]))
                errors.Add("schema-object-contract:" + path);
        }
        if (schema["properties"]!["schemaVersion"]!["const"]!.GetValue<string>() != "islamu-configurator-command/v2"
            || schema["properties"]!["diagnostics"]!["maxItems"]!.GetValue<int>() != 16
            || schema["$defs"]!["detail"]!["properties"]!["diagnostics"]!["maxItems"]!.GetValue<int>() != 32
            || schema["$defs"]!["detail"]!["properties"]!["coverage"]!["maxItems"]!.GetValue<int>() != 32)
            errors.Add("schema-response-bounds");
        if (!schema["properties"]!["exitCode"]!["enum"]!.AsArray().Select(node => node!.GetValue<int>()).ToHashSet()
            .SetEquals(SetupCliContractSpecification.ExitCodes.Values))
            errors.Add("schema-exit-vocabulary");
        JsonArray commands = schema["_metadata"]!["commands"]!.AsArray();
        if (!commands.Select(command => command!["name"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal)
            .SetEquals(SetupCliContractSpecification.Operations.Keys))
            errors.Add("schema-family-vocabulary");
        foreach (JsonNode? command in commands)
        {
            string family = command!["name"]!.GetValue<string>();
            if (!SetupCliContractSpecification.Operations.TryGetValue(family, out IReadOnlySet<string>? expected)
                || !expected.SetEquals(command["operations"]!.AsArray()
                    .Select(operation => operation!["name"]!.GetValue<string>())))
                errors.Add("schema-operation-vocabulary");
        }
        return errors;
    }

    internal static IReadOnlyList<string> Validate(byte[] bytes)
    {
        var errors = new List<string>();
        if (bytes.Length == 0 || bytes.Length > MaximumOutputBytes || bytes[0] == 0xEF || bytes.Contains((byte)'\r')
            || bytes[^1] != (byte)'\n' || bytes.Count(value => value == (byte)'\n') != 1)
        {
            errors.Add("machine-framing");
        }
        if (bytes.Any(value => value is 0x1B or 0x7F || (value < 0x20 && value is not 0x0A)))
        {
            errors.Add("machine-control");
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            })?.AsObject();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            errors.Add("machine-single-object");
            return errors;
        }
        if (root is null)
        {
            return [.. errors, "machine-object-required"];
        }

        try
        {
            ValidateObject(root, "$", errors);
            ValidateInvocation(root["invocation"] as JsonObject, errors);
            ValidateStatus(root, errors);
            bool compilerHandshake = root["invocation"]?["commandFamily"]?.GetValue<string>() == "definition"
                && root["invocation"]?["operation"]?.GetValue<string>() is "check" or "build"
                && root["exitCode"]?.GetValue<int>() == 0;
            if (compilerHandshake ? root["authoringContractVersion"]?.GetValue<int>() != 1 || root["runtimeSchemaVersion"]?.GetValue<int>() != 1
                : root.ContainsKey("authoringContractVersion") || root.ContainsKey("runtimeSchemaVersion"))
                errors.Add("machine-compiler-handshake");
            ValidateDiagnostics(root["diagnostics"] as JsonArray, 16, errors);
            ValidateArtifacts(root["artifacts"] as JsonArray, errors);
            ValidateCoverage(root["coverage"] as JsonObject, "$.coverage", errors);
            ValidateReadiness(root["readiness"] as JsonObject, "$.readiness", errors);
            ValidateSummary(root, errors);
            if (root.ContainsKey("detail")) ValidateDetail(root, errors);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            errors.Add("machine-property-type");
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static byte[] GoodFixture()
    {
        const string digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var root = new JsonObject
        {
            ["schemaVersion"] = "islamu-configurator-command/v2",
            ["invocation"] = new JsonObject { ["commandFamily"] = "artifact", ["operation"] = "validate", ["mode"] = "machine" },
            ["status"] = "success",
            ["exitCategory"] = "success",
            ["exitCode"] = 0,
            ["dryRun"] = true,
            ["completion"] = "completed",
            ["preparation"] = "ready",
            ["targetAcceptance"] = "unassessed",
            ["cleanup"] = "not-required",
            ["delivery"] = "attempt",
            ["diagnosticsTotal"] = 0,
            ["diagnosticsOmitted"] = 0,
            ["detailDigest"] = digest,
            ["diagnostics"] = new JsonArray(),
            ["artifacts"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "config",
                    ["kind"] = "definition-artifact",
                    ["mediaType"] = "application/json",
                    ["digest"] = digest,
                    ["sensitivity"] = "restricted",
                    ["pathIntent"] = "input",
                    ["writeStatus"] = "none"
                }
            },
            ["coverage"] = Coverage(digest),
            ["readiness"] = Readiness("ready")
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");
    }

    internal static byte[] Mutate(Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(GoodFixture())!.AsObject();
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString() + "\n");
    }

    private static JsonObject Coverage(string digest) => new()
    {
        ["coveredCount"] = 1,
        ["missingCount"] = 0,
        ["detailDigest"] = digest
    };

    private static JsonObject Readiness(string state) => new()
    {
        ["state"] = state,
        ["missingCount"] = 0,
        ["blockedCount"] = 0
    };

    private static void InspectNode(JsonNode node, string path, List<string> errors, HashSet<JsonNode> visited)
    {
        if (!visited.Add(node))
        {
            return;
        }
        if (node is JsonObject obj)
        {
            bool objectSchema = obj["type"]?.GetValue<string>() == "object";
            if (objectSchema && obj["additionalProperties"]?.GetValue<bool>() != false)
            {
                errors.Add("schema-open-object:" + path);
            }
            bool arraySchema = obj["type"]?.GetValue<string>() == "array";
            if (arraySchema && obj["maxItems"] is null)
            {
                errors.Add("schema-unbounded-array:" + path);
            }
            bool stringSchema = obj["type"]?.GetValue<string>() == "string";
            if (stringSchema && obj["maxLength"] is null && obj["pattern"] is null && obj["enum"] is null && obj["const"] is null)
            {
                errors.Add("schema-unbounded-string:" + path);
            }
            if (obj["properties"] is JsonObject properties)
            {
                foreach ((string name, JsonNode? child) in properties)
                {
                    if (ForbiddenPublicNames.Any(forbidden => forbidden == "live" ? name.Equals(forbidden, StringComparison.OrdinalIgnoreCase)
                        : name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add("schema-forbidden-name:" + name);
                    }
                    if (child is not null)
                    {
                        InspectNode(child, path + "." + name, errors, visited);
                    }
                }
            }
            foreach ((string name, JsonNode? child) in obj)
            {
                if (name != "properties" && child is not null)
                {
                    InspectNode(child, path + "/" + name, errors, visited);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    InspectNode(child, path + "[]", errors, visited);
                }
            }
        }
    }

    private static void ValidateObject(JsonObject obj, string path, List<string> errors)
    {
        if (!Required.TryGetValue(path, out string[]? required))
        {
            return;
        }
        if (required.Any(name => !obj.ContainsKey(name)))
        {
            errors.Add("machine-required:" + path);
        }
        if (obj.Any(property => !Allowed[path].Contains(property.Key, StringComparer.Ordinal)))
        {
            errors.Add("machine-additional-property:" + path);
        }
    }

    private static void ValidateInvocation(JsonObject? invocation, List<string> errors)
    {
        if (invocation is null)
        {
            errors.Add("machine-invocation");
            return;
        }
        ValidateObject(invocation, "$.invocation", errors);
        string family = invocation["commandFamily"]?.GetValue<string>() ?? string.Empty;
        string operation = invocation["operation"]?.GetValue<string>() ?? string.Empty;
        string mode = invocation["mode"]?.GetValue<string>() ?? string.Empty;
        if (!SetupCliContractSpecification.Operations.TryGetValue(family, out IReadOnlySet<string>? operations) || !operations.Contains(operation) || mode != "machine")
        {
            errors.Add("machine-invocation-vocabulary");
        }
    }

    private static void ValidateStatus(JsonObject root, List<string> errors)
    {
        string status = root["status"]?.GetValue<string>() ?? string.Empty;
        string category = root["exitCategory"]?.GetValue<string>() ?? string.Empty;
        int code = root["exitCode"]?.GetValue<int>() ?? -1;
        if (status != category || !SetupCliContractSpecification.ExitCodes.TryGetValue(category, out int expected) || code != expected)
        {
            errors.Add("machine-exit-mismatch");
        }
        if (root["schemaVersion"]?.GetValue<string>() != "islamu-configurator-command/v2")
        {
            errors.Add("machine-version");
        }
    }

    private static void ValidateDiagnostics(JsonArray? diagnostics, int maximum, List<string> errors)
    {
        if (diagnostics is null || diagnostics.Count > maximum)
        {
            errors.Add("machine-diagnostic-count");
            return;
        }
        foreach (JsonNode? node in diagnostics)
        {
            if (node is not JsonObject diagnostic)
            {
                errors.Add("machine-diagnostic-object");
                continue;
            }
            ValidateObject(diagnostic, "$.diagnostics[]", errors);
            string code = diagnostic["code"]?.GetValue<string>() ?? string.Empty;
            string path = diagnostic["path"]?.GetValue<string>() ?? string.Empty;
            string severity = diagnostic["severity"]?.GetValue<string>() ?? string.Empty;
            if (code.Length is < 1 or > 96 || !CodePattern().IsMatch(code)
                || diagnostic["path"] is not JsonValue || path.Length > 256 || !PathPattern().IsMatch(path)
                || severity is not ("info" or "warning" or "error"))
            {
                errors.Add("machine-diagnostic-shape");
            }
            if ((diagnostic.ContainsKey("line") && !IsInteger(diagnostic["line"], 1, int.MaxValue))
                || (diagnostic.ContainsKey("column") && !IsInteger(diagnostic["column"], 1, int.MaxValue))
                || (diagnostic.ContainsKey("byteOffset") && !IsInteger(diagnostic["byteOffset"], 0, long.MaxValue)))
                errors.Add("machine-diagnostic-location");
        }
    }

    private static void ValidateArtifacts(JsonArray? artifacts, List<string> errors)
    {
        if (artifacts is null || artifacts.Count > 64)
        {
            errors.Add("machine-artifact-count");
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? node in artifacts)
        {
            if (node is not JsonObject artifact)
            {
                errors.Add("machine-artifact-object");
                continue;
            }
            ValidateObject(artifact, "$.artifacts[]", errors);
            string id = artifact["id"]?.GetValue<string>() ?? string.Empty;
            string mediaType = artifact["mediaType"]?.GetValue<string>() ?? string.Empty;
            if (id.Length is < 1 or > 128 || !KeyPattern().IsMatch(id) || !ids.Add(id)
                || !DigestPattern().IsMatch(artifact["digest"]?.GetValue<string>() ?? string.Empty)
                || artifact["sensitivity"]?.GetValue<string>() is not ("public" or "restricted")
                || artifact["kind"]?.GetValue<string>() is not ("catalogue" or "definition-artifact" or "answer-snapshot" or "doctor-report" or "authoring-source" or "compiled-definition" or "logical-id-map")
                || mediaType.Length is < 1 or > 128 || !MediaTypePattern().IsMatch(mediaType)
                || artifact["pathIntent"]?.GetValue<string>() is not ("none" or "input" or "output" or "stdout")
                || artifact["writeStatus"]?.GetValue<string>() is not ("none" or "planned" or "written" or "not-attempted" or "conflict" or "blocked" or "failed" or "unknown"))
            {
                errors.Add("machine-artifact-shape");
            }
        }
    }

    private static void ValidateCoverage(JsonObject? coverage, string path, List<string> errors)
    {
        if (coverage is null)
        {
            errors.Add("machine-coverage");
            return;
        }
        ValidateObject(coverage, path, errors);
        if (!IsInteger(coverage["coveredCount"], 0, 4096) || !IsInteger(coverage["missingCount"], 0, 4096)
            || !DigestPattern().IsMatch(coverage["detailDigest"]?.GetValue<string>() ?? string.Empty))
            errors.Add("machine-coverage-shape");
        else if (coverage["coveredCount"]!.GetValue<int>() + coverage["missingCount"]!.GetValue<int>() > 4096)
            errors.Add("machine-coverage-count");
    }

    private static void ValidateReadiness(JsonObject? readiness, string path, List<string> errors)
    {
        if (readiness is null)
        {
            errors.Add("machine-readiness");
            return;
        }
        ValidateObject(readiness, path, errors);
        string state = readiness["state"]?.GetValue<string>() ?? string.Empty;
        if (!IsInteger(readiness["missingCount"], 0, 4096) || !IsInteger(readiness["blockedCount"], 0, 4096))
        {
            errors.Add("machine-readiness-count");
            return;
        }
        int missingCount = readiness["missingCount"]!.GetValue<int>();
        int blockedCount = readiness["blockedCount"]!.GetValue<int>();
        bool consistent = state switch
        {
            "ready" => missingCount == 0 && blockedCount == 0,
            "incomplete" => missingCount > 0 && blockedCount == 0,
            "blocked" => blockedCount > 0,
            "unassessed" => missingCount == 0 && blockedCount == 0,
            _ => false
        };
        if (!consistent)
        {
            errors.Add("machine-readiness-contradiction");
        }
    }

    private static void ValidateSummary(JsonObject root, List<string> errors)
    {
        if (root["dryRun"] is not JsonValue dryRun || !dryRun.TryGetValue<bool>(out _)
            || root["targetAcceptance"]?.GetValue<string>() != "unassessed"
            || root["delivery"]?.GetValue<string>() != "attempt"
            || root["cleanup"]?.GetValue<string>() is not ("not-required" or "completed" or "failed" or "unknown"))
            errors.Add("machine-lifecycle-shape");
        string? completion = root["completion"]?.GetValue<string>();
        int exitCode = root["exitCode"]?.GetValue<int>() ?? -1;
        if (completion is not ("completed" or "failed" or "cancelled")
            || (completion == "cancelled") != (exitCode == 130)
            || (exitCode == 0 && completion != "completed"))
            errors.Add("machine-completion-contradiction");
        string? preparation = root["preparation"]?.GetValue<string>();
        if (preparation is not (null or "ready" or "incomplete" or "blocked"))
            errors.Add("machine-preparation-contradiction");
        if (!IsInteger(root["diagnosticsTotal"], 0, 4096) || !IsInteger(root["diagnosticsOmitted"], 0, 4096)
            || root["diagnosticsTotal"]!.GetValue<int>() != (root["diagnostics"] as JsonArray)?.Count + root["diagnosticsOmitted"]!.GetValue<int>())
            errors.Add("machine-diagnostic-accounting");
        string digest = root["detailDigest"]?.GetValue<string>() ?? string.Empty;
        if (!DigestPattern().IsMatch(digest) || root["coverage"]?["detailDigest"]?.GetValue<string>() != digest)
            errors.Add("machine-detail-digest");
    }

    private static void ValidateDetail(JsonObject root, List<string> errors)
    {
        if (root["detail"] is not JsonObject detail)
        {
            errors.Add("machine-detail-object");
            return;
        }
        ValidateObject(detail, "$.detail", errors);
        ValidateDiagnostics(detail["diagnostics"] as JsonArray, 32, errors);
        if (!IsInteger(detail["offset"], 0, 2048) || !IsInteger(detail["limit"], 1, 32) || !IsInteger(detail["total"], 0, 4096)
            || detail["digest"]?.GetValue<string>() != root["detailDigest"]?.GetValue<string>()
            || Encoding.UTF8.GetByteCount(detail.ToJsonString(new JsonSerializerOptions { WriteIndented = false })) > 8192)
            errors.Add("machine-detail-bounds");
        if (detail["coverage"] is not JsonArray coverage || coverage.Count > 32)
        {
            errors.Add("machine-detail-coverage-count");
            return;
        }
        var ids = new List<string>();
        foreach (JsonNode? node in coverage)
        {
            if (node is not JsonObject entry) { errors.Add("machine-detail-coverage-entry"); continue; }
            ValidateObject(entry, "$.detail.coverage[]", errors);
            string id = entry["id"]?.GetValue<string>() ?? string.Empty;
            ids.Add(id);
            if (id.Length is < 1 or > 128 || !KeyPattern().IsMatch(id) || entry["state"]?.GetValue<string>() is not ("covered" or "missing"))
                errors.Add("machine-detail-coverage-entry");
        }
        if (!ids.SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            errors.Add("machine-detail-coverage-order");
        int diagnosticCount = (detail["diagnostics"] as JsonArray)?.Count ?? 0;
        int total = detail["total"]?.GetValue<int>() ?? -1;
        int count;
        switch (detail["kind"]?.GetValue<string>())
        {
            case "diagnostics":
                count = diagnosticCount;
                if (coverage.Count != 0 || total != root["diagnosticsTotal"]?.GetValue<int>()) errors.Add("machine-detail-kind");
                break;
            case "coverage":
                count = coverage.Count;
                if (diagnosticCount != 0 || total != root["coverage"]?["coveredCount"]?.GetValue<int>() + root["coverage"]?["missingCount"]?.GetValue<int>())
                    errors.Add("machine-detail-kind");
                break;
            default:
                errors.Add("machine-detail-kind");
                return;
        }
        if (count > detail["limit"]?.GetValue<int>() || count > Math.Max(0, total - (detail["offset"]?.GetValue<int>() ?? 0)))
            errors.Add("machine-detail-page");
    }

    private static bool IsInteger(JsonNode? node, long minimum, long maximum) =>
        node is JsonValue value && value.TryGetValue<long>(out long number) && number >= minimum && number <= maximum;

    [GeneratedRegex("\\A[a-z][a-z0-9]*(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("\\A(?:/(?:[^~/\\x00-\\x1F\\x7F]|~[01])*)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();

    [GeneratedRegex("\\A[A-Za-z][A-Za-z0-9._-]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("\\A[a-z0-9][a-z0-9.+-]*/[a-z0-9][a-z0-9.+-]*(?:;v=[a-z0-9.-]+)?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex MediaTypePattern();
}
