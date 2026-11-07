namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>Expands object rows into typed leaves with independent navigation for each nested list.</summary>
internal sealed class TerminalFieldEditor
{
    internal sealed record Slot(DefinitionFormModel Form, DefinitionFieldInput? Field, string[] Path, DefinitionNode Schema,
        string? FallbackTitle = null)
    {
        internal DefinitionFieldInput Input => Field ?? throw new InvalidOperationException("root-editor-has-no-field");
        internal JsonElement Metadata => Schema.Keywords;
        internal DefinitionPath Location => DefinitionPath.FromSegments(Field is null ? Path : [Field.Definition.Name, .. Path]);
        internal string Identity { get; } = Form.GetEditorIdentity(
            DefinitionPath.FromSegments(Field is null ? Path : [Field.Definition.Name, .. Path]));
        internal DefinitionEditorPresentation? Presentation => Form.GetEditorPresentation(Location);
        internal string Title => FallbackTitle is not null && !Metadata.TryGetProperty("title", out _)
            ? FallbackTitle : Presentation?.Title ?? Field?.Definition.Title ?? TerminalStrings.Get("ConfigurationRoot", Form.Locale);
        internal string Type => Schema.JsonType;
        internal bool IsList => Schema.Type == DefinitionNodeType.Sequence;
        internal bool IsObject => Type == "object";
        internal bool IsContainer => IsList || IsObject;
        internal bool IsObjectList => IsList && Schema.Items!.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence;
        internal bool IsMap => ObjectSchema.AdditionalProperties is not null;
        internal DefinitionNode ObjectSchema => Value(this) is { } value
            ? DefinitionValidator.SelectVariant(Schema, value) ?? Schema : Schema;
        internal string ScalarType => IsList ? Schema.Items!.JsonType : Type;
        internal bool RequiresMasking => Form.GetFieldValidation(Location).RequiresMasking;
        internal string[] FullPath => Field is null ? Path : [Field.Definition.Id, .. Path];
        internal string Help => TerminalStrings.FormatHelp(Presentation, Metadata, Form.Locale);
    }

    private readonly DefinitionFormModel _form;
    private readonly TerminalRootEditor _root;
    internal TerminalFieldEditor(DefinitionFormModel form)
    {
        _form = form;
        _root = new(form);
    }
    internal IReadOnlyList<Slot> Slots => !DefinitionEditingSupport.HasPropertyRoot(_form.Definition) ? _root.Slots
        : _form.Projection.Sections.SelectMany(section => section.Fields)
        .Distinct(StringComparer.Ordinal).Where(_form.Projection.IsFieldVisible).SelectMany(id =>
        {
            var (input, schema) = _form.GetFieldEditor(id);
            return _root.Expand(new(_form, input, [], schema));
        }).ToArray();

    internal static string Read(Slot slot, int listIndex = 0)
    {
        if (slot.IsObject || slot.IsObjectList) return string.Empty;
        DefinitionPath path = EditLocation(slot, listIndex);
        if (slot.Form.ReadDraftText(path) is { } draft) return draft;
        JsonNode? value = Node(slot);
        if (slot.IsList)
            value = value is JsonArray array && listIndex >= 0 && listIndex < array.Count ? array[listIndex] : null;
        return value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
            ? text : value?.ToJsonString() ?? string.Empty;
    }

    internal static int ItemCount(Slot slot) => Node(slot) is JsonArray array ? array.Count : 0;
    internal static int MaximumItems(Slot slot) => (int)(slot.Form.GetFieldValidation(slot.Location).Bounds
        .FirstOrDefault(bound => bound.Kind == DefinitionFieldBoundKind.MaxItems)?.Value ?? 4096);
    internal static int LastItemPosition(Slot slot)
    {
        int count = ItemCount(slot);
        return slot.IsObjectList || count >= MaximumItems(slot) ? Math.Max(0, count - 1) : count;
    }
    internal static bool Supply(Slot slot) => slot.Form.GetFieldValidation(slot.Location).CanEdit &&
        (slot.IsObject ? slot.Form.SupplyObject(slot.Location) : slot.IsList && slot.Form.SupplyList(slot.Location));
    internal static bool Clear(Slot slot) => slot.Form.GetFieldValidation(slot.Location).CanOmit &&
        slot.Form.EditValue(slot.Location, null);
    internal static bool HasItem(Slot slot, int index) => Node(slot) is JsonArray items && index >= 0 && index < items.Count;
    internal static bool AddItem(Slot slot) => slot.IsList && slot.Form.GetFieldValidation(slot.Location).CanEdit &&
        slot.Form.AddListItem(slot.Location);

