namespace Configurator.Core.Definitions.Authoring;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;

/// <summary>
/// Lowers authoring <c>outputs</c> into runtime artifacts with bounded projection trees. Bindings accept string
/// pointer shorthands, <c>{ "literal": value }</c> constants and explicit references; <c>includeBindings</c>
/// reuses another output's bindings, <c>bindObject</c> binds every member of an object, and <c>when</c>
/// guards the whole artifact. Precedence is included, then object, then explicit bindings.
/// </summary>
internal sealed class DefinitionOutputLowering
{
    private const int MaximumBindings = 1024;
    private static readonly string[] OutputMembers =
    [
        "id", "format", "targetUpdateMode", "restartRequirement", "bindings", "projection", "title", "description",
        "consumerIds", "surface", "environmentDialect", "when", "includeBindings", "bindObject"
    ];

    private readonly JsonObject _outputs;
    private readonly JsonObject _configuration;
    private readonly Dictionary<string, SortedDictionary<string, JsonNode>> _resolved = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    private DefinitionOutputLowering(JsonObject outputs, JsonObject configuration)
    {
        _outputs = outputs;
        _configuration = configuration;
    }

    internal static JsonArray Lower(JsonObject outputs, JsonObject configuration)
    {
        if (outputs.Count is < 1 or > 64) throw new DefinitionException("artifact-limit");
        var lowering = new DefinitionOutputLowering(outputs, configuration);
        var artifacts = new JsonArray();
        var budget = new CompositionBudget(CompositionLimits.Default);
        foreach ((string fileName, JsonNode? value) in outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            JsonObject artifact = lowering.Artifact(fileName, value as JsonObject ?? throw new DefinitionException("authoring-mapping"));
            Charge(artifact, budget, 3);
            artifacts.Add(artifact);
        }
        return artifacts;
    }

    public static string OutputId(string fileName) => ValidIdentifier("out-" + fileName) ? "out-" + fileName : "o-" + Hash("output:" + fileName);

    private JsonObject Artifact(string fileName, JsonObject output)
    {
        if (output.Any(pair => !OutputMembers.Contains(pair.Key, StringComparer.Ordinal)))
            throw new DefinitionException("authoring-member");
        string format = output["format"]!.GetValue<string>();
        bool bound = output.ContainsKey("bindings") || output.ContainsKey("includeBindings") || output.ContainsKey("bindObject");
        JsonNode projection;
        if (output.ContainsKey("projection"))
        {
            if (bound) throw new DefinitionException("authoring-mapping");
            projection = output["projection"]!.DeepClone();
        }
        else if (bound) projection = Project(format, Bindings(fileName));
        else
        {
            if (format is not ("json" or "yaml")) throw new DefinitionException("authoring-mapping");
            projection = Copy(new JsonObject { ["scope"] = "root", ["path"] = "" });
        }
        if (output["when"] is { } when) Guard(projection.AsObject(), when);
        var artifact = new JsonObject
        {
            ["id"] = output.ContainsKey("id") ? output["id"]!.DeepClone() : JsonValue.Create(OutputId(fileName)),
            ["fileName"] = fileName,
            ["format"] = format,
            ["targetUpdateMode"] = output.ContainsKey("targetUpdateMode") ? output["targetUpdateMode"]!.DeepClone() : JsonValue.Create("unspecified"),
            ["restartRequirement"] = output.ContainsKey("restartRequirement") ? output["restartRequirement"]!.DeepClone() : JsonValue.Create("unspecified"),
            ["projection"] = projection
        };
        foreach (string name in new[] { "title", "description", "consumerIds", "surface", "environmentDialect" })
            if (output.ContainsKey(name)) artifact[name] = output[name]!.DeepClone();
        return artifact;
    }

    /// <summary>Combines an output-level guard with any guard already on the projection.</summary>
    private static void Guard(JsonObject projection, JsonNode when)
    {
        projection["when"] = projection["when"] is { } existing
            ? new JsonObject { ["op"] = "all", ["operands"] = new JsonArray(existing.DeepClone(), when.DeepClone()) }
            : when.DeepClone();
    }

