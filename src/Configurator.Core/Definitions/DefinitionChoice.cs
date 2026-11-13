namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// One admitted enum member with its display metadata. The <see cref="Value"/> is the exact wire value;
/// <see cref="DisplayLabel"/> is presentation text and never selects or rewrites answers.
/// </summary>
public sealed class DefinitionChoice
{
    internal DefinitionChoice(JsonElement value, JsonElement? metadata, int? metadataIndex)
    {
        Value = value.Clone();
        WireToken = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        MetadataIndex = metadataIndex;
        string? label = Text(metadata, "label");
        HasExplicitLabel = label is not null;
        DisplayLabel = label ?? (value.ValueKind == JsonValueKind.String ? DefinitionDisplayText.Humanize(WireToken) : WireToken);
        Explanation = Text(metadata, "explanation");
        Deprecated = metadata is { } entry && entry.TryGetProperty("deprecated", out JsonElement deprecated)
            && deprecated.ValueKind == JsonValueKind.True;
        DeprecationGuidance = Text(metadata, "deprecationMessage");
    }

    /// <summary>The exact admitted enum value.</summary>
    public JsonElement Value { get; }

    /// <summary>The exact wire spelling: the string value itself, or raw JSON for numbers, booleans and null.</summary>
    public string WireToken { get; }

    /// <summary>The authored label, or deterministic humanized text when the label was omitted.</summary>
    public string DisplayLabel { get; }

    public bool HasExplicitLabel { get; }
    public string? Explanation { get; }
    public bool Deprecated { get; }
    /// <summary>Authored <c>deprecationMessage</c> guidance explaining what to choose instead.</summary>
    public string? DeprecationGuidance { get; }

    /// <summary>
    /// Index of the declaring <c>x-configurator.choices</c> entry, used to address localization overrides;
    /// <see langword="null"/> when the member has no authored metadata.
    /// </summary>
    public int? MetadataIndex { get; }

    public override string ToString() => $"{nameof(DefinitionChoice)}:{WireToken}";

    internal static IReadOnlyList<DefinitionChoice> From(JsonElement keywords)
    {
        if (!keywords.TryGetProperty("enum", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<DefinitionChoice>();
        JsonElement declared = keywords.TryGetProperty("x-configurator", out JsonElement extension)
            && extension.ValueKind == JsonValueKind.Object && extension.TryGetProperty("choices", out JsonElement entries)
            && entries.ValueKind == JsonValueKind.Array ? entries : default;
        var choices = new List<DefinitionChoice>(values.GetArrayLength());
        foreach (JsonElement value in values.EnumerateArray())
        {
            int? index = null;
            JsonElement? metadata = null;
            if (declared.ValueKind == JsonValueKind.Array)
            {
                int position = 0;
                foreach (JsonElement entry in declared.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("value", out JsonElement token)
                        && JsonElement.DeepEquals(token, value))
                    {
                        index = position;
                        metadata = entry;
                        break;
                    }
                    position++;
                }
            }
            choices.Add(new DefinitionChoice(value, metadata, index));
        }
        return choices.AsReadOnly();
    }

    private static string? Text(JsonElement? metadata, string name) =>
        metadata is { } entry && entry.TryGetProperty(name, out JsonElement text) && text.ValueKind == JsonValueKind.String
            ? text.GetString() : null;
}
