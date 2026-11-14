namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json.Nodes;

/// <summary>
/// Inlines named root <c>predicates</c> into every authoring predicate position. A reference is written as
/// <c>{"op":"predicate","name":"aiEnabled"}</c>. References must be acyclic and at most
/// <see cref="MaximumReferenceDepth"/> levels deep; the runtime definition only ever sees plain condition trees.
/// </summary>
internal sealed class DefinitionPredicateLowering
{
    internal const int MaximumReferenceDepth = 16;
    private const int MaximumPredicates = 256;
    private const string ReferenceOperation = "predicate";

    private readonly JsonObject _declared;
    private readonly Dictionary<string, (JsonNode Tree, int Height)> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);

    private DefinitionPredicateLowering(JsonObject declared) => _declared = declared;

    /// <summary>Removes <c>predicates</c> from the authoring root and inlines every reference in place.</summary>
    internal static void Lower(JsonObject root)
    {
        JsonObject declared = root["predicates"] as JsonObject ?? new JsonObject();
        if (root.ContainsKey("predicates") && root["predicates"] is not JsonObject)
            throw new DefinitionException("predicate-invalid");
        if (declared.Count > MaximumPredicates) throw new DefinitionException("predicate-limit");
        foreach ((string name, _) in declared)
            if (name.Length is 0 or > 64 || !char.IsAsciiLetter(name[0])
                || !name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
                throw new DefinitionException("predicate-invalid");
        var lowering = new DefinitionPredicateLowering(declared);
        foreach ((string name, _) in declared) _ = lowering.Expand(name);
        root.Remove("predicates");
        if (root["configuration"] is JsonObject configuration) lowering.Schema(configuration);
        if (root["outputs"] is JsonObject outputs)
            foreach ((_, JsonNode? output) in outputs)
                if (output is JsonObject artifact)
                {
                    lowering.Replace(artifact, "when");
                    if (artifact["projection"] is JsonObject projection) lowering.Projection(projection);
                }
        if (root["presentation"] is JsonObject presentation)
        {
            if (presentation["outputSelector"]?["options"] is JsonArray options)
                foreach (JsonNode? option in options)
                    if (option?["nextActions"] is JsonArray actions)
                        foreach (JsonNode? action in actions)
                            if (action is JsonObject nextAction) lowering.Replace(nextAction, "proposedWhen");
            foreach (string collection in new[] { "sections", "journeys" })
                if (presentation[collection] is JsonArray items)
                    foreach (JsonNode? item in items)
                        if (item is JsonObject entry)
                        {
                            lowering.Replace(entry, "applicableWhen");
                            if (entry["groups"] is JsonArray groups)
                                foreach (JsonNode? group in groups)
                                    if (group is JsonObject member) lowering.Replace(member, "applicableWhen");
                        }
        }
        if (root["deployment"] is JsonObject deployment) lowering.Replace(deployment, "when");
    }

    /// <summary>Expands one named predicate once; its height is the longest chain of named references below it.</summary>
    private (JsonNode Tree, int Height) Expand(string name)
    {
        if (_expanded.TryGetValue(name, out (JsonNode Tree, int Height) done)) return done;
        if (!_declared.TryGetPropertyValue(name, out JsonNode? body) || body is null)
            throw new DefinitionException("predicate-unknown");
        if (!_visiting.Add(name)) throw new DefinitionException("predicate-cycle");
        (JsonNode tree, int height) = Inline(body);
        _visiting.Remove(name);
        if (height > MaximumReferenceDepth) throw new DefinitionException("predicate-limit");
        _expanded[name] = (tree, height);
        return (tree, height);
    }

    /// <summary>Returns a copy of a predicate tree with every named reference replaced by its expansion.</summary>
    private (JsonNode Tree, int Height) Inline(JsonNode predicate)
    {
        if (predicate is not JsonObject node) throw new DefinitionException("predicate-invalid");
        if (node["op"] is JsonValue operation && operation.TryGetValue(out string? op) && op == ReferenceOperation)
        {
            if (node.Count != 2 || node["name"] is not JsonValue reference || !reference.TryGetValue(out string? name))
                throw new DefinitionException("predicate-invalid");
            (JsonNode tree, int height) = Expand(name);
            return (tree.DeepClone(), height + 1);
        }
        JsonObject copy = node.DeepClone().AsObject();
        int deepest = 0;
        if (copy["operands"] is JsonArray operands)
            for (int index = 0; index < operands.Count; index++)
                if (operands[index] is { } operand)
                {
                    (JsonNode tree, int height) = Inline(operand);
                    operands[index] = tree;
                    deepest = Math.Max(deepest, height);
                }
        return (copy, deepest);
    }

    private void Replace(JsonObject owner, string member)
    {
        if (owner[member] is { } predicate) owner[member] = Inline(predicate).Tree;
    }

    private void Schema(JsonObject schema)
    {
        if (schema["x-configurator"] is JsonObject metadata) Replace(metadata, "applicableWhen");
        foreach (string container in new[] { "properties", "$defs" })
            if (schema[container] is JsonObject children)
                foreach ((_, JsonNode? child) in children)
                    if (child is JsonObject nested) Schema(nested);
        foreach (string member in new[] { "items", "additionalProperties" })
            if (schema[member] is JsonObject nested) Schema(nested);
        if (schema["oneOf"] is JsonArray variants)
            foreach (JsonNode? variant in variants)
                if (variant is JsonObject nested) Schema(nested);
    }

    private void Projection(JsonObject projection)
    {
        Replace(projection, "when");
        if (projection["members"] is JsonObject members)
            foreach ((_, JsonNode? member) in members)
                if (member is JsonObject nested) Projection(nested);
        if (projection["items"] is JsonArray items)
            foreach (JsonNode? item in items)
                if (item is JsonObject nested) Projection(nested);
        if (projection["item"] is JsonObject repeated) Projection(repeated);
    }
}
