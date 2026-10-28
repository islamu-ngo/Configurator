namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>Expands object rows into typed leaves with independent navigation for each nested list.</summary>
internal sealed class SetupTerminalFieldEditor
{
    internal sealed record Slot(DefinitionFormModel Form, DefinitionFieldInput Input, string[] Path, DefinitionNode Schema,
        string? FallbackTitle = null)
    {
        internal JsonElement Metadata => Schema.Keywords;
        internal DefinitionEditorPresentation? Presentation => Form.GetEditorPresentation(FullPath);
        internal string Title => FallbackTitle is not null && !Metadata.TryGetProperty("title", out _)
            ? FallbackTitle : Presentation?.Title ?? Input.Definition.Title;
        internal string Type => Schema.JsonType;
        internal bool IsList => Schema.Type == DefinitionNodeType.Sequence;
        internal bool IsObject => Type == "object";
        internal bool IsContainer => IsList || IsObject;
        internal bool IsObjectList => IsList && Schema.Items!.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence;
        internal bool IsMap => ObjectSchema.AdditionalProperties is not null;
        internal DefinitionNode ObjectSchema => Value(this) is { } value
            ? DefinitionValidator.SelectVariant(Schema, value) ?? Schema : Schema;
        internal string ScalarType => IsList ? Schema.Items!.JsonType : Type;
        internal bool RequiresMasking => Input.RequiresMasking ||
            Form.GetEditorState(FullPath)?.Sensitivity is { } sensitivity && sensitivity != SetupFieldSensitivity.Public;
        internal string[] FullPath => [Input.Definition.Id, .. Path];
        internal string Help => SetupTerminalText.FormatHelp(Presentation, Metadata);
    }

    private readonly DefinitionFormModel _form;
    internal SetupTerminalFieldEditor(DefinitionFormModel form) => _form = form;
    internal IReadOnlyList<Slot> Slots => _form.Projection.Sections.SelectMany(section => section.Fields)
        .Distinct(StringComparer.Ordinal).Where(_form.Projection.IsFieldVisible).SelectMany(id =>
        {
            var (input, schema) = _form.GetFieldEditor(id);
            return Expand(input, schema, []);
        }).ToArray();

    private IEnumerable<Slot> Expand(DefinitionFieldInput input, DefinitionNode schema, string[] path,
        string? fallbackTitle = null)
    {
        var current = new Slot(_form, input, path, schema, fallbackTitle);
        if (_form.GetEditorState(current.FullPath) is { IsActive: false }) yield break;
        yield return current;
        if (Value(current) is { ValueKind: JsonValueKind.Null }) yield break;
        if (schema.Type == DefinitionNodeType.Mapping)
        {
            DefinitionNode active = current.ObjectSchema;
            foreach ((string name, DefinitionNode property) in active.Properties)
                foreach (Slot slot in Expand(input, property, [.. path, name]))
                    yield return slot;
            if (active.AdditionalProperties is { } entry && Value(current) is { ValueKind: JsonValueKind.Object } mapping)
            {
                int index = 0;
                foreach (JsonProperty property in mapping.EnumerateObject().Where(property =>
                    !active.Properties.ContainsKey(property.Name)))
                    foreach (Slot slot in Expand(input, entry, [.. path, property.Name],
                        SetupTerminalText.Get("MapEntry") + " " + (++index).ToString(CultureInfo.InvariantCulture)))
                        yield return slot;
            }
            yield break;
        }
        if (!current.IsList || schema.Items!.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) yield break;
        int count = ItemCount(current);
        for (int index = 0; index < count; index++)
            foreach (Slot slot in Expand(input, schema.Items!,
                [.. path, index.ToString(CultureInfo.InvariantCulture)]))
                yield return slot;
    }

    internal static string Read(Slot slot, int listIndex = 0)
    {
        if (slot.IsObject || slot.IsObjectList) return string.Empty;
        string[] path = slot.IsList
            ? [.. slot.FullPath, listIndex.ToString(CultureInfo.InvariantCulture)] : slot.FullPath;
        if (slot.Form.ReadDraftText(path) is { } draft) return draft;
        JsonNode? value = Node(slot);
        if (slot.IsList)
            value = value is JsonArray array && listIndex >= 0 && listIndex < array.Count ? array[listIndex] : null;
        return value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
            ? text : value?.ToJsonString() ?? string.Empty;
    }

    internal static int ItemCount(Slot slot) => Node(slot) is JsonArray array ? array.Count : 0;
    internal static int MaximumItems(Slot slot) => slot.Metadata.TryGetProperty("maxItems", out JsonElement bound)
        ? bound.GetInt32() : 4096;
    internal static int LastItemPosition(Slot slot)
    {
        int count = ItemCount(slot);
        return slot.IsObjectList || count >= MaximumItems(slot) ? Math.Max(0, count - 1) : count;
    }
    internal static bool Supply(Slot slot) => slot.IsObject ? slot.Form.SupplyObject(slot.FullPath)
        : slot.IsList && slot.Form.SupplyList(slot.FullPath);
    internal static bool Clear(Slot slot) => slot.Form.EditValue(slot.FullPath, null);
    internal static bool HasItem(Slot slot, int index) => Node(slot) is JsonArray items && index >= 0 && index < items.Count;
    internal static bool AddItem(Slot slot) => slot.IsList && slot.Form.AddListItem(slot.FullPath);