    /// <summary>Resolves the effective target-to-projection map of one output, following includes without cycles.</summary>
    private SortedDictionary<string, JsonNode> Bindings(string fileName)
    {
        if (_resolved.TryGetValue(fileName, out SortedDictionary<string, JsonNode>? done)) return done;
        if (!_resolving.Add(fileName)) throw new DefinitionException("authoring-include-cycle");
        if (_outputs[fileName] is not JsonObject output) throw new DefinitionException("authoring-mapping");
        string format = output["format"]!.GetValue<string>();
        var bindings = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        if (output["includeBindings"] is { } includes)
            foreach (JsonNode? include in includes.AsArray())
            {
                string included = include!.GetValue<string>();
                if (!_outputs.ContainsKey(included) || !SameTargets(_outputs[included]!["format"]!.GetValue<string>(), format))
                    throw new DefinitionException("authoring-mapping");
                foreach ((string target, JsonNode node) in Bindings(included)) bindings[target] = node.DeepClone();
            }
        if (output["bindObject"] is { } objects)
            foreach (JsonNode? entry in objects.AsArray())
                foreach ((string target, JsonNode node) in ObjectBindings(format, entry!)) bindings[target] = node;
        if (output["bindings"] is JsonObject explicitBindings)
        {
            if (explicitBindings.Count == 0) throw new DefinitionException("authoring-mapping");
            foreach ((string target, JsonNode? value) in explicitBindings) bindings[target] = Binding(value);
        }
        if (bindings.Count is < 1 or > MaximumBindings) throw new DefinitionException("authoring-mapping");
        _resolving.Remove(fileName);
        _resolved[fileName] = bindings;
        return bindings;
    }

    /// <summary>Dotenv bindings target keys; JSON and YAML bindings share the JSON Pointer target space.</summary>
    private static bool SameTargets(string left, string right) => left == right || left is "json" or "yaml" && right is "json" or "yaml";

    private static JsonObject Binding(JsonNode? value) => value switch
    {
        JsonValue pointer when pointer.TryGetValue(out string? path) => Copy(new JsonObject { ["scope"] = "root", ["path"] = path }),
        JsonObject literal when literal.Count == 1 && literal.ContainsKey("literal") =>
            new JsonObject { ["op"] = "literal", ["value"] = literal["literal"]?.DeepClone() },
        JsonObject reference => Copy(reference.DeepClone().AsObject()),
        _ => throw new DefinitionException("authoring-mapping")
    };

    /// <summary>Binds each member of a configuration object, named by its key, optionally filtered and nested.</summary>
    private List<KeyValuePair<string, JsonNode>> ObjectBindings(string format, JsonNode entry)
    {
        JsonObject declaration = entry is JsonValue shorthand && shorthand.TryGetValue(out string? sourcePath)
            ? new JsonObject { ["source"] = sourcePath } : entry as JsonObject ?? throw new DefinitionException("authoring-mapping");
        if (declaration.Any(pair => pair.Key is not ("source" or "target" or "only" or "except")))
            throw new DefinitionException("authoring-member");
        string source = declaration["source"]!.GetValue<string>();
        string? target = declaration["target"]?.GetValue<string>();
        if (format == "dotenv" && target is not null || format is not ("dotenv" or "json" or "yaml"))
            throw new DefinitionException("authoring-mapping");
        string[] members = ObjectMembers(source);
        var only = Names(declaration["only"]);
        var except = Names(declaration["except"]);
        if (only?.Any(name => !members.Contains(name, StringComparer.Ordinal)) == true
            || except?.Any(name => !members.Contains(name, StringComparer.Ordinal)) == true)
            throw new DefinitionException("authoring-mapping");
        DefinitionPath sourceRoot = DefinitionPath.Parse(source);
        DefinitionPath targetRoot = DefinitionPath.Parse(target ?? "");
        var bindings = new List<KeyValuePair<string, JsonNode>>();
        foreach (string member in members)
        {
            if (only is not null && !only.Contains(member) || except?.Contains(member) == true) continue;
            string targetKey = format == "dotenv" ? member : DefinitionPath.FromSegments([.. targetRoot.Segments, member]).ToPointer();
            bindings.Add(new(targetKey, Copy(new JsonObject
            {
                ["scope"] = "root",
                ["path"] = DefinitionPath.FromSegments([.. sourceRoot.Segments, member]).ToPointer()
            })));
        }
        return bindings;
    }

