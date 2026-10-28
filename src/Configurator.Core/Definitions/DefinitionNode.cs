namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionNodeType { Text, Boolean, WholeNumber, Number, Mapping, Sequence }

/// <summary>Immutable recursive shape data. Constructing a shape does not admit a definition or validate answers.</summary>
public sealed record DefinitionNode
{
    private static readonly JsonElement EmptyKeywords = JsonSerializer.SerializeToElement(new { });
    private readonly string _keywordIdentity;

    public DefinitionNode(DefinitionNodeType type, bool nullable = false,
        IEnumerable<KeyValuePair<string, DefinitionNode>>? properties = null,
        IEnumerable<string>? required = null, DefinitionNode? items = null,
        DefinitionNode? additionalProperties = null, IEnumerable<DefinitionNode>? variants = null,
        JsonElement? keywords = null)
    {
        if (!Enum.IsDefined(type)) throw new SetupDefinitionException("schema-type");
        Type = type;
        IsNullable = nullable;
        JsonElement metadata = keywords ?? EmptyKeywords;
        if (metadata.ValueKind != JsonValueKind.Object) throw new SetupDefinitionException("schema-keywords");
        byte[] canonicalKeywords = DefinitionProfile.CanonicalizeJson(Encoding.UTF8.GetBytes(metadata.GetRawText()));
        using JsonDocument keywordDocument = JsonDocument.Parse(canonicalKeywords);
        Keywords = keywordDocument.RootElement.Clone();
        _keywordIdentity = Encoding.UTF8.GetString(canonicalKeywords);
        var propertySnapshot = new SortedDictionary<string, DefinitionNode>(StringComparer.Ordinal);
        foreach ((string name, DefinitionNode child) in properties ?? [])
        {
            if (propertySnapshot.Count == SetupCompositionLimits.DefaultMappingEntries)
                throw new SetupDefinitionException("schema-limit");
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(child);
            DefinitionProfile.ValidateScalar(name);
            if (!propertySnapshot.TryAdd(name, child)) throw new SetupDefinitionException("schema-duplicate");
        }
        var requiredSnapshot = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in required ?? [])
            if (!propertySnapshot.ContainsKey(name) || !requiredSnapshot.Add(name))
                throw new SetupDefinitionException("schema-required");
        var variantSnapshot = new List<DefinitionNode>();
        foreach (DefinitionNode variant in variants ?? [])
        {
            ArgumentNullException.ThrowIfNull(variant);
            if (variantSnapshot.Count == SetupCompositionLimits.DefaultSequenceEntries)
                throw new SetupDefinitionException("schema-limit");
            if (variant.Type != DefinitionNodeType.Mapping) throw new SetupDefinitionException("schema-variant");
            variantSnapshot.Add(variant);
        }
        if (type == DefinitionNodeType.Sequence != (items is not null)
            || type != DefinitionNodeType.Mapping && (propertySnapshot.Count != 0 || requiredSnapshot.Count != 0
                || additionalProperties is not null || variantSnapshot.Count != 0))
            throw new SetupDefinitionException("schema-shape");
        Properties = new ReadOnlyDictionary<string, DefinitionNode>(propertySnapshot);
        Required = Array.AsReadOnly(requiredSnapshot.ToArray());
        Items = items;
        AdditionalProperties = additionalProperties;
        Variants = variantSnapshot.AsReadOnly();
        ExpandedNodes = 1;
        Depth = 1;
        ScalarCharacters = propertySnapshot.Keys.Sum(name => name.Length);
        foreach (DefinitionNode child in propertySnapshot.Values.Concat(variantSnapshot)
            .Concat(items is null ? [] : new[] { items })
            .Concat(additionalProperties is null ? [] : new[] { additionalProperties }))
        {
            ExpandedNodes += child.ExpandedNodes;
            ScalarCharacters += child.ScalarCharacters;
            Depth = Math.Max(Depth, child.Depth + 1);
            if (ExpandedNodes > SetupCompositionLimits.DefaultNormalizedNodes
                || ScalarCharacters > SetupCompositionLimits.DefaultAggregateScalarCharacters
                || Depth > SetupCompositionLimits.DefaultNestingDepth)
                throw new SetupDefinitionException("schema-limit");
        }
        if (ScalarCharacters > SetupCompositionLimits.DefaultAggregateScalarCharacters)
            throw new SetupDefinitionException("schema-limit");
    }

    public DefinitionNodeType Type { get; }
    public string JsonType => Type switch
    {
        DefinitionNodeType.Text => "string",
        DefinitionNodeType.Boolean => "boolean",
        DefinitionNodeType.WholeNumber => "integer",
        DefinitionNodeType.Number => "number",
        DefinitionNodeType.Mapping => "object",
        DefinitionNodeType.Sequence => "array",
        _ => throw new InvalidOperationException()
    };
    public bool IsNullable { get; }
    /// <summary>Local public keywords only; child schemas belong to the typed recursive properties.</summary>
    public JsonElement Keywords { get; }
    public IReadOnlyDictionary<string, DefinitionNode> Properties { get; }
    public IReadOnlyList<string> Required { get; }
    public DefinitionNode? Items { get; }
    public DefinitionNode? AdditionalProperties { get; }
    public IReadOnlyList<DefinitionNode> Variants { get; }
    internal int ExpandedNodes { get; }
    internal int ScalarCharacters { get; }
    internal int Depth { get; }

    public bool Equals(DefinitionNode? other) => other is not null
        && Type == other.Type && IsNullable == other.IsNullable
        && StringComparer.Ordinal.Equals(_keywordIdentity, other._keywordIdentity)
        && Properties.SequenceEqual(other.Properties) && Required.SequenceEqual(other.Required, StringComparer.Ordinal)
        && Equals(Items, other.Items) && Equals(AdditionalProperties, other.AdditionalProperties)
        && Variants.SequenceEqual(other.Variants);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(IsNullable);
        hash.Add(_keywordIdentity, StringComparer.Ordinal);
        foreach ((string name, DefinitionNode node) in Properties)
        {
            hash.Add(name, StringComparer.Ordinal);
            hash.Add(node);
        }
        foreach (string name in Required) hash.Add(name, StringComparer.Ordinal);
        hash.Add(Items);
        hash.Add(AdditionalProperties);
        foreach (DefinitionNode variant in Variants) hash.Add(variant);
        return hash.ToHashCode();
    }

    public override string ToString() => $"{nameof(DefinitionNode)}:{Type}";
}