    internal static IReadOnlyList<string> Choices(Slot slot)
    {
        string[] path = EditPath(slot, 0);
        IReadOnlyList<DefinitionChoicePresentation>? choices = slot.Form.GetEditorPresentation(path)?.Choices;
        if (choices is { Count: > 0 }) return choices.Select(choice => choice.Label).ToArray();
        JsonElement metadata = slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata;
        if (metadata.TryGetProperty("enum", out JsonElement declared))
            return declared.EnumerateArray().Select(choice => choice.ValueKind == JsonValueKind.String
                ? choice.GetString()! : choice.GetRawText()).ToArray();
        return slot.ScalarType == "boolean" ? ["false", "true"] : [];
    }

    internal static string[] EditPath(Slot slot, int index) => slot.IsList
        ? [.. slot.FullPath, index.ToString(CultureInfo.InvariantCulture)] : slot.FullPath;

    internal static int ChoiceIndex(Slot slot, int index = 0)
    {
        JsonElement? value = Value(slot);
        if (slot.IsList)
            value = value is { ValueKind: JsonValueKind.Array } rows && index < rows.GetArrayLength() ? rows[index] : null;
        if (value is null) return -1;
        JsonElement metadata = slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata;
        if (metadata.TryGetProperty("enum", out JsonElement choices))
        {
            for (int choice = 0; choice < choices.GetArrayLength(); choice++)
                if (JsonElement.DeepEquals(choices[choice], value.Value)) return choice;
            return -1;
        }
        return value.Value.ValueKind == JsonValueKind.True ? 1 : value.Value.ValueKind == JsonValueKind.False ? 0 : -1;
    }

    internal static bool SelectChoice(Slot slot, int choice, int index = 0)
    {
        if (slot.IsList && !HasItem(slot, index) && !AddItem(slot)) return false;
        string[] path = EditPath(slot, index);
        JsonElement metadata = slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata;
        return metadata.TryGetProperty("enum", out _)
            ? slot.Form.SelectChoice(path, choice)
            : slot.Form.SetValue(path, JsonSerializer.SerializeToElement(choice == 1));
    }

    internal static bool AddMapEntry(Slot slot, string key)
    {
        if (!slot.IsMap || slot.ObjectSchema.Properties.ContainsKey(key) ||
            Value(slot) is { ValueKind: JsonValueKind.Object } mapping && mapping.TryGetProperty(key, out _)) return false;
        int count = Value(slot) is { ValueKind: JsonValueKind.Object } current ? current.EnumerateObject().Count() : 0;
        int maximumEntries = slot.ObjectSchema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum)
            ? maximum.GetInt32() : Configurator.Core.Composition.SetupCompositionLimits.DefaultMappingEntries;
        if (count >= maximumEntries) return false;
        string[] path = [.. slot.FullPath, key];
        try { _ = DefinitionPath.FromSegments(path); }
        catch (SetupDefinitionException) { return false; }
        DefinitionNode schema = slot.ObjectSchema.AdditionalProperties!;
        JsonElement initial = schema.Type switch
        {
            DefinitionNodeType.Text => JsonSerializer.SerializeToElement(""),
            DefinitionNodeType.Boolean => JsonSerializer.SerializeToElement(false),
            DefinitionNodeType.WholeNumber or DefinitionNodeType.Number => JsonSerializer.SerializeToElement(0),
            DefinitionNodeType.Sequence => JsonSerializer.SerializeToElement(Array.Empty<object>()),
            _ => JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        };
        return slot.Form.SetValue(path, initial);
    }

    internal static bool Write(Slot slot, string text, int listIndex = 0)
    {
        if (slot.IsObject || slot.IsObjectList) return text.Length == 0;
        if (slot.IsList)
        {
            JsonArray array = Node(slot) as JsonArray ?? [];
            if (listIndex < 0 || listIndex > array.Count) return false;
            if (listIndex == array.Count && text.Length == 0) return true;
            if (array.Count >= MaximumItems(slot) && listIndex == array.Count) return false;
            string[] itemPath = [.. slot.FullPath, listIndex.ToString(CultureInfo.InvariantCulture)];
            if (listIndex < array.Count) return slot.Form.EditValue(itemPath, text);
            if (!DefinitionScalarEditing.TryParse(slot.ScalarType, text, slot.Schema.Items!.Keywords, out JsonElement? scalar))
            {
                return AddItem(slot) && slot.Form.EditValue(itemPath, text);
            }
            if (scalar is null) return true;
            return AddItem(slot) && slot.Form.EditValue(itemPath, text);
        }
        return slot.Form.EditValue(slot.FullPath, text);
    }

    internal static void Remove(Slot slot, int listIndex)
    {
        if (slot.IsList) slot.Form.RemoveListItem(slot.FullPath, listIndex);
        else slot.Form.SetValue(slot.FullPath, null);
    }

    internal static JsonElement? Value(Slot slot)
    {
        if (!slot.Input.TryValue(out JsonElement? value) || value is null) return null;
        JsonElement node = value.Value;
        foreach (string key in slot.Path)
        {
            if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out JsonElement child)) node = child;
            else if (node.ValueKind == JsonValueKind.Array && int.TryParse(key, out int index)
                && index >= 0 && index < node.GetArrayLength()) node = node[index];
            else return null;
        }
        return node;
    }

    private static JsonNode? Node(Slot slot) => Value(slot) is { } value ? JsonNode.Parse(value.GetRawText()) : null;
}
