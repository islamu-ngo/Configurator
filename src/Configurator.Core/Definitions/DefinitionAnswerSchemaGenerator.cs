namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Generates a JSON Schema (Draft 2020-12) for the private answer file of one admitted definition, so external
/// editors can complete and check answers. The input is public definition metadata only: no answers, workspace
/// state or secret material is read, and secret leaves are emitted as <c>writeOnly</c> without value exemplars.
/// </summary>
/// <remarks>
/// The schema is an editing aid. Applicability, relationships and secret policy remain enforced by
/// <see cref="DefinitionValidator"/>; an answer file that satisfies this schema can still be incomplete.
/// </remarks>
public static class DefinitionAnswerSchemaGenerator
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";
    private static readonly string[] Bounds =
    [
        "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "minLength", "maxLength",
        "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties", "format"
    ];

    /// <summary>Returns the indented answer schema document, terminated by a newline.</summary>
    public static string Generate(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var root = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = string.Create(CultureInfo.InvariantCulture,
                $"urn:islamu:configurator:answers:{definition.FamilyId:D}:{definition.Revision}"),
            ["title"] = string.Create(CultureInfo.InvariantCulture, $"{definition.Title} answers (revision {definition.Revision})"),
            ["description"] = "Private answers for one definition revision. Keep this file local; it may contain secrets.",
            ["x-configurator"] = new JsonObject
            {
                ["familyId"] = definition.FamilyId.ToString("D"),
                ["revision"] = definition.Revision,
                ["definitionDigest"] = definition.Digest.Value
            },
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("answerContractVersion", "values"),
            ["properties"] = new JsonObject
            {
                ["answerContractVersion"] = new JsonObject { ["const"] = AnswerSnapshotCodec.ContractVersion },
                ["values"] = Node(definition.Configuration, null, FieldDefinition.SensitivityOf(definition.Configuration)),
                ["omitted"] = new JsonObject
                {
                    ["description"] = "JSON Pointers deliberately left unset even when an initial value exists.",
                    ["type"] = "array",
                    ["uniqueItems"] = true,
                    ["default"] = new JsonArray(),
                    ["items"] = new JsonObject { ["type"] = "string", ["pattern"] = "^(?:/(?:[^~]|~[01])*)*$" }
                }
            }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static JsonObject Node(DefinitionNode node, string? name, FieldSensitivity inherited)
    {
        var sensitivity = (FieldSensitivity)Math.Max((int)inherited, (int)FieldDefinition.SensitivityOf(node));
        bool secret = sensitivity == FieldSensitivity.Secret;
        bool exemplarsAllowed = FieldDefinition.EffectiveSensitivity(node, inherited) == FieldSensitivity.Public;
        JsonElement keywords = node.Keywords;
        var schema = new JsonObject();
        if (name is not null)
            schema["title"] = Text(keywords, "title") ?? (node.PresentationMetadata.DisplayRawKey ? name : DefinitionDisplayText.Humanize(name));
        if (Text(keywords, "description") is { } description) schema["description"] = description;
        if (node.Variants.Count == 0)
            schema["type"] = node.IsNullable ? new JsonArray(node.JsonType, "null") : node.JsonType;
        foreach (string bound in Bounds)
            if (keywords.TryGetProperty(bound, out JsonElement value)) schema[bound] = JsonNode.Parse(value.GetRawText());
        if (keywords.TryGetProperty("deprecated", out JsonElement deprecated) && deprecated.ValueKind == JsonValueKind.True)
            schema["deprecated"] = true;
        if (keywords.TryGetProperty("readOnly", out JsonElement readOnly) && readOnly.ValueKind == JsonValueKind.True)
            schema["readOnly"] = true;
        if (secret) schema["writeOnly"] = true;
        else
        {
            if (keywords.TryGetProperty("enum", out JsonElement values)) schema["enum"] = JsonNode.Parse(values.GetRawText());
            if (exemplarsAllowed)
            {
                if (keywords.TryGetProperty("const", out JsonElement constant)) schema["const"] = JsonNode.Parse(constant.GetRawText());
                if (keywords.TryGetProperty("x-configurator", out JsonElement extension)
                    && extension.TryGetProperty("initialValue", out JsonElement initial))
                    schema["default"] = JsonNode.Parse(initial.GetRawText());
                if (keywords.TryGetProperty("examples", out JsonElement examples)) schema["examples"] = JsonNode.Parse(examples.GetRawText());
            }
        }
        if (Markdown(schema, node, secret) is { } markdown) schema["markdownDescription"] = markdown;
        if (node.Type == DefinitionNodeType.Mapping && node.Variants.Count == 0)
        {
            var properties = new JsonObject();
            foreach (string property in node.OrderedPropertyNames)
                properties[property] = Node(node.Properties[property], property, sensitivity);
            schema["properties"] = properties;
            if (node.Required.Count > 0)
                schema["required"] = new JsonArray(node.Required.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray());
            schema["additionalProperties"] = node.AdditionalProperties is { } map ? Node(map, null, sensitivity) : false;
        }
        if (node.Variants.Count > 0)
            schema["oneOf"] = new JsonArray(node.Variants.Select(variant => (JsonNode?)Node(variant, null, sensitivity)).ToArray());
        if (node.Items is { } items) schema["items"] = Node(items, null, sensitivity);
        return schema;
    }

    /// <summary>Synthesizes hover documentation: description, units, omission behavior, choices and deprecation.</summary>
    private static string? Markdown(JsonObject schema, DefinitionNode node, bool secret)
    {
        DefinitionFieldPresentationMetadata metadata = node.PresentationMetadata;
        var text = new StringBuilder();
        if (schema["title"] is JsonValue title) text.Append("**").Append(title.GetValue<string>()).Append("**\n\n");
        if (schema["description"] is JsonValue description) text.Append(description.GetValue<string>()).Append("\n\n");
        if (secret) text.Append("Secret value: keep this answer file private.\n\n");
        if (metadata.Units is { } units) text.Append("Units: ").Append(units).Append("\n\n");
        if (metadata.DefaultText is { } omitted) text.Append("When omitted: ").Append(omitted).Append("\n\n");
        if (!secret && metadata.Placeholder is { } placeholder) text.Append("Example: `").Append(placeholder).Append("`\n\n");
        if (!secret && node.OptionMetadata.Count > 0)
        {
            text.Append("| Value | Label | Notes |\n| --- | --- | --- |\n");
            foreach (DefinitionChoice choice in node.OptionMetadata)
            {
                string notes = string.Join(" ", new[]
                {
                    choice.Explanation,
                    choice.Deprecated ? "Deprecated." + (choice.DeprecationGuidance is { } guidance ? " " + guidance : "") : null
                }.Where(note => note is not null));
                text.Append("| `").Append(Cell(choice.WireToken)).Append("` | ").Append(Cell(choice.DisplayLabel))
                    .Append(" | ").Append(Cell(notes)).Append(" |\n");
            }
            text.Append('\n');
        }
        if (metadata.DeprecationGuidance is { } deprecation) text.Append("Deprecated: ").Append(deprecation).Append("\n\n");
        string result = text.ToString().TrimEnd('\n');
        return result.Length == 0 ? null : result;
    }

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    private static string? Text(JsonElement keywords, string name) =>
        keywords.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
