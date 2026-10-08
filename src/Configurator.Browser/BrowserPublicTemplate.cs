namespace Configurator.Browser;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed record BrowserTemplateResult(
    bool IsAccepted, SetupAnswerSnapshot? Answers, string? Preview, string? DownloadHref)
{
    public static BrowserTemplateResult Rejected { get; } = new(false, null, null, null);
}

/// <summary>
/// A closed public choice-transfer envelope, never a Core composed artifact or native configuration.
/// </summary>
public static class BrowserPublicTemplate
{
    public const int MaximumBytes = 64 * 1024;
    public const string SchemaVersion = "configurator-public-template/v1";

    public static bool IsSelectable(SetupFieldDefinition field) =>
        field.Sensitivity == SetupFieldSensitivity.Public && field.Type is "enum" or "boolean";

    public static BrowserTemplateResult Parse(SetupDefinition definition, ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumBytes) return BrowserTemplateResult.Rejected;
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            string[] names = ["schemaVersion", "familyId", "revision", "definitionDigest", "selections"];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Any(item => !seen.Add(item.Name) || !names.Contains(item.Name, StringComparer.Ordinal))
                || seen.Count != names.Length
                || root.GetProperty("schemaVersion").GetString() != SchemaVersion
                || root.GetProperty("familyId").GetString() != definition.FamilyId.ToString("D")
                || !root.GetProperty("revision").TryGetInt64(out long revision) || revision != definition.Revision
                || root.GetProperty("definitionDigest").GetString() != definition.Digest.Value
                || root.GetProperty("selections").ValueKind != JsonValueKind.Object)
                return BrowserTemplateResult.Rejected;

            var selections = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.GetProperty("selections").EnumerateObject())
            {
                if (selections.Count >= 1024 || !selections.TryAdd(property.Name, property.Value.Clone()))
                    return BrowserTemplateResult.Rejected;
            }
            return Create(definition, selections);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException or SetupDefinitionException)
        {
            return BrowserTemplateResult.Rejected;
        }
    }

    public static BrowserTemplateResult Create(
        SetupDefinition definition, IReadOnlyDictionary<string, JsonElement> selections)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(selections);
        foreach ((string id, JsonElement value) in selections)
        {
            SetupFieldDefinition? field = definition.Fields.FirstOrDefault(item => item.Id == id);
            if (field is null || !IsSelectable(field)
                || (field.Type == "boolean"
                    ? value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    : value.ValueKind != JsonValueKind.String
                        || !field.Choices.Any(choice => JsonElement.DeepEquals(choice, value))))
                return BrowserTemplateResult.Rejected;
        }
        var snapshot = new SetupAnswerSnapshot(0, selections);
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, snapshot);
        // Missing required answers are valid in a partial template; supplied answers must be active and valid.
        if (validation.Fields.Any(field => selections.ContainsKey(field.Id) && (!field.IsActive || field.HasError)))
            return BrowserTemplateResult.Rejected;

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteString("familyId", definition.FamilyId.ToString("D"));
            writer.WriteNumber("revision", definition.Revision);
            writer.WriteString("definitionDigest", definition.Digest.Value);
            writer.WriteStartObject("selections");
            foreach ((string id, JsonElement value) in selections.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(id);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        if (output.Length > MaximumBytes) return BrowserTemplateResult.Rejected;
        byte[] bytes = output.ToArray();
        return new BrowserTemplateResult(true, snapshot, Encoding.UTF8.GetString(bytes),
            $"data:application/json;base64,{Convert.ToBase64String(bytes)}");
    }
}
