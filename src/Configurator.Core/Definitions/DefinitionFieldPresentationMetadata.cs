namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// Admitted non-value field metadata. None of these members supplies, defaults or rewrites an answer:
/// <see cref="Placeholder"/> and <see cref="DefaultText"/> are display text only and never materialize into
/// validation results or prepared artifacts.
/// </summary>
public sealed class DefinitionFieldPresentationMetadata
{
    internal static readonly string[] WidgetTokens =
        ["auto", "singleline", "multiline", "markdown", "code", "select", "radio", "switch", "slider"];

    internal DefinitionFieldPresentationMetadata(JsonElement keywords)
    {
        JsonElement extension = keywords.TryGetProperty("x-configurator", out JsonElement metadata)
            && metadata.ValueKind == JsonValueKind.Object ? metadata : default;
        Placeholder = Text(extension, "placeholder");
        DefaultText = Text(extension, "defaultText");
        DeprecationGuidance = Text(extension, "deprecationMessage");
        Units = Text(extension, "units");
        Widget = ParseWidget(Text(extension, "widget"));
        Rows = extension.ValueKind == JsonValueKind.Object && extension.TryGetProperty("rows", out JsonElement rows)
            && rows.TryGetInt32(out int count) ? count : null;
        Syntax = Text(extension, "syntax");
        ItemTitlePointer = Text(extension, "itemTitlePointer");
        DisplayRawKey = extension.ValueKind == JsonValueKind.Object && extension.TryGetProperty("displayRawKey", out JsonElement raw)
            && raw.ValueKind == JsonValueKind.True;
        if (extension.ValueKind == JsonValueKind.Object && extension.TryGetProperty("booleanLabels", out JsonElement labels)
            && labels.ValueKind == JsonValueKind.Object)
        {
            TrueLabel = Text(labels, "true");
            FalseLabel = Text(labels, "false");
        }
    }

    /// <summary>Example input shape shown in an empty editor. Never a value; admitted only on public subtrees.</summary>
    public string? Placeholder { get; }
    /// <summary>Explains what the target does when the field is omitted; it is not a default value.</summary>
    public string? DefaultText { get; }
    /// <summary>Authored <c>deprecationMessage</c> for a deprecated field.</summary>
    public string? DeprecationGuidance { get; }
    public string? Units { get; }
    public DefinitionFieldWidget Widget { get; }
    /// <summary>Preferred visible line count for multi-line, Markdown and code editors.</summary>
    public int? Rows { get; }
    /// <summary>Syntax highlighting language for the code widget, for example <c>json</c> or <c>yaml</c>.</summary>
    public string? Syntax { get; }
    public string? TrueLabel { get; }
    public string? FalseLabel { get; }
    /// <summary>Item-relative JSON Pointer of a public scalar whose value titles each list row.</summary>
    public string? ItemTitlePointer { get; }
    /// <summary>Requests the exact wire key as display text instead of humanization.</summary>
    public bool DisplayRawKey { get; }

    public override string ToString() => $"{nameof(DefinitionFieldPresentationMetadata)}:{Widget}";

    internal static DefinitionFieldWidget ParseWidget(string? token) => token switch
    {
        null or "auto" => DefinitionFieldWidget.Auto,
        "singleline" => DefinitionFieldWidget.SingleLine,
        "multiline" => DefinitionFieldWidget.MultiLine,
        "markdown" => DefinitionFieldWidget.Markdown,
        "code" => DefinitionFieldWidget.Code,
        "select" => DefinitionFieldWidget.Select,
        "radio" => DefinitionFieldWidget.Radio,
        "switch" => DefinitionFieldWidget.Switch,
        "slider" => DefinitionFieldWidget.Slider,
        _ => throw new DefinitionException("widget-invalid")
    };

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
