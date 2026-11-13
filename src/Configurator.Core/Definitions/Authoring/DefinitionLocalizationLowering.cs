namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json.Nodes;

/// <summary>
/// Resolves value-keyed choice localization keys such as
/// <c>/configuration/properties/mode/x-configurator/choices[value=LOCAL]/label</c> to the positional runtime
/// pointer of that choice, so translations stay bound to wire values when choices are inserted or reordered.
/// A keyed choice without metadata gains an entry; a keyed label without authored text gains the humanized label.
/// </summary>
internal static class DefinitionLocalizationLowering
{
    private const string Prefix = "choices[value=";

    internal static void Lower(JsonObject runtime)
    {
        if (runtime["localization"]?["translations"] is not JsonObject translations) return;
        foreach ((_, JsonNode? translation) in translations)
        {
            if (translation?["overrides"] is not JsonObject overrides) continue;
            var rewritten = new JsonObject();
            foreach ((string key, JsonNode? value) in overrides.ToArray())
            {
                string pointer = key.Contains(Prefix, StringComparison.Ordinal) ? Resolve(runtime, key) : key;
                if (rewritten.ContainsKey(pointer)) throw new DefinitionException("localization-target");
                rewritten[pointer] = value?.DeepClone();
            }
            translation["overrides"] = rewritten;
        }
    }

    private static string Resolve(JsonObject runtime, string key)
    {
        IReadOnlyList<string> segments = DefinitionPath.Parse(key).Segments;
        var resolved = new List<string>(segments.Count + 1);
        JsonNode? current = runtime;
        JsonObject? schema = null;
        for (int index = 0; index < segments.Count; index++)
        {
            string segment = segments[index];
            if (segment.StartsWith(Prefix, StringComparison.Ordinal) && segment.EndsWith(']'))
            {
                if (current is not JsonObject metadata || schema is null) throw new DefinitionException("localization-target");
                string token = segment[Prefix.Length..^1];
                int position = Choice(schema, metadata, token);
                JsonObject entry = metadata["choices"]!.AsArray()[position]!.AsObject();
                if (index + 1 < segments.Count && segments[index + 1] == "label" && !entry.ContainsKey("label"))
                    entry["label"] = DefinitionDisplayText.Humanize(token);
                resolved.Add("choices");
                resolved.Add(position.ToString(System.Globalization.CultureInfo.InvariantCulture));
                current = entry;
                continue;
            }
            if (segment == "x-configurator" && current is JsonObject owner) schema = owner;
            current = current switch
            {
                JsonObject node => node[segment],
                JsonArray array when int.TryParse(segment, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int item) && item < array.Count => array[item],
                _ => null
            };
            resolved.Add(segment);
        }
        return DefinitionPath.FromSegments(resolved).ToPointer();
    }

    /// <summary>Returns the index of the choice entry for a string enum token, creating the entry when absent.</summary>
    private static int Choice(JsonObject schema, JsonObject metadata, string token)
    {
        if (schema["enum"] is not JsonArray values
            || !values.Any(value => value is JsonValue member && member.TryGetValue(out string? text) && text == token))
            throw new DefinitionException("localization-target");
        if (metadata["choices"] is not JsonArray choices) metadata["choices"] = choices = new JsonArray();
        for (int index = 0; index < choices.Count; index++)
            if (choices[index]?["value"] is JsonValue value && value.TryGetValue(out string? text) && text == token)
                return index;
        choices.Add(new JsonObject { ["value"] = token });
        return choices.Count - 1;
    }
}
