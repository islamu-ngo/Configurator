namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;
using Configurator.Core.Dotenv;

internal static partial class DefinitionAdmission
{
    internal static DefinitionNode Admit(JsonElement document, SortedSet<string> capabilities)
    {
        Only(document, "schemaVersion", "profile", "familyId", "revision", "project", "applicationVersions",
            "entryPoints", "locales", "configuration", "artifacts", "requiredCapabilities", "presentation", "localization", "annotations", "deployment");
        if (document.GetProperty("schemaVersion").ValueKind != JsonValueKind.String
            || document.GetProperty("schemaVersion").GetString() != DefinitionProfile.RuntimeSchemaVersion
            || document.GetProperty("profile").GetString() != DefinitionProfile.Id)
            throw new DefinitionException("definition-version");
        if (!Guid.TryParseExact(document.GetProperty("familyId").GetString(), "D", out Guid family)
            || family == Guid.Empty || document.GetProperty("revision").GetInt64() < 1)
            throw new DefinitionException("identity-invalid");
        JsonElement project = document.GetProperty("project");
        Only(project, "id", "name", "publisher", "description", "repository", "definitionLicense");
        foreach (string name in new[] { "id", "name", "publisher" }) Text(project.GetProperty(name), 256);
        foreach (string name in new[] { "description", "definitionLicense" })
            if (project.TryGetProperty(name, out var text)) Text(text, name == "description" ? 4096 : 256);
        if (project.TryGetProperty("repository", out var repository)) PublicUrl(repository, fragment: false);
        foreach (string name in new[] { "applicationVersions", "entryPoints", "locales" })
            Strings(document.GetProperty(name), 128, 128, nonempty: true);
        JsonElement configurationSource = document.GetProperty("configuration");
        DefinitionNode configuration = DefinitionNodeReader.Read(configurationSource, capabilities, true, false,
            configurationSource, new(StringComparer.Ordinal), new(CompositionLimits.Default), 1);
        if (configuration.Type != DefinitionNodeType.Mapping || configuration.IsNullable)
            throw new DefinitionException("configuration-object-required");
        if (configuration.Properties.Count > 1024) throw new DefinitionException("document-limit");
        ValidateApplicability(configuration, capabilities);
        if (document.TryGetProperty("annotations", out var annotations) && annotations.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("annotations-invalid");
        ValidateLocalization(document);
        JsonElement artifacts = document.GetProperty("artifacts");
        if (artifacts.GetArrayLength() is < 1 or > 64) throw new DefinitionException("artifact-limit");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            Only(artifact, "id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection",
                "title", "description", "consumerIds", "surface", "environmentDialect");
            string id = Text(artifact.GetProperty("id"), 128);
            if (!char.IsAsciiLetter(id[0]) || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || !ids.Add(id)) throw new DefinitionException("artifact-identifier");
            Choice(artifact, "format", DefinitionVocabulary.Formats);
            Choice(artifact, "targetUpdateMode", DefinitionVocabulary.TargetUpdateModes);
            Choice(artifact, "restartRequirement", DefinitionVocabulary.RestartRequirements);
            ValidateArtifactUsage(artifact, capabilities);
            string file = Text(artifact.GetProperty("fileName"), 128);
            string stem = file.Split('.')[0];
            if (file is "." or ".." || file[^1] is '.' or ' '
                || !file.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL"
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsAsciiDigit(stem[3])
                || !files.Add(file)) throw new DefinitionException("filename-invalid");
            ValidateProjection(artifact.GetProperty("projection"), configuration, null, capabilities, 0);
            if (artifact.GetProperty("format").GetString() == "dotenv")
                ValidateDotenv(artifact.GetProperty("projection"), configuration);
            if (artifact.GetProperty("format").GetString() == "markdown")
                ValidateMarkdown(artifact.GetProperty("projection"), configuration);
        }
        if (document.TryGetProperty("deployment", out var deployment))
            ValidateDeployment(deployment, configuration, artifacts, capabilities);
        if (document.TryGetProperty("presentation", out var presentation))
            ValidatePresentation(presentation, configuration, artifacts);
        return configuration;

    }

