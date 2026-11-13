namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using Configurator.Core.Composition;

/// <summary>Immutable public schema positions, separate from canonical definition identity.</summary>
public sealed class DefinitionSourceMap
{
    private static readonly FrozenSet<string> SchemaMembers = new[]
    {
        "$ref", "$defs", "type", "title", "description", "enum", "const", "properties", "required",
        "additionalProperties", "items", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems",
        "minProperties", "maxProperties", "format", "default", "readOnly", "writeOnly", "deprecated", "examples",
        "x-configurator", "if", "then", "else", "dependentRequired", "oneOf", "exclusiveMinimum",
        "exclusiveMaximum", "multipleOf", "uniqueItems"
    }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> MetadataMembers = new[]
    {
        "sensitivity", "initialValue", "suggestedValue", "notBlank", "detail", "guidance", "secret",
        "choices", "units", "uniqueBy", "applicableWhen", "requiredWhen", "propertyOrder", "order", "placeholder", "defaultText",
        "deprecationMessage", "widget", "rows", "syntax", "booleanLabels", "itemTitlePointer", "displayRawKey"
    }.ToFrozenSet(StringComparer.Ordinal);
    private readonly FrozenDictionary<string, DefinitionSourceSpan> _spans;

    internal DefinitionSourceMap(IEnumerable<KeyValuePair<string, DefinitionSourceSpan>> spans) =>
        _spans = spans.ToFrozenDictionary(StringComparer.Ordinal);

    public static DefinitionSourceMap Empty { get; } = new(
        Array.Empty<KeyValuePair<string, DefinitionSourceSpan>>());

    public IReadOnlyDictionary<string, DefinitionSourceSpan> Spans => _spans;

    public bool TryGetSpan(string schemaPath, out DefinitionSourceSpan span) =>
        _spans.TryGetValue(schemaPath, out span);

    /// <summary>Projects syntax positions, never data values or unrecognized member names.</summary>
    internal static DefinitionSourceMap FromSyntax(CompositionMap root)
    {
        var spans = new Dictionary<string, DefinitionSourceSpan>(StringComparer.Ordinal);
        int characters = 0;
        Add("", root);
        foreach (string name in new[]
        {
            "$schema", "schemaVersion", "profile", "identity", "familyId", "revision", "project",
            "applicationVersions", "entryPoints", "locales", "requiredCapabilities", "configuration",
            "outputs", "artifacts", "presentation", "localization", "annotations"
        })
        {
            if (!root.Entries.TryGetValue(name, out CompositionNode? node)) continue;
            string path = "/" + name;
            if (name == "configuration") Schema(path, node);
            else Add(path, node);
        }
        if (root.Entries.TryGetValue("identity", out CompositionNode? identity) && identity is CompositionMap identityMap)
            FixedMembers("/identity", identityMap, ["familyId", "revision"]);
        if (root.Entries.TryGetValue("project", out CompositionNode? project) && project is CompositionMap projectMap)
            FixedMembers("/project", projectMap, ["id", "name", "publisher", "description", "repository", "definitionLicense"]);
        if (root.Entries.TryGetValue("artifacts", out CompositionNode? artifacts) && artifacts is CompositionSequence sequence)
            for (int index = 0; index < sequence.Entries.Count; index++) Artifact(index, sequence.Entries[index]);
        // Source output names lower to deterministic artifact ordinals, not native paths.
        if (root.Entries.TryGetValue("outputs", out CompositionNode? outputs) && outputs is CompositionMap outputMap)
        {
            int index = 0;
            foreach (CompositionNode output in outputMap.Entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value))
                Artifact(index++, output);
        }
        return new DefinitionSourceMap(spans);

        bool Add(string path, CompositionNode node)
        {
            if (path.Length > CompositionLimits.DefaultScalarCharacters
                || spans.Count >= CompositionLimits.DefaultNormalizedNodes
                || path.Length > CompositionLimits.DefaultAggregateScalarCharacters - characters)
                return false;
            if (node.SourceSpan is { } span && spans.TryAdd(path, span)) characters += path.Length;
            return true;
        }

        void FixedMembers(string path, CompositionMap node, IEnumerable<string> names)
        {
            foreach (string name in names)
                if (node.Entries.TryGetValue(name, out CompositionNode? child)) Add(path + "/" + name, child);
        }

        void Artifact(int index, CompositionNode node)
        {
            string path = "/artifacts/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!Add(path, node) || node is not CompositionMap map) return;
            FixedMembers(path, map, ["id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection", "bindings"]);
        }

        void Schema(string path, CompositionNode node)
        {
            if (!Add(path, node) || node is not CompositionMap map) return;
            foreach ((string name, CompositionNode child) in map.Entries)
            {
                if (!SchemaMembers.Contains(name)) continue;
                string memberPath = path + "/" + name;
                if (!Add(memberPath, child)) continue;
                if (name is "properties" or "$defs" && child is CompositionMap children)
                    foreach ((string declaredName, CompositionNode schema) in children.Entries)
                        Schema(memberPath + "/" + declaredName.Replace("~", "~0", StringComparison.Ordinal)
                            .Replace("/", "~1", StringComparison.Ordinal), schema);
                else if (name is "items" or "additionalProperties" or "if" or "then" or "else")
                    Schema(memberPath, child);
                else if (name == "oneOf" && child is CompositionSequence variants)
                    for (int index = 0; index < variants.Entries.Count; index++)
                        Schema(memberPath + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), variants.Entries[index]);
                else if (name == "x-configurator" && child is CompositionMap metadata)
                {
                    FixedMembers(memberPath, metadata, MetadataMembers);
                    if (metadata.Entries.TryGetValue("secret", out CompositionNode? secret) && secret is CompositionMap policy)
                    {
                        FixedMembers(memberPath + "/secret", policy, ["kind", "acquisition", "algorithm", "encoding", "constraints", "generation"]);
                        foreach (string section in new[] { "constraints", "generation" })
                            if (policy.Entries.TryGetValue(section, out CompositionNode? content) && content is CompositionMap members)
                                FixedMembers(memberPath + "/secret/" + section, members, ["characterSet", "allowWhitespace", "maxUtf8Bytes",
                                    "minLowercase", "minUppercase", "minDigits", "minSymbols", "allowedSymbols", "forbiddenCharacters",
                                    "minBytes", "maxBytes", "keySize", "curve", "memoryKiB", "iterations", "parallelism", "saltBytes",
                                    "derivedBytes", "cost", "length", "bytes", "input", "inputEncoding", "password"]);
                    }
                }
            }
        }
    }

    public override string ToString() => $"{nameof(DefinitionSourceMap)}:Count={_spans.Count}";
}
