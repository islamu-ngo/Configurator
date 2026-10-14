namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Presentation;

/// <summary>Expands object rows into typed leaves with independent navigation for each nested list.</summary>
internal sealed class SetupTerminalFieldEditor
{
    private static readonly string[] HelpKeys = ["help", "consequence"];
    internal sealed record Slot(DefinitionFormModel Form, DefinitionFieldInput Input, string[] Path, JsonElement Metadata)
    {
        internal string Title => Metadata.GetProperty("title").GetString()!;
        internal string Type => Metadata.GetProperty("type").GetString()!;
        internal bool IsList => Type == "list";
        internal bool IsObject => Type == "object";
        internal bool IsContainer => IsList || IsObject;
        internal bool IsObjectList => IsList && ScalarType == "object";
        internal string ScalarType => IsList ? Metadata.GetProperty("itemType").GetString()! : Type;
        internal bool RequiresMasking => Input.RequiresMasking;
        internal string[] FullPath => [Input.Definition.Id, .. Path];
        internal string Help => string.Join(" ", HelpKeys
            .Where(key => Metadata.TryGetProperty(key, out _)).Select(key => Metadata.GetProperty(key).GetString()));
    }

    private readonly DefinitionFormModel _form;
    internal SetupTerminalFieldEditor(DefinitionFormModel form) => _form = form;
    internal IReadOnlyList<Slot> Slots => _form.VisibleFields.SelectMany(input =>
        Expand(input, input.Definition.Constraints, [])).ToArray();

    private IEnumerable<Slot> Expand(DefinitionFieldInput input, JsonElement metadata, string[] path)
    {
        string type = metadata.GetProperty("type").GetString()!;
        if (type == "object")
        {
            yield return new Slot(_form, input, path, metadata);
            foreach (JsonElement property in metadata.GetProperty("properties").EnumerateArray())
                foreach (Slot slot in Expand(input, property, [.. path, property.GetProperty("id").GetString()!]))
                    yield return slot;
            yield break;
        }
        var current = new Slot(_form, input, path, metadata);
        yield return current;
        if (!current.IsObjectList) yield break;
        int count = ItemCount(current);
        for (int index = 0; index < count; index++)
            foreach (JsonElement property in metadata.GetProperty("properties").EnumerateArray())
                foreach (Slot slot in Expand(input, property,
                    [.. path, index.ToString(CultureInfo.InvariantCulture), property.GetProperty("id").GetString()!]))
                    yield return slot;
    }

    internal static string Read(Slot slot, int listIndex = 0)
    {
        if (slot.IsObject || slot.IsObjectList) return string.Empty;
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
    internal static bool Clear(Slot slot) => slot.Form.SetValue(slot.FullPath, null);
    internal static bool HasItem(Slot slot, int index) => Node(slot) is JsonArray items && index >= 0 && index < items.Count;
    internal static bool AddObjectItem(Slot slot) => slot.IsObjectList && slot.Form.AddListItem(slot.FullPath);

    internal static IReadOnlyList<string> Choices(Slot slot)
    {
        if (slot.ScalarType == "boolean") return ["false", "true"];
        return slot.Metadata.TryGetProperty("choices", out var choices)
            ? choices.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()! : item.GetRawText()).ToArray() : [];
    }

    internal static bool Write(Slot slot, string text, int listIndex = 0)
    {
        if (slot.IsObject || slot.IsObjectList) return text.Length == 0;
        if (text.Length > 65_536) return false;
        if (slot.IsList)
        {
            JsonArray array = Node(slot) as JsonArray ?? [];
            if (listIndex < 0 || listIndex > array.Count) return false;
            if (listIndex == array.Count && text.Length == 0) return true;
            if (array.Count >= MaximumItems(slot) && listIndex == array.Count) return false;
            JsonNode? value;
            if (text.Length == 0 && slot.ScalarType == "string") value = JsonValue.Create(string.Empty);
            else
            {
                if (text.Length == 0 || !DefinitionScalarEditing.TryParse(slot.ScalarType, text, slot.Metadata, out JsonElement? scalar)) return false;
                value = scalar is { } item ? JsonNode.Parse(item.GetRawText()) : null;
            }
            if (listIndex == array.Count) array.Add(value);
            else array[listIndex] = value;
            return slot.Form.SetValue(slot.FullPath, JsonSerializer.SerializeToElement(array));
        }
        return DefinitionScalarEditing.TryParse(slot.ScalarType, text, slot.Metadata, out JsonElement? parsed)
            && slot.Form.SetValue(slot.FullPath, parsed);
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