    /// <summary>Lists the declared members of an object schema in the lowered configuration, following local refs.</summary>
    private string[] ObjectMembers(string pointer)
    {
        JsonObject? schema = _configuration;
        foreach (string segment in DefinitionPath.Parse(pointer).Segments)
            schema = Dereference(Dereference(schema)?["properties"]?[segment] as JsonObject);
        schema = Dereference(schema);
        if (schema?["properties"] is not JsonObject properties || properties.Count == 0)
            throw new DefinitionException("authoring-mapping");
        return properties.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
    }

    private JsonObject? Dereference(JsonObject? schema)
    {
        for (int hop = 0; hop < 32 && schema?["$ref"] is JsonValue reference && reference.TryGetValue(out string? target); hop++)
        {
            if (!target.StartsWith("#/$defs/", StringComparison.Ordinal)) return null;
            schema = _configuration["$defs"]?[DefinitionPath.Parse(target[1..]).Segments[^1]] as JsonObject;
        }
        return schema;
    }

    private static HashSet<string>? Names(JsonNode? names) => names is null ? null
        : names.AsArray().Select(name => name!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    private static JsonNode Project(string format, SortedDictionary<string, JsonNode> bindings)
    {
        if (format == "markdown") throw new DefinitionException("authoring-mapping");
        var members = new JsonObject();
        var paths = new List<IReadOnlyList<string>>();
        JsonNode? rootProjection = null;
        foreach ((string target, JsonNode value) in bindings)
        {
            if (format == "dotenv")
            {
                if (!IsDotenvKey(target)) throw new DefinitionException("authoring-mapping");
                members.Add(target, value.DeepClone());
                continue;
            }
            if (format is not ("json" or "yaml")) throw new DefinitionException("authoring-mapping");
            IReadOnlyList<string> segments = DefinitionPath.Parse(target).Segments;
            foreach (IReadOnlyList<string> previous in paths)
                if (previous.Take(Math.Min(previous.Count, segments.Count))
                    .SequenceEqual(segments.Take(Math.Min(previous.Count, segments.Count)), StringComparer.Ordinal))
                    throw new DefinitionException("authoring-target-overlap");
            paths.Add(segments);
            if (segments.Count == 0) { rootProjection = value.DeepClone(); continue; }
            JsonObject current = members;
            for (int index = 0; index < segments.Count - 1; index++)
            {
                if (!current.ContainsKey(segments[index]))
                    current[segments[index]] = new JsonObject { ["op"] = "object", ["members"] = new JsonObject() };
                current = current[segments[index]]!["members"]!.AsObject();
            }
            current.Add(segments[^1], value.DeepClone());
        }
        return rootProjection ?? new JsonObject { ["op"] = "object", ["members"] = members };
    }

    private static JsonObject Copy(JsonObject reference) => new() { ["op"] = "copy", ["reference"] = reference };

    private static void Charge(JsonNode? node, CompositionBudget budget, int depth)
    {
        budget.Node(depth);
        if (node is JsonObject map)
            foreach ((string key, JsonNode? value) in map)
            {
                budget.Scalar(key);
                Charge(value, budget, checked(depth + 1));
            }
        else if (node is JsonArray array)
            foreach (JsonNode? child in array) Charge(child, budget, checked(depth + 1));
        else if (node is JsonValue value && value.TryGetValue<string>(out string? text)) budget.Scalar(text);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool ValidIdentifier(string value) => value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    private static bool IsDotenvKey(string key) => key.Length > 0 && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