    internal static IReadOnlyList<string> Choices(Slot slot)
    {
        DefinitionPath path = EditLocation(slot, 0);
        bool append = slot.IsList && !HasItem(slot, 0);
        DefinitionFieldValidation facts = slot.Form.GetFieldValidation(append ? slot.Location : path);
        JsonElement metadata = slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata;
        if (metadata.TryGetProperty("enum", out JsonElement declared) && facts.HasProtectedDescendants) return [];
        IReadOnlyList<DefinitionChoicePresentation>? choices = slot.Form.GetEditorPresentation(path)?.Choices;
        if (choices is { Count: > 0 }) return choices.Select(choice => choice.Label).ToArray();
        if (facts.AllowedValues.Count > 0)
            return facts.AllowedValues.Select(choice => choice.ValueKind == JsonValueKind.String
                ? choice.GetString()! : choice.GetRawText()).ToArray();
        if (append && declared.ValueKind == JsonValueKind.Array)
            return declared.EnumerateArray().Select(choice => choice.ValueKind == JsonValueKind.String
                ? choice.GetString()! : choice.GetRawText()).ToArray();
        return slot.ScalarType == "boolean" ? ["false", "true"] : [];
    }

    internal static DefinitionPath EditLocation(Slot slot, int index) => slot.IsList
        ? DefinitionPath.FromSegments([.. slot.Location.Segments, index.ToString(CultureInfo.InvariantCulture)]) : slot.Location;

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
        if (!slot.Form.GetFieldValidation(slot.Location).CanEdit) return false;
        if (slot.IsList && !HasItem(slot, index) && !AddItem(slot)) return false;
        DefinitionPath path = EditLocation(slot, index);
        JsonElement metadata = slot.IsList ? slot.Schema.Items!.Keywords : slot.Metadata;
        return metadata.TryGetProperty("enum", out _)
            ? slot.Form.SelectChoice(path, choice)
            : slot.Form.SetValue(path, JsonSerializer.SerializeToElement(choice == 1));
    }

    internal static bool AddMapEntry(Slot slot, string key) =>
        slot.IsMap && slot.Form.GetFieldValidation(slot.Location).CanEdit && slot.Form.AddMapEntry(slot.Location, key);

    internal static bool Write(Slot slot, string text, int listIndex = 0)
    {
        if (!slot.Form.GetFieldValidation(slot.Location).CanEdit) return false;
        if (slot.IsObject || slot.IsObjectList) return text.Length == 0;
        if (slot.IsList)
        {
            JsonArray array = Node(slot) as JsonArray ?? [];
            if (listIndex < 0 || listIndex > array.Count) return false;
            if (listIndex == array.Count && text.Length == 0) return true;
            if (array.Count >= MaximumItems(slot) && listIndex == array.Count) return false;
            DefinitionPath itemPath = EditLocation(slot, listIndex);
            if (listIndex < array.Count) return slot.Form.EditValue(itemPath, text);
            if (!DefinitionScalarEditing.TryParse(slot.ScalarType, text, slot.Schema.Items!.Keywords, out JsonElement? scalar))
            {
                return AddItem(slot) && slot.Form.EditValue(itemPath, text);
            }
            if (scalar is null) return true;
            return AddItem(slot) && slot.Form.EditValue(itemPath, text);
        }
        return slot.Form.EditValue(slot.Location, text);
    }

    internal static void Remove(Slot slot, int listIndex)
    {
        if (!slot.Form.GetFieldValidation(slot.Location).CanEdit) return;
        if (slot.IsList) slot.Form.RemoveListItem(slot.Location, listIndex);
        else slot.Form.SetValue(slot.Location, null);
    }

    internal static JsonElement? Value(Slot slot)
    {
        return slot.Form.Validate().TryGetEffectiveValue(slot.Location, out JsonElement value) ? value : null;
    }

    private static JsonNode? Node(Slot slot) => Value(slot) is { } value ? JsonNode.Parse(value.GetRawText()) : null;
}
