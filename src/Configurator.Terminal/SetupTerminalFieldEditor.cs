namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>Expands object rows into typed leaves with independent navigation for each nested list.</summary>
internal sealed class SetupTerminalFieldEditor
{
    internal sealed record Slot(DefinitionFormModel Form, DefinitionFieldInput Input, string[] Path, DefinitionNode Schema)
    {
        internal JsonElement Metadata => Schema.Keywords;
        internal string Title => Metadata.TryGetProperty("title", out var title) ? title.GetString()!
            : Path.Length == 0 ? Input.Definition.Title : Path[^1];
        internal string Type => Schema.JsonType;
        internal bool IsList => Schema.Type == DefinitionNodeType.Sequence;
        internal bool IsObject => Type == "object";
        internal bool IsContainer => IsList || IsObject;
        internal bool IsObjectList => IsList && ScalarType == "object";
        internal string ScalarType => IsList ? Schema.Items!.JsonType : Type;
        internal bool RequiresMasking => Input.RequiresMasking;
        internal string[] FullPath => [Input.Definition.Id, .. Path];
        internal string Help => Metadata.TryGetProperty("description", out var description) ? description.GetString()! : "";
    }

    private readonly DefinitionFormModel _form;
    internal SetupTerminalFieldEditor(DefinitionFormModel form) => _form = form;
    internal IReadOnlyList<Slot> Slots => _form.VisibleFields.SelectMany(input =>
        Expand(input, input.Definition.Node, [])).ToArray();

    private IEnumerable<Slot> Expand(DefinitionFieldInput input, DefinitionNode schema, string[] path)
    {
        if (schema.Type == DefinitionNodeType.Mapping)
        {
            yield return new Slot(_form, input, path, schema);
            foreach ((string name, DefinitionNode property) in schema.Properties)
                foreach (Slot slot in Expand(input, property, [.. path, name]))
                    yield return slot;
            yield break;
        }
        var current = new Slot(_form, input, path, schema);
        yield return current;
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
        if (slot.ScalarType == "boolean") return ["false", "true"];
        return (slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata).TryGetProperty("enum", out var choices)
            ? choices.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()! : item.GetRawText()).ToArray() : [];
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
            if (!DefinitionScalarEditing.TryParse(slot.ScalarType, text, slot.Metadata, out JsonElement? scalar))
            {
                return slot.Form.EditValue(itemPath, text);
            }
            array.Add(scalar is { } item ? JsonNode.Parse(item.GetRawText()) : null);
            return slot.Form.SetValue(slot.FullPath, JsonSerializer.SerializeToElement(array));
        }
        return slot.Form.EditValue(slot.FullPath, text);
    }

    internal static void Remove(Slot slot, int listIndex)
    {
        if (slot.IsList) slot.Form.RemoveListItem(slot.FullPath, listIndex);
        else slot.Form.SetValue(slot.FullPath, null);
    }

    private static JsonNode? Node(Slot slot)
    {
        if (!slot.Input.TryValue(out JsonElement? value) || value is null) return null;
        JsonNode? node = JsonNode.Parse(value.Value.GetRawText());
        foreach (string key in slot.Path)
            node = node is JsonObject mapping ? mapping[key] : node is JsonArray array && int.TryParse(key, out int index)
                && index >= 0 && index < array.Count ? array[index] : null;
        return node;
    }
}
