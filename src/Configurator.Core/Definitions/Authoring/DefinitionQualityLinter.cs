namespace Configurator.Core.Definitions.Authoring;

using System.Globalization;
using System.Text.Json;

/// <summary>One authoring quality finding: a stable code and the runtime schema path it concerns.</summary>
public sealed record DefinitionQualityAdvisory(string Code, string SchemaPath);

/// <summary>
/// Reports authoring quality advisories for an admitted definition. Advisories never change admission: they flag
/// operator-facing text that echoes raw identifiers or exporter internals, missing guidance, formatting hints,
/// ungrouped dense required sections, duplicate choice explanations, and unreachable layout.
/// </summary>
public static class DefinitionQualityLinter
{
    private const int MaximumUngroupedRequiredLeaves = 12;

    /// <summary>Stable advisory codes, in reporting order.</summary>
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        "raw-identifier-title", "verbatim-choice-label", "jsonpath-description", "exporter-diagnostic-description",
        "missing-deprecation-message", "missing-section-description", "required-secret-missing-guidance",
        "secret-missing-acquisition-guidance", "required-setting-missing-guidance", "choice-missing-guidance",
        "choice-duplicate-explanation", "description-missing-format-hint", "section-high-required-density",
        "unreferenced-journey-section", "output-option-missing-description", "output-selector-missing-next-actions"
    });

    private static readonly string[] ExporterPhrases = ["Host validator:", "Host restart:", "startup-owned:"];

    private static readonly string[] FormatHintCues =
    [
        "://", "@", "`", "e.g.", "for example", "example:", "such as",
        "rfc", "iso", "uuid", "hostname", "ipv4", "ipv6", "https", "http",
        "pattern", "format", "مثال", "صيغة", "تنسيق"
    ];

    public static IReadOnlyList<DefinitionQualityAdvisory> Lint(Definition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var advisories = new List<DefinitionQualityAdvisory>();
        var requiredLeaves = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<(DefinitionNode Node, string? Name, string Path, string DataPointer, bool StaticallyRequired, bool RequiresGuidance, FieldSensitivity Inherited)>();
        pending.Push((definition.Configuration, null, "/configuration", "", true, true, FieldSensitivity.Public));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Node(current.Node, current.Name, current.Path, current.RequiresGuidance, current.Inherited, advisories);
            bool leaf = current.Node.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
            if (leaf && current.StaticallyRequired && current.DataPointer.Length > 0)
                requiredLeaves.Add(current.DataPointer);
            var sensitivity = (FieldSensitivity)Math.Max((int)current.Inherited, (int)FieldDefinition.SensitivityOf(current.Node));
            HashSet<string>? conditionalTargets = CollectConditionalRequiredTargets(current.Node.Keywords);
            foreach (string name in current.Node.OrderedPropertyNames.Reverse())
            {
                DefinitionNode child = current.Node.Properties[name];
                bool locallyRequired = current.Node.RequiredSet.Contains(name);
                bool staticallyRequired = current.StaticallyRequired && locallyRequired;
                bool requiresGuidance = locallyRequired
                    || (conditionalTargets is not null && conditionalTargets.Contains(name))
                    || HasRequiredWhen(child.Keywords);
                pending.Push((child, name, current.Path + "/properties/" + Escape(name),
                    current.DataPointer + "/" + Escape(name), staticallyRequired, requiresGuidance, sensitivity));
            }
            if (current.Node.Items is { } items)
                pending.Push((items, null, current.Path + "/items", current.DataPointer, false, false, sensitivity));
            if (current.Node.AdditionalProperties is { } map)
                pending.Push((map, null, current.Path + "/additionalProperties", current.DataPointer, false, false, sensitivity));
            for (int index = current.Node.Variants.Count - 1; index >= 0; index--)
                pending.Push((current.Node.Variants[index], null,
                    current.Path + "/oneOf/" + index.ToString(CultureInfo.InvariantCulture), current.DataPointer, false, false, sensitivity));
        }
        if (definition.Presentation is { } presentation)
        {
            if (presentation.OutputSelector is { Options.Count: > 1 } selector)
            {
                if (selector.Options.All(option => option.NextActions.Count == 0))
                    advisories.Add(new("output-selector-missing-next-actions", "/presentation/outputSelector"));
                for (int index = 0; index < selector.Options.Count; index++)
                    if (selector.Options[index].Description is null)
                        advisories.Add(new("output-option-missing-description",
                            "/presentation/outputSelector/options/" + index.ToString(CultureInfo.InvariantCulture)));
            }
            var reachable = presentation.Experiences.SelectMany(journey => journey.Sections).ToHashSet(StringComparer.Ordinal);
            for (int index = 0; index < presentation.Sections.Count; index++)
            {
                DefinitionSection section = presentation.Sections[index];
                string path = "/presentation/sections/" + index.ToString(CultureInfo.InvariantCulture);
                if (section.Description is null) advisories.Add(new("missing-section-description", path));
                if (CountUngroupedRequiredLeaves(section, requiredLeaves) > MaximumUngroupedRequiredLeaves)
                    advisories.Add(new("section-high-required-density", path));
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
        var seenExplanations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int enumIndex = 0; enumIndex < node.OptionMetadata.Count; enumIndex++)
        {
            DefinitionChoice choice = node.OptionMetadata[enumIndex];
            if (choice.MetadataIndex is not { } index)
            {
                advisories.Add(new("choice-missing-guidance",
                    path + "/enum/" + enumIndex.ToString(CultureInfo.InvariantCulture)));
                continue;
            }
            string choicePath = path + "/x-configurator/choices/" + index.ToString(CultureInfo.InvariantCulture);
            if (choice.HasExplicitLabel && choice.Value.ValueKind == JsonValueKind.String && choice.DisplayLabel == choice.WireToken
                && DefinitionDisplayText.Humanize(choice.WireToken) != choice.WireToken)
                advisories.Add(new("verbatim-choice-label", choicePath + "/label"));
            if (choice.Deprecated && choice.DeprecationGuidance is null)
                advisories.Add(new("missing-deprecation-message", choicePath));
            if (choice.Explanation is null)
                advisories.Add(new("choice-missing-guidance", choicePath));
            else if (choice.Explanation.Trim() is { Length: > 0 } normalizedExplanation
                && !seenExplanations.TryAdd(normalizedExplanation, index))
                advisories.Add(new("choice-duplicate-explanation", choicePath));
        }
        var sensitivity = (FieldSensitivity)Math.Max((int)inherited, (int)FieldDefinition.SensitivityOf(node));
        bool leaf = node.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
        if (required && leaf && !HasGuidance(keywords))
            advisories.Add(new(sensitivity == FieldSensitivity.Secret
                ? "required-secret-missing-guidance" : "required-setting-missing-guidance", path));
        if (sensitivity == FieldSensitivity.Secret && leaf && !HasSecretAcquisitionGuidance(node, keywords))
            advisories.Add(new("secret-missing-acquisition-guidance", path));
        if (leaf && Text(keywords, "format") is not null && !HasFormatHint(keywords))
            advisories.Add(new("description-missing-format-hint", path));
    }

    private static HashSet<string>? CollectConditionalRequiredTargets(JsonElement keywords)
    {
        HashSet<string>? targets = null;
        if (keywords.TryGetProperty("if", out _))
        {
            AddRequiredFromBranch(keywords, "then", ref targets);
            AddRequiredFromBranch(keywords, "else", ref targets);
        }
        if (keywords.TryGetProperty("dependentRequired", out JsonElement dependencies)
            && dependencies.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (dependency.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (JsonElement target in dependency.Value.EnumerateArray())
                    if (target.ValueKind == JsonValueKind.String && target.GetString() is { } name)
                        (targets ??= new HashSet<string>(StringComparer.Ordinal)).Add(name);
            }
        }
        return targets;

        static void AddRequiredFromBranch(JsonElement parent, string branchName, ref HashSet<string>? targets)
        {
            if (!parent.TryGetProperty(branchName, out JsonElement branch)
                || branch.ValueKind != JsonValueKind.Object
                || !branch.TryGetProperty("required", out JsonElement required)
                || required.ValueKind != JsonValueKind.Array)
                return;
            foreach (JsonElement item in required.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name)
                    (targets ??= new HashSet<string>(StringComparer.Ordinal)).Add(name);
        }
    }

    private static bool HasRequiredWhen(JsonElement keywords) =>
        keywords.TryGetProperty("x-configurator", out JsonElement extension)
        && extension.ValueKind == JsonValueKind.Object
        && extension.TryGetProperty("requiredWhen", out _);

    private static bool HasGuidance(JsonElement keywords) =>
        Text(keywords, "description") is not null || HasInstructions(keywords);

    private static bool HasInstructions(JsonElement keywords) =>
        keywords.TryGetProperty("x-configurator", out JsonElement extension)
        && extension.TryGetProperty("guidance", out JsonElement guidance)
        && Text(guidance, "instructions") is not null;

    private static bool HasSecretAcquisitionGuidance(DefinitionNode node, JsonElement keywords) =>
        node.Secret is not null || HasInstructions(keywords);

    private static bool HasFormatHint(JsonElement keywords)
    {
        if (keywords.TryGetProperty("examples", out JsonElement examples)
            && examples.ValueKind == JsonValueKind.Array && examples.GetArrayLength() > 0)
            return true;

        if (ContainsFormatCue(Text(keywords, "description"))) return true;
        if (keywords.TryGetProperty("x-configurator", out JsonElement extension))
        {
            if (ContainsFormatCue(Text(extension, "defaultText"))) return true;
            if (extension.TryGetProperty("guidance", out JsonElement guidance)
                && ContainsFormatCue(Text(guidance, "instructions")))
                return true;
        }
        return false;

        static bool ContainsFormatCue(string? candidate) =>
            candidate is { Length: > 0 }
            && FormatHintCues.Any(cue => candidate.Contains(cue, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountUngroupedRequiredLeaves(DefinitionSection section, HashSet<string> requiredLeaves)
    {
        if (section.Pointers.Count == 0 || requiredLeaves.Count == 0) return 0;
        var groupedPointers = section.Groups.SelectMany(group => group.Pointers).ToArray();
        int ungrouped = 0;
        foreach (string leaf in requiredLeaves)
        {
            if (!section.Pointers.Any(pointer => PointerCovers(pointer, leaf))) continue;
            if (groupedPointers.Any(pointer => PointerCovers(pointer, leaf))) continue;
            ungrouped++;
        }
        return ungrouped;
    }

    private static bool PointerCovers(string scopePointer, string leafPointer) =>
        string.Equals(scopePointer, "/", StringComparison.Ordinal)
        || string.Equals(leafPointer, scopePointer, StringComparison.Ordinal)
        || leafPointer.StartsWith(scopePointer + "/", StringComparison.Ordinal);

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
