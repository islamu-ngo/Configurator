namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Presentation;

/// <summary>Edits scalar leaves and individual list items, never a whole definition as JSON.</summary>
internal sealed class SetupTerminalFieldEditor
{
    private static readonly string[] HelpKeys = ["help", "consequence"];
    internal sealed record Slot(DefinitionFieldInput Input, string[] Path, JsonElement Metadata)
    {
        internal string Title => Metadata.GetProperty("title").GetString()!;
        internal string Type => Metadata.GetProperty("type").GetString()!;
        internal bool IsList => Type == "list";
        internal string ScalarType => IsList ? Metadata.GetProperty("itemType").GetString()! : Type;
        internal bool RequiresMasking => Input.RequiresMasking;
        internal string Help => string.Join(" ", HelpKeys
            .Where(key => Metadata.TryGetProperty(key, out _)).Select(key => Metadata.GetProperty(key).GetString()));
    }

    private readonly DefinitionFormModel _form;
    internal SetupTerminalFieldEditor(DefinitionFormModel form) => _form = form;
    internal IReadOnlyList<Slot> Slots => _form.VisibleFields.SelectMany(input =>
        Expand(input, input.Definition.Constraints, [])).ToArray();

    private static IEnumerable<Slot> Expand(DefinitionFieldInput input, JsonElement metadata, string[] path)
    {
        if (metadata.GetProperty("type").GetString() == "object"
            && metadata.TryGetProperty("properties", out JsonElement properties))
        {
            foreach (JsonElement property in properties.EnumerateArray())
                foreach (Slot slot in Expand(input, property, [.. path, property.GetProperty("id").GetString()!]))
                    yield return slot;
        }
        else yield return new(input, path, metadata);
    }

    internal static string Read(Slot slot, int listIndex = 0)
    {
        JsonNode? value = Node(slot);
        if (slot.IsList)
            value = value is JsonArray array && listIndex >= 0 && listIndex < array.Count ? array[listIndex] : null;
        return value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
            ? text : value?.ToJsonString() ?? string.Empty;
    }

    internal static int ItemCount(Slot slot) => Node(slot) is JsonArray array ? array.Count : 0;

    internal static IReadOnlyList<string> Choices(Slot slot)
    {
        if (slot.ScalarType == "boolean") return ["false", "true"];
        return slot.Metadata.TryGetProperty("choices", out var choices)
            ? choices.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()! : item.GetRawText()).ToArray() : [];
    }

    internal static bool Write(Slot slot, string text, int listIndex = 0)
    {
        if (text.Length > 65_536 || !TryScalar(slot, text, out JsonNode? value))
            return false;
        if (slot.IsList)
        {
            JsonArray array = Node(slot) as JsonArray ?? [];
            if (listIndex < 0 || listIndex > array.Count || array.Count >= 4096 && listIndex == array.Count)
                return false;
            if (listIndex == array.Count && text.Length == 0) return true;
            if (listIndex == array.Count) array.Add(value);
            else array[listIndex] = value;
            Store(slot, array);
        }
        else Store(slot, value);
        return true;
    }

    internal static void Remove(Slot slot, int listIndex)
    {
        if (slot.IsList && Node(slot) is JsonArray array && listIndex >= 0 && listIndex < array.Count)
        {
            array.RemoveAt(listIndex);
            Store(slot, array);
        }
        else if (!slot.IsList) Store(slot, null);
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
            node = node is JsonObject mapping ? mapping[key] : null;
        return node?.DeepClone();
    }

    private static void Store(Slot slot, JsonNode? value)
    {
        if (slot.Path.Length == 0)
        {
            slot.Input.Text = value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
                ? text : value?.ToJsonString() ?? string.Empty;
            return;
        }
        JsonObject root = slot.Input.TryValue(out JsonElement? existing) && existing is not null
            ? JsonNode.Parse(existing.Value.GetRawText()) as JsonObject ?? [] : [];
        JsonObject parent = root;
        foreach (string key in slot.Path.SkipLast(1))
        {
            if (parent[key] is not JsonObject child) parent[key] = child = [];
            parent = child;
        }
        if (value is null) parent.Remove(slot.Path[^1]);
        else parent[slot.Path[^1]] = value;
        slot.Input.Text = root.ToJsonString();
    }
}