    private static void ValidateApplicability(DefinitionNode root, SortedSet<string> capabilities)
    {
        if (root.Keywords.TryGetProperty("x-configurator", out var rootMetadata)
            && rootMetadata.TryGetProperty("applicableWhen", out _)) throw new DefinitionException("condition-context");
        if (root.Variants.Count > 0)
        {
            foreach (DefinitionNode branch in root.Variants) ValidateApplicability(branch, capabilities);
            return;
        }
        ValidateScope(root, null);

        void ValidateScope(DefinitionNode scope, DefinitionNode? item)
        {
            foreach (DefinitionNode branch in scope.Variants)
                ValidateScope(branch, ReferenceEquals(scope, item) ? branch : item);
            var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach ((string name, DefinitionNode node) in scope.Properties)
            {
                string[] references = [];
                if (node.Keywords.TryGetProperty("x-configurator", out var metadata)
                    && metadata.TryGetProperty("applicableWhen", out var condition))
                    ValidateCondition(condition, root, capabilities, item);
                if (ReferenceEquals(scope, root) || ReferenceEquals(scope, item))
                    references = DefinitionValidator.SchemaReferences(node, item is null ? "root" : "item")
                        .Where(scope.Properties.ContainsKey).ToArray();
                dependencies.Add(name, references);
                ValidateScope(node, item);
            }
            if (scope.Items is { } row) ValidateScope(row, row);
            if (scope.AdditionalProperties is { } map) ValidateScope(map, item);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in scope.Properties.Keys) Visit(name);

            void Visit(string name)
            {
                if (visited.Contains(name)) return;
                if (!visiting.Add(name)) throw new DefinitionException("activation-cycle");
                foreach (string reference in dependencies[name]) Visit(reference);
                visiting.Remove(name);
                visited.Add(name);
            }
        }
    }

    private static void ValidateCondition(JsonElement condition, DefinitionNode root, SortedSet<string> capabilities,
        DefinitionNode? item = null)
    {
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            Only(condition, "op", "operands");
            JsonElement operands = condition.GetProperty("operands");
            if (operands.GetArrayLength() < 1 || operation == "not" && operands.GetArrayLength() != 1)
                throw new DefinitionException("condition-operands");
            foreach (JsonElement operand in operands.EnumerateArray()) ValidateCondition(operand, root, capabilities, item);
            return;
        }
        if (operation is not ("exists" or "equals" or "notEquals" or "in"))
            throw new DefinitionException("condition-operation");
        Only(condition, operation == "exists" ? ["op", "reference"]
            : operation == "in" ? ["op", "reference", "values"] : ["op", "reference", "value"]);
        JsonElement reference = condition.GetProperty("reference");
        if (DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments.Count == 0)
            throw new DefinitionException("condition-context");
        DefinitionNode controller = Reference(reference, root, item);
        foreach (JsonElement expected in operation == "exists" ? Array.Empty<JsonElement>()
            : operation == "in" ? condition.GetProperty("values").EnumerateArray().ToArray()
            : new[] { condition.GetProperty("value") })
        {
            bool compatible = expected.ValueKind == JsonValueKind.Null ? controller.IsNullable : controller.Type switch
            {
                DefinitionNodeType.Text => expected.ValueKind == JsonValueKind.String,
                DefinitionNodeType.Boolean => expected.ValueKind is JsonValueKind.True or JsonValueKind.False,
                DefinitionNodeType.WholeNumber => expected.ValueKind == JsonValueKind.Number && expected.TryGetInt64(out _),
                DefinitionNodeType.Number => expected.ValueKind == JsonValueKind.Number && expected.TryGetDecimal(out _),
                DefinitionNodeType.Mapping => expected.ValueKind == JsonValueKind.Object,
                DefinitionNodeType.Sequence => expected.ValueKind == JsonValueKind.Array,
                _ => false
            };
            if (!compatible) throw new DefinitionException("condition-type");
        }
        if (operation == "in" && condition.GetProperty("values").GetArrayLength() == 0)
            throw new DefinitionException("condition-operands");
        capabilities.Add(reference.GetProperty("scope").GetString() == "item" ? "predicate.item" : "predicate.root");
    }

    private static void ValidateProjection(JsonElement projection, DefinitionNode root, DefinitionNode? item,
        SortedSet<string> capabilities, int depth)
    {
        if (depth > CompositionLimits.DefaultNestingDepth) throw new DefinitionException("projection-limit");
        string op = projection.GetProperty("op").GetString()!;
        string[] members = op switch
        {
            "literal" => ["op", "value"],
            "copy" => ["op", "reference"],
            "omit" or "index" => ["op"],
            "object" => ["op", "members"],
            "list" => ["op", "items"],
            "repeat" => ["op", "reference", "item"],
            _ => throw new DefinitionException("projection-operation")
        };
        Only(projection, [.. members, "when"]);
        if (projection.TryGetProperty("when", out var condition)) ValidateCondition(condition, root, capabilities, item);
        capabilities.Add("projection." + op);
        switch (op)
        {
            case "literal": _ = projection.GetProperty("value"); break;
            case "copy": _ = Reference(projection.GetProperty("reference"), root, item); break;
            case "index": if (item is null) throw new DefinitionException("index-context"); break;
            case "object":
                foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                    ValidateProjection(member.Value, root, item, capabilities, depth + 1);
                break;
            case "list":
                foreach (JsonElement child in projection.GetProperty("items").EnumerateArray())
                    ValidateProjection(child, root, item, capabilities, depth + 1);
                break;
            case "repeat":
                DefinitionNode source = Reference(projection.GetProperty("reference"), root, item);
                if (source.Type != DefinitionNodeType.Sequence || source.IsNullable)
                    throw new DefinitionException("repeat-source");
                ValidateProjection(projection.GetProperty("item"), root, source.Items, capabilities, depth + 1);
                break;
        }
    }

    private static DefinitionNode Reference(JsonElement reference, DefinitionNode root, DefinitionNode? item)
    {
        Only(reference, "scope", "path");
        DefinitionNode node = reference.GetProperty("scope").GetString() switch
        {
            "root" => root,
            "item" => item ?? throw new DefinitionException("reference-context"),
            _ => throw new DefinitionException("reference-scope")
        };
        foreach (string segment in DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments)
        {
            if (node.Type == DefinitionNodeType.Mapping && node.Properties.TryGetValue(segment, out var property)) node = property;
            else if (node.Type == DefinitionNodeType.Sequence && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && segment == index.ToString(CultureInfo.InvariantCulture)) node = node.Items!;
            else throw new DefinitionException("reference-invalid");
        }
        return node;
    }

    private static void ValidateDotenv(JsonElement projection, DefinitionNode root)
    {
        if (projection.GetProperty("op").GetString() == "omit") return;
        if (projection.GetProperty("op").GetString() != "object") throw new DefinitionException("dotenv-shape");
        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
        {
            string key = member.Name;
            if (!DotenvCodec.IsEnvironmentKey(key)) throw new DefinitionException("dotenv-key");
            JsonElement value = member.Value;
            switch (value.GetProperty("op").GetString())
            {
                case "omit": break;
                case "copy":
                    DefinitionNode source = Reference(value.GetProperty("reference"), root, null);
                    if (source.IsNullable || source.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)
                        throw new DefinitionException("dotenv-shape");
                    break;
                case "literal":
                    if (value.GetProperty("value").ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                        throw new DefinitionException("dotenv-shape");
                    break;
                default: throw new DefinitionException("dotenv-shape");
            }
        }
    }

    private static void ValidateMarkdown(JsonElement projection, DefinitionNode root)
    {
        bool valid = projection.GetProperty("op").GetString() switch
        {
            "omit" => true,
            "literal" => projection.GetProperty("value").ValueKind == JsonValueKind.Array &&
                projection.GetProperty("value").EnumerateArray().All(value => value.ValueKind == JsonValueKind.String),
            "list" => projection.GetProperty("items").EnumerateArray().All(value => TextResult(value, null)),
            "copy" => TextSequence(Reference(projection.GetProperty("reference"), root, null)),
            "repeat" => TextResult(projection.GetProperty("item"),
                Reference(projection.GetProperty("reference"), root, null).Items),
            _ => false
        };
        if (!valid) throw new DefinitionException("markdown-shape");

        static bool TextSequence(DefinitionNode node) => !node.IsNullable && node.Type == DefinitionNodeType.Sequence
            && node.Items is { Type: DefinitionNodeType.Text, IsNullable: false };

        bool TextResult(JsonElement value, DefinitionNode? item) => value.GetProperty("op").GetString() switch
        {
            "omit" => true,
            "literal" => value.GetProperty("value").ValueKind == JsonValueKind.String,
            "copy" => Reference(value.GetProperty("reference"), root, item)
                is { Type: DefinitionNodeType.Text, IsNullable: false },
            _ => false
        };
    }

    private static void ValidatePresentation(JsonElement presentation, DefinitionNode root, JsonElement artifacts)
    {
        Only(presentation, "sections", "journeys", "defaultJourney", "navigation", "outputSelector");
        if (presentation.TryGetProperty("navigation", out _)) Choice(presentation, "navigation", "wizard", "sections");
        var sections = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement section in presentation.GetProperty("sections").EnumerateArray())
        {
            Only(section, "id", "title", "description", "pointers");
            if (!sections.Add(Text(section.GetProperty("id"), 64))) throw new DefinitionException("presentation-duplicate");
            Text(section.GetProperty("title"), 128);
            if (section.TryGetProperty("description", out var description)) Text(description, 2048);
            var pointers = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement pointer in section.GetProperty("pointers").EnumerateArray())
            {
                if (!pointers.Add(pointer.GetString()!)) throw new DefinitionException("presentation-duplicate");
                _ = Reference(JsonSerializer.SerializeToElement(new { scope = "root", path = pointer.GetString() }), root, null);
            }
        }
        if (sections.Count is < 1 or > 64) throw new DefinitionException("presentation-limit");
        var journeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement journey in presentation.GetProperty("journeys").EnumerateArray())
        {
            Only(journey, "id", "title", "description", "sections");
            if (!journeys.Add(Text(journey.GetProperty("id"), 64))) throw new DefinitionException("presentation-duplicate");
            Text(journey.GetProperty("title"), 128);
            if (journey.TryGetProperty("description", out var description)) Text(description, 2048);
            Strings(journey.GetProperty("sections"), 64, 64, true);
            foreach (JsonElement section in journey.GetProperty("sections").EnumerateArray())
                if (!sections.Contains(section.GetString()!)) throw new DefinitionException("presentation-reference");
        }
        if (journeys.Count is < 1 or > 16 || !journeys.Contains(presentation.GetProperty("defaultJourney").GetString()!))
            throw new DefinitionException("presentation-default");
        if (presentation.TryGetProperty("outputSelector", out var selector))
            ValidateOutputSelector(selector, root, artifacts);
    }

    private static void ValidateOutputSelector(JsonElement selector, DefinitionNode root, JsonElement artifacts)
    {
        Only(selector, "pointer", "options");
        DefinitionPath pointer = DefinitionPath.Parse(selector.GetProperty("pointer").GetString()!);
        if (pointer.Segments.Count != 1 || !PublicEditable(root)
            || !root.Properties.TryGetValue(pointer.Segments[0], out DefinitionNode? field)
            || field.Type != DefinitionNodeType.Text || field.IsNullable || !PublicEditable(field)
            || field.Keywords.GetProperty("x-configurator").TryGetProperty("applicableWhen", out _)
            || !field.Keywords.TryGetProperty("enum", out JsonElement enumeration))
            throw new DefinitionException("output-selector-field");
        var remaining = enumeration.EnumerateArray().Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        JsonElement options = selector.GetProperty("options");
        if (remaining.Count is < 1 or > 64 || options.GetArrayLength() is < 1 or > 64)
            throw new DefinitionException("output-selector-limit");
        foreach (JsonElement option in options.EnumerateArray())
        {
            Only(option, "value", "title", "artifactIds");
            string value = option.GetProperty("value").GetString()!;
            if (!remaining.Remove(value)) throw new DefinitionException("output-selector-value");
            Text(option.GetProperty("title"), 128);
            JsonElement artifactIds = option.GetProperty("artifactIds");
            Strings(artifactIds, 64, 128, nonempty: true);
            foreach (JsonElement id in artifactIds.EnumerateArray())
            {
                JsonElement artifact = artifacts.EnumerateArray().FirstOrDefault(
                    artifact => artifact.GetProperty("id").GetString() == id.GetString());
                if (artifact.ValueKind == JsonValueKind.Undefined
                    || !artifact.GetProperty("projection").TryGetProperty("when", out JsonElement guard)
                    || guard.GetProperty("op").GetString() != "equals"
                    || guard.GetProperty("reference").GetProperty("scope").GetString() != "root"
                    || guard.GetProperty("reference").GetProperty("path").GetString() != pointer.ToPointer()
                    || guard.GetProperty("value").ValueKind != JsonValueKind.String
                    || guard.GetProperty("value").GetString() != value)
                    throw new DefinitionException("output-selector-artifact");
            }
        }
        if (remaining.Count != 0) throw new DefinitionException("output-selector-value");

        static bool PublicEditable(DefinitionNode node) =>
            node.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
            && metadata.TryGetProperty("sensitivity", out JsonElement sensitivity)
            && sensitivity.GetString() == "public"
            && (!node.Keywords.TryGetProperty("writeOnly", out JsonElement writeOnly) || !writeOnly.GetBoolean())
            && (!node.Keywords.TryGetProperty("readOnly", out JsonElement readOnly) || !readOnly.GetBoolean());
    }

    private static void ValidateLocalization(JsonElement document)
    {
        var locales = new SortedSet<string>(StringComparer.Ordinal) { "en" };
        if (document.TryGetProperty("localization", out var localization))
        {
            Only(localization, "defaultLocale", "translations");
            locales.Clear();
            locales.Add(Text(localization.GetProperty("defaultLocale"), 128));
            foreach (JsonProperty translation in localization.GetProperty("translations").EnumerateObject())
            {
                Text(JsonSerializer.SerializeToElement(translation.Name), 128);
                locales.Add(translation.Name);
                Only(translation.Value, "direction", "overrides");
                if (translation.Value.TryGetProperty("direction", out _)) Choice(translation.Value, "direction", "ltr", "rtl");
                if (translation.Value.TryGetProperty("overrides", out var overrides))
                    foreach (JsonProperty entry in overrides.EnumerateObject())
                    {
                        IReadOnlyList<string> segments = DefinitionPath.Parse(entry.Name).Segments;
                        if (segments.Count == 0 || segments[^1] is not ("title" or "description" or "instructions"
                            or "label" or "explanation" or "units"))
                            throw new DefinitionException("localization-target");
                        JsonElement target = document;
                        foreach (string segment in segments)
                        {
                            if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty(segment, out var child)) target = child;
                            else if (target.ValueKind == JsonValueKind.Array
                                && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                                && index >= 0 && index < target.GetArrayLength()
                                && segment == index.ToString(CultureInfo.InvariantCulture)) target = target[index];
                            else throw new DefinitionException("localization-target");
                        }
                        if (target.ValueKind != JsonValueKind.String) throw new DefinitionException("localization-target");
                        Text(entry.Value, 4096);
                    }
            }
        }
        if (!document.GetProperty("locales").EnumerateArray().Select(value => value.GetString()).SequenceEqual(locales, StringComparer.Ordinal))
            throw new DefinitionException("locales-invalid");
    }

    internal static void ValidateGuidance(JsonElement guidance)
    {
        Only(guidance, "instructions", "links", "searchTerms");
        if (guidance.TryGetProperty("instructions", out var instructions))
        {
            string text = instructions.GetString()!;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4096
                || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))
                || text.Contains("${", StringComparison.Ordinal) || text.Contains("{{", StringComparison.Ordinal)
                || text.Contains("}}", StringComparison.Ordinal)) throw new DefinitionException("guidance-invalid");
        }
        if (guidance.TryGetProperty("links", out var links))
        {
            if (links.GetArrayLength() > 4) throw new DefinitionException("guidance-limit");
            foreach (JsonElement link in links.EnumerateArray())
            {
                Only(link, "title", "url");
                Text(link.GetProperty("title"), 128);
                PublicUrl(link.GetProperty("url"), fragment: true);
            }
        }
        if (guidance.TryGetProperty("searchTerms", out var terms)) Strings(terms, 16, 64, false);
    }

    private static void PublicUrl(JsonElement value, bool fragment)
    {
        string url = Text(value, 2048);
        if (url.Any(char.IsWhiteSpace) || url.Contains("${", StringComparison.Ordinal) || url.Contains("{{", StringComparison.Ordinal)
            || url.Contains("}}", StringComparison.Ordinal) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0 || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || !fragment && uri.Fragment.Length != 0)
            throw new DefinitionException("public-url-invalid");
    }

    private static void Strings(JsonElement array, int maximum, int textMaximum, bool nonempty)
    {
        if (array.GetArrayLength() > maximum || nonempty && array.GetArrayLength() == 0)
            throw new DefinitionException("metadata-limit");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in array.EnumerateArray())
            if (!seen.Add(Text(value, textMaximum))) throw new DefinitionException("metadata-duplicate");
    }

    internal static string Text(JsonElement value, int maximum)
    {
        string text = value.GetString() ?? throw new DefinitionException("text-invalid");
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)
            || text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal))
            throw new DefinitionException("text-invalid");
        return text;
    }

    internal static void Choice(JsonElement value, string name, params string[] allowed)
    {
        if (!allowed.Contains(value.GetProperty(name).GetString(), StringComparer.Ordinal))
            throw new DefinitionException("vocabulary-invalid");
    }

    internal static void Only(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new DefinitionException("member-unsupported");
    }
}
