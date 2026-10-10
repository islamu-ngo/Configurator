namespace Configurator.Core.Definitions.Authoring;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;

/// <summary>Expands closed maintainer data and delegates final identity and privacy admission to Core.</summary>
public static class DefinitionAuthoringCompiler
{
    public static SetupDefinition Compile(DefinitionAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            JsonObject root = JsonNode.Parse(document.CanonicalBytes.Span)!.AsObject();
            Only(root, "$schema", "identity", "project", "applicationVersions", "entryPoints", "locales", "fields", "outputs");
            RejectNullMembers(root);
            JsonObject identity = root["identity"]!.AsObject();
            Only(identity, "familyId", "revision");
            JsonObject project = root["project"]!.AsObject();
            Only(project, "id", "name", "publisher", "description", "repository", "definitionLicense");
            RejectNullMembers(project);
            ValidateProject(project);
            JsonObject fields = root["fields"]!.AsObject();
            JsonObject outputs = root["outputs"]!.AsObject();
            if (fields.ContainsKey("$item")) throw new SetupDefinitionException("authoring-reserved");
            if (fields.Count > 1024 || outputs.Count is < 1 or > 64)
                throw new SetupDefinitionException("document-limit");
            var generatedBudget = new CompositionBudget(SetupCompositionLimits.Default);
            generatedBudget.Node(2);
            generatedBudget.Node(2);
            var ids = fields.ToDictionary(pair => pair.Key, pair => FieldId(pair.Key), StringComparer.Ordinal);
            if (ids.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count)
                throw new SetupDefinitionException("field-duplicate");
            var expandedFields = new JsonArray();
            foreach ((string key, JsonNode? value) in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                JsonObject expanded = ExpandField(key, value!.AsObject(), ids[key], ids);
                Charge(expanded, generatedBudget, 3);
                expandedFields.Add(expanded);
            }

            JsonArray artifacts = ExpandOutputs(outputs, fields, ids, generatedBudget);
            var runtime = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["familyId"] = identity["familyId"]!.DeepClone(),
                ["revision"] = identity["revision"]!.DeepClone(),
                ["applicationId"] = project["id"]!.DeepClone(),
                ["title"] = project["name"]!.DeepClone(),
                ["publisher"] = project["publisher"]?.DeepClone() ?? project["name"]!.DeepClone(),
                ["applicationVersions"] = root["applicationVersions"]!.DeepClone(),
                ["entryPoints"] = root["entryPoints"]?.DeepClone() ?? new JsonArray("setup"),
                ["locales"] = root["locales"]?.DeepClone() ?? new JsonArray("en")
            };
            Charge(runtime, generatedBudget, 1);
            runtime["fields"] = expandedFields;
            runtime["artifacts"] = artifacts;
            return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
        }
        catch (SetupDefinitionException) { throw; }
        catch (SetupCompositionException) { throw new SetupDefinitionException("document-limit"); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            throw new SetupDefinitionException("authoring-invalid");
        }
    }

    private static void ValidateProject(JsonObject project)
    {
        foreach (string member in new[] { "id", "name", "publisher", "description", "definitionLicense" })
            if (project[member] is JsonNode value) ValidateProjectText(value, member == "description" ? 4096 : 256);
        if (project["repository"] is JsonNode repository && (!Uri.TryCreate(repository.GetValue<string>(), UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0))
            throw new SetupDefinitionException("authoring-repository");
    }

    private static void ValidateProjectText(JsonNode value, int maximum)
    {
        using JsonDocument text = JsonDocument.Parse(value.ToJsonString());
        _ = SetupDefinitionCodec.Text(text.RootElement, maximum);
    }

    private static JsonArray ExpandOutputs(JsonObject outputs, JsonObject fields, Dictionary<string, string> ids, CompositionBudget budget)
    {
        var artifacts = new JsonArray();
        foreach ((string fileName, JsonNode? value) in outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            JsonObject output = value!.AsObject();
            Only(output, "id", "format", "applySemantics", "restart", "bindings", "projection");
            RejectNullMembers(output);
            string format = output["format"]!.GetValue<string>();
            JsonNode projection;
            if (output.ContainsKey("projection"))
            {
                if (output.ContainsKey("bindings")) throw new SetupDefinitionException("authoring-mapping");
                projection = output["projection"]!;
                output.Remove("projection");
                RewriteProjection(projection.AsObject(), ids);
                Charge(projection, budget, 4);
            }
            else
            {
                if (format == "markdown") throw new SetupDefinitionException("authoring-mapping");
                IEnumerable<KeyValuePair<string, JsonNode?>> bindings = output["bindings"] is JsonNode explicitBindings
                    ? explicitBindings.AsObject() : fields.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Key)));
                var members = new JsonObject();
                budget.Node(4);
                budget.Node(5);
                budget.Node(5);
                budget.Scalar("op");
                budget.Scalar("object");
                budget.Scalar("members");
                foreach ((string target, JsonNode? binding) in bindings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    string field = binding!.GetValue<string>();
                    if (!ids.TryGetValue(field, out string? id)) throw new SetupDefinitionException("authoring-reference");
                    if (format == "dotenv" && (!IsDotenvKey(target)
                        || fields[field]!["type"]!.GetValue<string>() is "object" or "list"))
                        throw new SetupDefinitionException("authoring-mapping");
                    budget.Scalar(target);
                    budget.Scalar(id);
                    budget.Scalar("op");
                    budget.Scalar("copy");
                    budget.Scalar("field");
                    budget.Node(6);
                    budget.Node(7);
                    budget.Node(7);
                    members.Add(target, new JsonObject { ["op"] = "copy", ["field"] = id });
                }
                projection = new JsonObject { ["op"] = "object", ["members"] = members };
            }
            var artifact = new JsonObject
            {
                ["id"] = output["id"]?.DeepClone() ?? JsonValue.Create(OutputId(fileName)),
                ["fileName"] = fileName,
                ["format"] = format,
                ["applySemantics"] = output["applySemantics"]?.DeepClone() ?? JsonValue.Create("generationOnly"),
                ["restart"] = output["restart"]?.DeepClone() ?? JsonValue.Create("none")
            };
            Charge(artifact, budget, 3);
            budget.Scalar("projection");
            artifact["projection"] = projection;
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

    public static string FieldId(string logicalKey) => ValidIdentifier(logicalKey) ? logicalKey
        : "f-" + Hash("field:/" + logicalKey.Normalize(NormalizationForm.FormC).Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));

    public static string OutputId(string fileName) => ValidIdentifier("out-" + fileName) ? "out-" + fileName
        : "o-" + Hash("output:" + fileName.Normalize(NormalizationForm.FormC));

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool ValidIdentifier(string value) => value.Length is > 0 and <= 128 && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    private static bool IsDotenvKey(string key) => key.Length > 0 && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static JsonObject ExpandField(string key, JsonObject source, string id, IReadOnlyDictionary<string, string> ids)
    {
        if (source.ContainsKey("id")) throw new SetupDefinitionException("authoring-member");
        JsonObject field = source.DeepClone().AsObject();
        field["id"] = id;
        if (!field.ContainsKey("title")) field["title"] = Humanize(key);
        if (!field.ContainsKey("sensitivity")) field["sensitivity"] = "sensitive";
        if (!field.ContainsKey("disclosure")) field["disclosure"] = "guided";
        if (field["properties"] is JsonNode properties)
        {
            var children = new JsonArray();
            foreach ((string name, JsonNode? child) in properties.AsObject().OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!ValidIdentifier(name)) throw new SetupDefinitionException("authoring-property");
                children.Add(ExpandField(name, child!.AsObject(), name, ids));
            }
            field["properties"] = children;
        }
        foreach (string member in new[] { "activeWhen", "requiredWhen" })
            if (field[member] is JsonNode condition) RewriteCondition(condition.AsObject(), ids);
        return field;
    }

    private static string Humanize(string key)
    {
        string title = key.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ').Trim();
        return title.Length == 0 ? "Setting" : title.Length <= 256 ? title : title[..256];
    }

    private static void RewriteReference(JsonObject node, IReadOnlyDictionary<string, string> ids, bool itemAllowed)
    {
        string field = node["field"]!.GetValue<string>();
        if (itemAllowed && field == "$item") return;
        if (!ids.TryGetValue(field, out string? id)) throw new SetupDefinitionException("authoring-reference");
        node["field"] = id;
    }

    private static void RewriteCondition(JsonObject condition, IReadOnlyDictionary<string, string> ids)
    {
        if (condition["operands"] is JsonArray operands)
            foreach (JsonNode? operand in operands) RewriteCondition(operand!.AsObject(), ids);
        else if (condition.ContainsKey("field")) RewriteReference(condition, ids, false);
    }

    private static void RewriteProjection(JsonObject projection, IReadOnlyDictionary<string, string> ids)
    {
        string op = projection["op"]!.GetValue<string>();
        if (projection["when"] is JsonNode condition) RewriteCondition(condition.AsObject(), ids);
        if (op is "copy" or "repeat") RewriteReference(projection, ids, op == "copy");
        if (op == "object")
            foreach ((_, JsonNode? child) in projection["members"]!.AsObject()) RewriteProjection(child!.AsObject(), ids);
        if (op == "list")
            foreach (JsonNode? child in projection["items"]!.AsArray()) RewriteProjection(child!.AsObject(), ids);
        if (op == "repeat") RewriteProjection(projection["item"]!.AsObject(), ids);
    }

    private static void Only(JsonObject value, params string[] allowed)
    {
        if (value.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal)))
            throw new SetupDefinitionException("authoring-member");
    }

    private static void RejectNullMembers(JsonObject value)
    {
        if (value.Any(pair => pair.Value is null)) throw new SetupDefinitionException("authoring-invalid");
    }
}
