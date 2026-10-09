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
        if (slot.IsObjectList) return string.Empty;
        JsonNode? value = Node(slot);
        if (slot.IsList)
            value = value is JsonArray array && listIndex >= 0 && listIndex < array.Count ? array[listIndex] : null;
        return value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
            ? text : value?.ToJsonString() ?? string.Empty;
    }

    internal static int ItemCount(Slot slot) => Node(slot) is JsonArray array ? array.Count : 0;
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
        if (slot.IsObjectList) return text.Length == 0;
        if (text.Length > 65_536 || !TryScalar(slot, text, out JsonNode? value)) return false;
        if (slot.IsList)
        {
            JsonArray array = Node(slot) as JsonArray ?? [];
            int maximum = slot.Metadata.TryGetProperty("maxItems", out JsonElement bound) ? bound.GetInt32() : 4096;
            if (listIndex < 0 || listIndex > array.Count || array.Count >= maximum && listIndex == array.Count) return false;
            if (listIndex == array.Count && text.Length == 0) return true;
            if (listIndex == array.Count) array.Add(value);
            else array[listIndex] = value;
            return slot.Form.SetValue(slot.FullPath, JsonSerializer.SerializeToElement(array));
        }
        return slot.Form.SetValue(slot.FullPath, value is null ? null : JsonSerializer.SerializeToElement(value));
    }

    internal static void Remove(Slot slot, int listIndex)
    {
        if (slot.IsList) slot.Form.RemoveListItem(slot.FullPath, listIndex);
        else slot.Form.SetValue(slot.FullPath, null);
    }

    private static bool TryScalar(Slot slot, string text, out JsonNode? value)
    {
        value = null;
        if (!slot.IsList && text.Length == 0) return true;
        switch (slot.ScalarType)
        {
            case "string": value = JsonValue.Create(text); return true;
            case "boolean":
                if (!bool.TryParse(text, out bool boolean)) return false;
                value = JsonValue.Create(boolean); return true;
            case "integer":
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                value = JsonValue.Create(integer); return true;
            case "number":
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) return false;
                value = JsonValue.Create(number); return true;
            case "enum":
                foreach (JsonElement choice in slot.Metadata.GetProperty("choices").EnumerateArray())
                    if ((choice.ValueKind == JsonValueKind.String ? choice.GetString() : choice.GetRawText()) == text)
                    {
                        value = JsonNode.Parse(choice.GetRawText());
                        return true;
                    }
                return false;
            default: return false;
        }
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
