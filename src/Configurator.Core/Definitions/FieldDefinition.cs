namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// Admitted field metadata. <see cref="Name"/> and <see cref="TechnicalName"/> keep the exact wire key;
/// <see cref="Title"/> resolves explicit <c>title</c> first and deterministic humanization otherwise.
/// </summary>
public sealed class FieldDefinition
{
    private const int MaximumTitleLength = 256;

    internal FieldDefinition(string name, DefinitionNode node, bool required,
        FieldSensitivity inherited = FieldSensitivity.Public)
    {
        Node = node;
        Name = name;
        Id = DefinitionProfile.FieldId(name);
        Type = node.JsonType;
        string? explicitTitle = Text("title");
        HasExplicitTitle = explicitTitle is not null;
        DisplayTitle = explicitTitle ?? (node.PresentationMetadata.DisplayRawKey ? RawTitle(name) : InferredTitle(name));
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
        RequiredWhen = ExtensionValue("requiredWhen");
        Dependencies = Array.AsReadOnly(DefinitionValidator.NodeConditions(node)
            .SelectMany(DefinitionValidator.References).Distinct(StringComparer.Ordinal).ToArray());
        Properties = Array.AsReadOnly(node.OrderedPropertyNames.Select(property => new FieldDefinition(property,
            node.Properties[property], node.Required.Contains(property, StringComparer.Ordinal), own)).ToArray());
        Items = node.Items is { } itemSchema ? new FieldDefinition("item", itemSchema, false, own) : null;
        UniqueBy = DefinitionUniqueKey.Of(node);
        Guidance = ExtensionValue("guidance") is { } guidance ? new DefinitionFieldGuidance(guidance) : null;
    }
    public DefinitionNode Node { get; }
    internal JsonElement Data => Node.Keywords;
    internal IReadOnlyList<string> Dependencies { get; }
    internal JsonElement? ActiveWhen { get; }
    /// <summary>Data-only condition under which this member must be supplied, in addition to <see cref="Required"/>.</summary>
    internal JsonElement? RequiredWhen { get; }
    public string Id { get; }
    /// <summary>The exact wire key.</summary>
    public string Name { get; }
    /// <summary>The exact wire key, named for display surfaces that show technical and humanized text side by side.</summary>
    public string TechnicalName => Name;
    public string Type { get; }
    /// <summary>The resolved display title; equal to <see cref="DisplayTitle"/>.</summary>
    public string Title => DisplayTitle;
    /// <summary>Explicit authored <c>title</c>, or the humanized wire key when the title was omitted.</summary>
    public string DisplayTitle { get; }
    public bool HasExplicitTitle { get; }
    public string? Help => Text("description");
    public string? Format => Text("format");
    public DefinitionFieldGuidance? Guidance { get; }
    public bool Required { get; }
    public string? ItemType => Node.Items?.JsonType;
    public FieldDefinition? Items { get; }
    public int? MinimumItems => Data.TryGetProperty("minItems", out var value) ? value.GetInt32() : null;
    public int? MaximumItems => Data.TryGetProperty("maxItems", out var value) ? value.GetInt32() : null;
    public IReadOnlyList<FieldDefinition> Properties { get; }
    /// <summary>Item-relative key paths; more than one path forms a composite row key.</summary>
    public IReadOnlyList<DefinitionPath> UniqueBy { get; }
    public FieldSensitivity DeclaredSensitivity => SensitivityOf(Node);
    public FieldSensitivity Sensitivity { get; }
    public FieldDisclosure Disclosure { get; }
    public JsonElement? Default { get; }
    public JsonElement? InitialValue { get; }
    public JsonElement? SuggestedValue { get; }
    public IReadOnlyList<JsonElement> Choices { get; }
    /// <summary>Enum members with explicit or humanized display metadata, in enum order.</summary>
    public IReadOnlyList<DefinitionChoice> OptionMetadata => Node.OptionMetadata;
    public DefinitionFieldPresentationMetadata PresentationMetadata => Node.PresentationMetadata;
    public JsonElement Constraints => Data.Clone();
    public override string ToString() => $"{nameof(FieldDefinition)}:{Id}:{Type}:{Sensitivity}";
    internal string? Text(string name) => Data.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static string RawTitle(string name) => string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)
        ? InferredTitle(name) : name.Length <= MaximumTitleLength ? name : name[..MaximumTitleLength];

    /// <summary>Humanizes an untitled key; blank keys get a neutral label and control characters stay escaped.</summary>
    private static string InferredTitle(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Setting";
        string title = name.Any(char.IsControl)
            ? JsonSerializer.Serialize(name, CoreJsonContext.Default.String) : DefinitionDisplayText.Humanize(name);
        return title.Length <= MaximumTitleLength ? title : title[..MaximumTitleLength];
    }

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
