namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed class FieldDefinition
{
    internal FieldDefinition(string name, DefinitionNode node, bool required,
        FieldSensitivity inherited = FieldSensitivity.Public)
    {
        Node = node;
        Name = name;
        Id = DefinitionProfile.FieldId(name);
        Type = node.JsonType;
        string inferredTitle = string.IsNullOrWhiteSpace(name) ? "Setting" : name;
        if (inferredTitle.Any(char.IsControl)) inferredTitle = JsonSerializer.Serialize(inferredTitle);
        Title = Text("title") ?? (inferredTitle.Length <= 256 ? inferredTitle : inferredTitle[..256]);
        Required = required;
        Default = Data.TryGetProperty("default", out var descriptive) ? descriptive.Clone() : null;
        InitialValue = ExtensionValue("initialValue");
        SuggestedValue = ExtensionValue("suggestedValue");
        Choices = Array.AsReadOnly(Data.TryGetProperty("enum", out var choices)
            ? choices.EnumerateArray().Select(item => item.Clone()).ToArray() : []);
        FieldSensitivity own = (FieldSensitivity)Math.Max((int)inherited, (int)SensitivityOf(node));
        Sensitivity = EffectiveSensitivity(node, inherited);
        Disclosure = ExtensionValue("detail")?.GetString() switch
        {
            "basic" => FieldDisclosure.Essential,
            "advanced" => FieldDisclosure.Advanced,
            _ => FieldDisclosure.Guided
        };
        ActiveWhen = ExtensionValue("applicableWhen");
        Dependencies = Array.AsReadOnly(ActiveWhen is { } condition
            ? DefinitionValidator.References(condition).ToArray() : []);
        Properties = Array.AsReadOnly(node.Properties.Select(item => new FieldDefinition(item.Key, item.Value,
            node.Required.Contains(item.Key, StringComparer.Ordinal), own)).ToArray());
        Items = node.Items is { } itemSchema ? new FieldDefinition("item", itemSchema, false, own) : null;
        UniqueBy = ExtensionValue("uniqueBy") is { } path
            ? DefinitionPath.Parse(path.GetString()!).Segments : Array.Empty<string>();
        Guidance = ExtensionValue("guidance") is { } guidance ? new DefinitionFieldGuidance(guidance) : null;
    }
    public DefinitionNode Node { get; }
    internal JsonElement Data => Node.Keywords;
    internal IReadOnlyList<string> Dependencies { get; }
    internal JsonElement? ActiveWhen { get; }
    public string Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string Title { get; }
    public string? Help => Text("description");
    public string? Format => Text("format");
    public DefinitionFieldGuidance? Guidance { get; }
    public bool Required { get; }
    public string? ItemType => Node.Items?.JsonType;
    public FieldDefinition? Items { get; }
    public int? MinimumItems => Data.TryGetProperty("minItems", out var value) ? value.GetInt32() : null;
    public int? MaximumItems => Data.TryGetProperty("maxItems", out var value) ? value.GetInt32() : null;
    public IReadOnlyList<FieldDefinition> Properties { get; }
    public IReadOnlyList<string> UniqueBy { get; }
    public FieldSensitivity DeclaredSensitivity => SensitivityOf(Node);
    public FieldSensitivity Sensitivity { get; }
    public FieldDisclosure Disclosure { get; }
    public JsonElement? Default { get; }
    public JsonElement? InitialValue { get; }
    public JsonElement? SuggestedValue { get; }
    public IReadOnlyList<JsonElement> Choices { get; }
    public JsonElement Constraints => Data.Clone();
    public override string ToString() => $"{nameof(FieldDefinition)}:{Id}:{Type}:{Sensitivity}";
    internal string? Text(string name) => Data.TryGetProperty(name, out var value) ? value.GetString() : null;

    private JsonElement? ExtensionValue(string name) =>
        Data.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty(name, out var value)
            ? value.Clone() : null;

    /// <summary>Returns the declared node sensitivity, before inherited and descendant floors.</summary>
    public static FieldSensitivity SensitivityOf(DefinitionNode node) => DefinitionSensitivityPolicy.SensitivityOf(node);

    internal static FieldSensitivity EffectiveSensitivity(DefinitionNode node,
        FieldSensitivity inherited = FieldSensitivity.Public)
    {
        var sensitivity = (FieldSensitivity)Math.Max((int)inherited, (int)SensitivityOf(node));
        foreach (DefinitionNode child in node.Properties.Values.Concat(node.Variants)
            .Concat(node.Items is null ? [] : new[] { node.Items })
            .Concat(node.AdditionalProperties is null ? [] : new[] { node.AdditionalProperties }))
            sensitivity = (FieldSensitivity)Math.Max((int)sensitivity, (int)EffectiveSensitivity(child));
        return sensitivity;
    }
}
