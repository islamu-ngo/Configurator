namespace Configurator.Core.Definitions.Authoring;

using System.Globalization;
using System.Text.Json;

/// <summary>One authoring quality finding: a stable code and the runtime schema path it concerns.</summary>
public sealed record DefinitionQualityAdvisory(string Code, string SchemaPath);

/// <summary>
/// Reports authoring quality advisories for an admitted definition. Advisories never change admission: they flag
/// operator-facing text that echoes raw identifiers or exporter internals, missing guidance, and unreachable layout.
/// </summary>
public static class DefinitionQualityLinter
{
    /// <summary>Stable advisory codes, in reporting order.</summary>
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        "raw-identifier-title", "verbatim-choice-label", "jsonpath-description", "exporter-diagnostic-description",
        "missing-deprecation-message", "missing-section-description", "required-secret-missing-guidance",
        "unreferenced-journey-section"
    });

    private static readonly string[] ExporterPhrases = ["Host validator:", "Host restart:", "startup-owned:"];

    public static IReadOnlyList<DefinitionQualityAdvisory> Lint(Definition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var advisories = new List<DefinitionQualityAdvisory>();
        var pending = new Stack<(DefinitionNode Node, string? Name, string Path, bool Required, FieldSensitivity Inherited)>();
        pending.Push((definition.Configuration, null, "/configuration", false, FieldSensitivity.Public));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Node(current.Node, current.Name, current.Path, current.Required, current.Inherited, advisories);
            var sensitivity = (FieldSensitivity)Math.Max((int)current.Inherited, (int)FieldDefinition.SensitivityOf(current.Node));
            foreach (string name in current.Node.OrderedPropertyNames.Reverse())
                pending.Push((current.Node.Properties[name], name, current.Path + "/properties/" + Escape(name),
                    current.Node.Required.Contains(name, StringComparer.Ordinal), sensitivity));
            if (current.Node.Items is { } items) pending.Push((items, null, current.Path + "/items", false, sensitivity));
            if (current.Node.AdditionalProperties is { } map)
                pending.Push((map, null, current.Path + "/additionalProperties", false, sensitivity));
            for (int index = current.Node.Variants.Count - 1; index >= 0; index--)
                pending.Push((current.Node.Variants[index], null,
                    current.Path + "/oneOf/" + index.ToString(CultureInfo.InvariantCulture), false, sensitivity));
        }
        if (definition.Presentation is { } presentation)
        {
            var reachable = presentation.Experiences.SelectMany(journey => journey.Sections).ToHashSet(StringComparer.Ordinal);
            for (int index = 0; index < presentation.Sections.Count; index++)
            {
                DefinitionSection section = presentation.Sections[index];
                string path = "/presentation/sections/" + index.ToString(CultureInfo.InvariantCulture);
                if (section.Description is null) advisories.Add(new("missing-section-description", path));
                // Deployment selectors have dedicated recipe and scope controls; a section of only them needs no journey.
                if (!reachable.Contains(section.Id) && !SelectorsOnly(definition, section))
                    advisories.Add(new("unreferenced-journey-section", path));
            }
        }
        return advisories.AsReadOnly();
    }

    private static void Node(DefinitionNode node, string? name, string path, bool required, FieldSensitivity inherited,
        List<DefinitionQualityAdvisory> advisories)
    {
        JsonElement keywords = node.Keywords;
        string? title = Text(keywords, "title");
        if (title is not null && name is not null && RawIdentifier(title)) advisories.Add(new("raw-identifier-title", path + "/title"));
        foreach (string member in new[] { "title", "description" })
            if (Text(keywords, member) is { } text)
            {
                if (JsonPath(text)) advisories.Add(new("jsonpath-description", path + "/" + member));
                if (ExporterPhrases.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                    advisories.Add(new("exporter-diagnostic-description", path + "/" + member));
            }
        if (keywords.TryGetProperty("deprecated", out JsonElement deprecated) && deprecated.ValueKind == JsonValueKind.True
            && node.PresentationMetadata.DeprecationGuidance is null)
            advisories.Add(new("missing-deprecation-message", path));
        foreach (DefinitionChoice choice in node.OptionMetadata)
        {
            if (choice.MetadataIndex is not { } index) continue;
            string choicePath = path + "/x-configurator/choices/" + index.ToString(CultureInfo.InvariantCulture);
            if (choice.HasExplicitLabel && choice.Value.ValueKind == JsonValueKind.String && choice.DisplayLabel == choice.WireToken
                && DefinitionDisplayText.Humanize(choice.WireToken) != choice.WireToken)
                advisories.Add(new("verbatim-choice-label", choicePath + "/label"));
            if (choice.Deprecated && choice.DeprecationGuidance is null)
                advisories.Add(new("missing-deprecation-message", choicePath));
        }
        var sensitivity = (FieldSensitivity)Math.Max((int)inherited, (int)FieldDefinition.SensitivityOf(node));
        bool leaf = node.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
        if (required && leaf && sensitivity == FieldSensitivity.Secret && Text(keywords, "description") is null
            && !(keywords.TryGetProperty("x-configurator", out JsonElement extension)
                && extension.TryGetProperty("guidance", out JsonElement guidance) && guidance.TryGetProperty("instructions", out _)))
            advisories.Add(new("required-secret-missing-guidance", path));
    }

    /// <summary>True when every value a section reaches is a deployment recipe or scope selector.</summary>
    private static bool SelectorsOnly(Definition definition, DefinitionSection section)
    {
        if (definition.Deployment is not { } deployment || section.Pointers.Count == 0) return false;
        var selectors = new HashSet<string>(StringComparer.Ordinal) { deployment.RecipePointer, deployment.ScopePointer };
        return section.Pointers.All(pointer => Leaves(definition.Configuration, DefinitionPath.Parse(pointer)).All(selectors.Contains));

        static IEnumerable<string> Leaves(DefinitionNode root, DefinitionPath location)
        {
            DefinitionNode node = root;
            foreach (string segment in location.Segments)
                if (!node.Properties.TryGetValue(segment, out node!)) return [location.ToPointer()];
            if (node.Type != DefinitionNodeType.Mapping || node.Properties.Count == 0) return [location.ToPointer()];
            var leaves = new List<string>();
            foreach (string name in node.Properties.Keys)
                leaves.AddRange(Leaves(root, DefinitionPath.FromSegments([.. location.Segments, name])));
            return leaves;
        }
    }

    /// <summary>Single-token text that humanization would rewrite, such as <c>PUBLIC_BASE_URL</c> or <c>events.require_approval</c>.</summary>
    private static bool RawIdentifier(string title) =>
        !title.Any(char.IsWhiteSpace) && DefinitionDisplayText.Humanize(title) != title;

    private static bool JsonPath(string text)
    {
        for (int index = text.IndexOf("$.", StringComparison.Ordinal); index >= 0;
             index = text.IndexOf("$.", index + 2, StringComparison.Ordinal))
            if (index + 2 < text.Length && (char.IsAsciiLetter(text[index + 2]) || text[index + 2] is '_' or '['))
                return true;
        return false;
    }

    private static string? Text(JsonElement keywords, string name) =>
        keywords.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Escape(string name) =>
        name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
