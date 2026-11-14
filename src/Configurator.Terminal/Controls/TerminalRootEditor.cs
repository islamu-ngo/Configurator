namespace Configurator.Terminal;

using System.Globalization;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>Projects literal root coordinates into the same native slots used by ordinary fields.</summary>
internal sealed class TerminalRootEditor(DefinitionFormModel form)
{
    internal IReadOnlyList<TerminalFieldEditor.Slot> Slots =>
        Expand(new(form, null, [], form.Definition.Configuration,
            TerminalStrings.Get("ConfigurationRoot", form.Locale))).ToArray();

    internal IEnumerable<TerminalFieldEditor.Slot> Expand(TerminalFieldEditor.Slot current)
    {
        if (form.GetEditorState(current.Location) is { IsActive: false }) yield break;
        yield return current;
        if (TerminalFieldEditor.Value(current) is { ValueKind: JsonValueKind.Null }) yield break;
        if (current.Schema.Type == DefinitionNodeType.Mapping)
        {
            DefinitionNode active = current.ObjectSchema;
            foreach ((string name, DefinitionNode property) in active.Properties)
                foreach (var slot in Expand(new(form, current.Field, [.. current.Path, name], property)))
                    yield return slot;
            if (active.AdditionalProperties is { } entry &&
                TerminalFieldEditor.Value(current) is { ValueKind: JsonValueKind.Object } mapping)
            {
                bool protectKeys = form.GetFieldValidation(current.Location).HasProtectedDescendants ||
                    IsProtectedSchema(entry);
                int ordinal = 0;
                foreach (JsonProperty property in mapping.EnumerateObject().Where(property =>
                    !active.Properties.ContainsKey(property.Name)))
                {
                    ordinal++;
                    string trimmed = protectKeys ? string.Empty : property.Name.Trim();
                    string fallback = trimmed.Length > 0
                        ? trimmed
                        : TerminalStrings.Get("MapEntry", form.Locale) + " " + ordinal.ToString(TerminalStrings.Culture(form.Locale));
                    foreach (var slot in Expand(new(form, current.Field, [.. current.Path, property.Name], entry, fallback,
                        PreferFallbackTitle: !protectKeys && trimmed.Length > 0)))
                        yield return slot;
                }
            }
        }
        else if (current.IsObjectList)
        {
            DefinitionFieldValidation listFacts = form.GetFieldValidation(current.Location);
            string? titlePointer = !listFacts.HasProtectedDescendants
                ? form.GetEditorPresentation(current.Location)?.ItemTitlePointer
                : null;
            JsonElement? arrayValue = titlePointer is not null ? TerminalFieldEditor.Value(current) : null;
            int count = TerminalFieldEditor.ItemCount(current);
            for (int index = 0; index < count; index++)
            {
                string? resolvedTitle = titlePointer is not null &&
                    arrayValue is { ValueKind: JsonValueKind.Array } array &&
                    index < array.GetArrayLength()
                    ? ResolveScalarPointer(array[index], titlePointer)
                    : null;
                foreach (var slot in Expand(new(form, current.Field,
                    [.. current.Path, index.ToString(CultureInfo.InvariantCulture)], current.Schema.Items!,
                    resolvedTitle, PreferFallbackTitle: !string.IsNullOrEmpty(resolvedTitle))))
                    yield return slot;
            }
        }
    }

    private static string? ResolveScalarPointer(JsonElement element, string pointer)
    {
        if (string.IsNullOrEmpty(pointer)) return null;
        string trimmed = pointer.StartsWith('/') ? pointer[1..] : pointer;
        JsonElement current = element;
        foreach (string rawSegment in trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString()?.Trim(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.GetRawText(),
            _ => null
        };
    }

    private static bool IsProtectedSchema(DefinitionNode node) =>
        node.Keywords.TryGetProperty("x-configurator", out JsonElement ext) &&
        ext.TryGetProperty("sensitivity", out JsonElement sens) &&
        sens.ValueKind == JsonValueKind.String &&
        !string.Equals(sens.GetString(), "public", StringComparison.Ordinal);
}
