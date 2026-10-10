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
        ["$"] = ["schemaVersion", "invocation", "status", "exitCategory", "exitCode", "dryRun", "diagnostics", "artifacts", "coverage", "readiness"],
        ["$.invocation"] = ["commandFamily", "operation", "mode"],
        ["$.diagnostics[]"] = ["code", "path", "severity"],
        ["$.artifacts[]"] = ["kind", "mediaType", "digest", "sensitivity", "coverage", "readiness", "pathIntent", "writeStatus"],
        ["$.coverage"] = ["coveredKeys", "missingKeys"],
        ["$.readiness"] = ["state", "missingKeys", "blockedKeys"]
    };

    private static readonly Dictionary<string, string[]> Allowed = Required.ToDictionary(
        item => item.Key, item => item.Key == "$" ? [.. item.Value, "authoringContractVersion", "runtimeSchemaVersion"] : item.Value,
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
        if (schema["$id"]?.GetValue<string>() != "https://schemas.islamu.org/configurator/command/v1/schema.json")
        {
            errors.Add("schema-id");
        }
        if (schema["_metadata"]?["generatedBy"]?.GetValue<string>() != "Configurator.CommandSchemaGenerator")
        {
            errors.Add("schema-metadata");
        }

        InspectNode(schema, "$", errors, new HashSet<JsonNode>(ReferenceEqualityComparer.Instance));
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
        if (bytes.Length == 0 || bytes.Length > MaximumOutputBytes || bytes[0] == 0xEF || bytes.Contains((byte)'\r') || bytes[^1] != (byte)'\n')
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

        ValidateObject(root, "$", errors);
        ValidateInvocation(root["invocation"] as JsonObject, errors);
        ValidateStatus(root, errors);
        bool compilerHandshake = root["invocation"]?["commandFamily"]?.GetValue<string>() == "definition"
            && root["invocation"]?["operation"]?.GetValue<string>() is "check" or "build"
            && root["exitCode"]?.GetValue<int>() == 0;
        if (compilerHandshake ? root["authoringContractVersion"]?.GetValue<int>() != 1 || root["runtimeSchemaVersion"]?.GetValue<int>() != 1
            : root.ContainsKey("authoringContractVersion") || root.ContainsKey("runtimeSchemaVersion"))
            errors.Add("machine-compiler-handshake");
        ValidateDiagnostics(root["diagnostics"] as JsonArray, errors);
        ValidateArtifacts(root["artifacts"] as JsonArray, errors);
        ValidateCoverage(root["coverage"] as JsonObject, "$.coverage", errors);
        ValidateReadiness(root["readiness"] as JsonObject, "$.readiness", errors);
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static byte[] GoodFixture()
    {
        const string digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var root = new JsonObject
        {
            ["schemaVersion"] = "islamu-configurator-command/v1",
            ["invocation"] = new JsonObject { ["commandFamily"] = "artifact", ["operation"] = "validate", ["mode"] = "machine" },
            ["status"] = "success",
            ["exitCategory"] = "success",
            ["exitCode"] = 0,
            ["dryRun"] = true,
            ["diagnostics"] = new JsonArray(),
            ["artifacts"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "definition-artifact",
                    ["mediaType"] = "application/json",
                    ["digest"] = digest,
                    ["sensitivity"] = "restricted",
                    ["coverage"] = Coverage("service.name"),
                    ["readiness"] = Readiness("ready"),
                    ["pathIntent"] = "input",
                    ["writeStatus"] = "none"
                }
            },
            ["coverage"] = Coverage("service.name"),
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

    private static JsonObject Coverage(params string[] covered) => new()
    {
        ["coveredKeys"] = new JsonArray(covered.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
        ["missingKeys"] = new JsonArray()
    };

    private static JsonObject Readiness(string state, string[]? missing = null, string[]? blocked = null) => new()
    {
        ["state"] = state,
        ["missingKeys"] = new JsonArray((missing ?? []).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
        ["blockedKeys"] = new JsonArray((blocked ?? []).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray())
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
                    if (ForbiddenPublicNames.Any(forbidden => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
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
        if (root["schemaVersion"]?.GetValue<string>() != "islamu-configurator-command/v1")
        {
            errors.Add("machine-version");
        }
    }

    private static void ValidateDiagnostics(JsonArray? diagnostics, List<string> errors)
    {
        if (diagnostics is null || diagnostics.Count > 128)
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
            if (code.Length is < 1 or > 96 || !CodePattern().IsMatch(code) || path.Length is < 1 or > 256 || !PathPattern().IsMatch(path) || severity is not ("info" or "warning" or "error"))
            {
                errors.Add("machine-diagnostic-shape");
            }
        }
    }

    private static void ValidateArtifacts(JsonArray? artifacts, List<string> errors)
    {
        if (artifacts is null || artifacts.Count > 64)
        {
            errors.Add("machine-artifact-count");
            return;
        }
        foreach (JsonNode? node in artifacts)
        {
            if (node is not JsonObject artifact)
            {
                errors.Add("machine-artifact-object");
                continue;
            }
            ValidateObject(artifact, "$.artifacts[]", errors);
            if (!DigestPattern().IsMatch(artifact["digest"]?.GetValue<string>() ?? string.Empty) ||
                artifact["sensitivity"]?.GetValue<string>() is not ("public" or "restricted"))
            {
                errors.Add("machine-artifact-shape");
            }
            ValidateCoverage(artifact["coverage"] as JsonObject, "$.coverage", errors);
            ValidateReadiness(artifact["readiness"] as JsonObject, "$.readiness", errors);
        }
    }

    private static void ValidateCoverage(JsonObject? coverage, string path, List<string> errors)
    {
        if (coverage is null)
        {
            errors.Add("machine-coverage");
            return;
        }
        ValidateObject(coverage, "$.coverage", errors);
        ValidateKeyList(coverage["coveredKeys"] as JsonArray, path + ".coveredKeys", errors);
        ValidateKeyList(coverage["missingKeys"] as JsonArray, path + ".missingKeys", errors);
    }

    private static void ValidateReadiness(JsonObject? readiness, string path, List<string> errors)
    {
        if (readiness is null)
        {
            errors.Add("machine-readiness");
            return;
        }
        ValidateObject(readiness, "$.readiness", errors);
        string state = readiness["state"]?.GetValue<string>() ?? string.Empty;
        JsonArray? missing = readiness["missingKeys"] as JsonArray;
        JsonArray? blocked = readiness["blockedKeys"] as JsonArray;
        ValidateKeyList(missing, path + ".missingKeys", errors);
        ValidateKeyList(blocked, path + ".blockedKeys", errors);
        int missingCount = missing?.Count ?? 0;
        int blockedCount = blocked?.Count ?? 0;
        bool consistent = state switch
        {
            "ready" => missingCount == 0 && blockedCount == 0,
            "incomplete" => missingCount > 0 && blockedCount == 0,
            "blocked" => blockedCount > 0,
            _ => false
        };
        if (!consistent)
        {
            errors.Add("machine-readiness-contradiction");
        }
    }

    private static void ValidateKeyList(JsonArray? keys, string path, List<string> errors)
    {
        if (keys is null || keys.Count > 1024)
        {
            errors.Add("machine-key-count:" + path);
            return;
        }
        string[] values = keys.Select(item => item?.GetValue<string>() ?? string.Empty).ToArray();
        if (values.Any(value => value.Length is < 1 or > 128 || !KeyPattern().IsMatch(value)) ||
            !values.SequenceEqual(values.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            errors.Add("machine-key-order:" + path);
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^\\$(?:\\.[a-z][A-Za-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
