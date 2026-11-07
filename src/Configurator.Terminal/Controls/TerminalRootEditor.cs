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
                int ordinal = 0;
                foreach (JsonProperty property in mapping.EnumerateObject().Where(property =>
                    !active.Properties.ContainsKey(property.Name)))
                    foreach (var slot in Expand(new(form, current.Field, [.. current.Path, property.Name], entry,
                        TerminalStrings.Get("MapEntry", form.Locale) + " " + (++ordinal).ToString(TerminalStrings.Culture(form.Locale)))))
                        yield return slot;
            }
        }
        else if (current.IsObjectList)
        {
            for (int index = 0; index < TerminalFieldEditor.ItemCount(current); index++)
                foreach (var slot in Expand(new(form, current.Field,
                    [.. current.Path, index.ToString(CultureInfo.InvariantCulture)], current.Schema.Items!)))
                    yield return slot;
        }
    }
}
