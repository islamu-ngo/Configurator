namespace Configurator.Core.Definitions.Authoring;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;

/// <summary>Lowers source output mappings and delegates recursive semantics to the sole runtime admission owner.</summary>
public static class DefinitionAuthoringCompiler
{
    public static SetupDefinition Compile(DefinitionAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            JsonObject root = JsonNode.Parse(document.CanonicalBytes.Span)!.AsObject();
            Only(root, "$schema", "identity", "project", "applicationVersions", "entryPoints", "configuration",
                "outputs", "presentation", "localization", "annotations");
            if (root["$schema"]!.GetValue<string>() != DefinitionAuthoringCodec.SchemaId)
                throw new SetupDefinitionException("authoring-schema");
            JsonObject identity = root["identity"]!.AsObject();
            Only(identity, "familyId", "revision");
            JsonObject project = root["project"]!.DeepClone().AsObject();
            if (!project.ContainsKey("publisher")) project["publisher"] = project["name"]!.DeepClone();
            var locales = new SortedSet<string>(StringComparer.Ordinal) { "en" };
            if (root["localization"] is JsonObject localization)
            {
                locales.Clear();
                locales.Add(localization["defaultLocale"]!.GetValue<string>());
                foreach ((string locale, _) in localization["translations"]!.AsObject()) locales.Add(locale);
            }
            var runtime = new JsonObject
            {
                ["schemaVersion"] = DefinitionProfile.RuntimeSchemaVersion,
                ["familyId"] = identity["familyId"]!.DeepClone(),
                ["revision"] = identity["revision"]!.DeepClone(),
                ["project"] = project,
                ["applicationVersions"] = root["applicationVersions"]!.DeepClone(),
                ["entryPoints"] = root.ContainsKey("entryPoints") ? root["entryPoints"]!.DeepClone() : new JsonArray("setup"),
                ["locales"] = new JsonArray(locales.Select(locale => (JsonNode?)JsonValue.Create(locale)).ToArray()),
                ["configuration"] = root["configuration"]!.DeepClone()
            };
            foreach (string name in new[] { "presentation", "localization", "annotations" })
                if (root.ContainsKey(name)) runtime[name] = root[name]!.DeepClone();
            runtime["artifacts"] = ExpandOutputs(root["outputs"]!.AsObject());
            return SetupDefinitionCodec.AdmitCompiled(runtime);
        }
        catch (SetupDefinitionException) { throw; }
        catch (SetupCompositionException) { throw new SetupDefinitionException("document-limit"); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            throw new SetupDefinitionException("authoring-invalid");
        }
    }

    private static JsonArray ExpandOutputs(JsonObject outputs)
    {
        if (outputs.Count is < 1 or > 64) throw new SetupDefinitionException("artifact-limit");
        var artifacts = new JsonArray();
        var budget = new CompositionBudget(SetupCompositionLimits.Default);
        foreach ((string fileName, JsonNode? value) in outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            JsonObject output = value!.AsObject();
            Only(output, "id", "format", "targetUpdateMode", "restartRequirement", "bindings", "projection");
            string format = output["format"]!.GetValue<string>();
            JsonNode projection;
            if (output.ContainsKey("projection"))
            {
                if (output.ContainsKey("bindings")) throw new SetupDefinitionException("authoring-mapping");
                projection = output["projection"]!.DeepClone();
            }
            else if (output.ContainsKey("bindings"))
            {
                JsonObject bindings = output["bindings"]!.AsObject();
                if (bindings.Count is < 1 or > 1024 || format == "markdown") throw new SetupDefinitionException("authoring-mapping");
                var paths = new List<IReadOnlyList<string>>();
                JsonObject members = new();
                JsonObject objectProjection = new() { ["op"] = "object", ["members"] = members };
                JsonNode? rootProjection = null;
                foreach ((string target, JsonNode? reference) in bindings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    JsonObject copy = new() { ["op"] = "copy", ["reference"] = reference!.DeepClone() };
                    if (format == "dotenv")
                    {
                        if (!IsDotenvKey(target)) throw new SetupDefinitionException("authoring-mapping");
                        members.Add(target, copy);
                        continue;
                    }
                    if (format is not ("json" or "yaml")) throw new SetupDefinitionException("authoring-mapping");
                    IReadOnlyList<string> segments = DefinitionPath.Parse(target).Segments;
                    foreach (IReadOnlyList<string> previous in paths)
                        if (previous.Take(Math.Min(previous.Count, segments.Count))
                            .SequenceEqual(segments.Take(Math.Min(previous.Count, segments.Count)), StringComparer.Ordinal))
                            throw new SetupDefinitionException("authoring-target-overlap");
                    paths.Add(segments);
                    if (segments.Count == 0) rootProjection = copy;
                    else
                    {
                        JsonObject current = members;
                        for (int index = 0; index < segments.Count - 1; index++)
                        {
                            if (!current.ContainsKey(segments[index]))
                                current[segments[index]] = new JsonObject { ["op"] = "object", ["members"] = new JsonObject() };
                            current = current[segments[index]]!["members"]!.AsObject();
                        }
                        current.Add(segments[^1], copy);
                    }
                }
                projection = rootProjection ?? objectProjection;
            }
            else
            {
                if (format is not ("json" or "yaml")) throw new SetupDefinitionException("authoring-mapping");
                projection = new JsonObject
                {
                    ["op"] = "copy",
                    ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "" }
                };
            }
            var artifact = new JsonObject
            {
                ["id"] = output.ContainsKey("id") ? output["id"]!.DeepClone() : JsonValue.Create(OutputId(fileName)),
                ["fileName"] = fileName,
                ["format"] = format,
                ["targetUpdateMode"] = output.ContainsKey("targetUpdateMode") ? output["targetUpdateMode"]!.DeepClone() : JsonValue.Create("unspecified"),
                ["restartRequirement"] = output.ContainsKey("restartRequirement") ? output["restartRequirement"]!.DeepClone() : JsonValue.Create("unspecified"),
                ["projection"] = projection
            };
            Charge(artifact, budget, 3);
            artifacts.Add(artifact);
        }
        return artifacts;
    }

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

    public static string OutputId(string fileName) => ValidIdentifier("out-" + fileName) ? "out-" + fileName : "o-" + Hash("output:" + fileName);
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool ValidIdentifier(string value) => value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    private static bool IsDotenvKey(string key) => key.Length > 0 && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    private static void Only(JsonObject node, params string[] allowed)
    {
        if (node.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal)))
            throw new SetupDefinitionException("authoring-member");
    }
}
