namespace Configurator.Presentation;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

/// <summary>Admitted metadata for one form; contains no answers, origins or dynamic row positions.</summary>
internal sealed class DefinitionFormIndex
{
    internal sealed class Node
    {
        internal Node(DefinitionNode schema, Node? parent = null, string schemaPath = "")
        {
            Schema = schema;
            SchemaPath = schemaPath;
            JsonElement metadata = schema.Keywords;
            Parent = parent;
            Type = schema.JsonType;
            Title = metadata.TryGetProperty("title", out JsonElement title) ? title.GetString() : null;
            Help = metadata.TryGetProperty("description", out JsonElement help) ? help.GetString() : null;
            JsonElement extension = metadata.TryGetProperty("x-configurator", out JsonElement configured)
                ? configured : default;
            JsonElement guidance = extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("guidance", out JsonElement declared) ? declared : default;
            Instructions = guidance.ValueKind == JsonValueKind.Object &&
                guidance.TryGetProperty("instructions", out JsonElement instructions) ? instructions.GetString() : null;
            SearchTerms = Array.AsReadOnly(guidance.ValueKind == JsonValueKind.Object &&
                guidance.TryGetProperty("searchTerms", out JsonElement terms)
                ? terms.EnumerateArray().Select(term => term.GetString()!).ToArray() : []);
            Properties = schema.Properties.ToDictionary(child => child.Key,
                child => new Node(child.Value, this, schemaPath + "/properties/" +
                    child.Key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)), StringComparer.Ordinal);
            Item = schema.Items is { } item ? new Node(item, this, schemaPath + "/items") : null;
            Additional = schema.AdditionalProperties is { } additional
                ? new Node(additional, this, schemaPath + "/additionalProperties") : null;
            Variants = Array.AsReadOnly(schema.Variants.Select((variant, index) =>
                new Node(variant, this, schemaPath + "/oneOf/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray());
            HasVariants = Variants.Count > 0 || Properties.Values.Any(child => child.HasVariants) ||
                Item?.HasVariants == true || Additional?.HasVariants == true;
            IsProtected = DefinitionSensitivityPolicy.SensitivityOf(schema) != FieldSensitivity.Public ||
                Properties.Values.Any(child => child.IsProtected) || Item?.IsProtected == true ||
                Additional?.IsProtected == true || Variants.Any(child => child.IsProtected);
            Disclosure = extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("detail", out JsonElement detail) ? detail.GetString() switch
                {
                    "basic" => FieldDisclosure.Essential,
                    "advanced" => FieldDisclosure.Advanced,
                    _ => FieldDisclosure.Guided
                } : FieldDisclosure.Guided;
            HasEssentialDecision = Disclosure == FieldDisclosure.Essential ||
                Properties.Values.Any(child => child.HasEssentialDecision) || Item?.HasEssentialDecision == true;
            Controllers = Array.AsReadOnly(extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("applicableWhen", out JsonElement condition)
                ? References(condition).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : []);
        }

        internal DefinitionNode Schema { get; }
        internal string SchemaPath { get; }
        internal JsonElement Metadata => Schema.Keywords;
        internal Node? Parent { get; }
        internal string Type { get; }
        internal string? Title { get; }
        internal string? Help { get; }
        internal string? Instructions { get; }
        internal IReadOnlyList<string> SearchTerms { get; }
        internal Dictionary<string, Node> Properties { get; }
        internal Node? Item { get; }
        internal Node? Additional { get; }
        internal IReadOnlyList<Node> Variants { get; }
        internal bool HasVariants { get; }
        internal bool IsProtected { get; }
        internal bool HasEssentialDecision { get; }
        internal FieldDisclosure Disclosure { get; }
        internal IReadOnlyList<string> Controllers { get; }

        private static IEnumerable<string> References(JsonElement condition)
        {
            if (condition.TryGetProperty("reference", out JsonElement reference))
            {
                DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
                if (reference.GetProperty("scope").GetString() == "root" && path.Segments.Count > 0)
                    yield return path.Segments[0];
            }
            else
                foreach (JsonElement operand in condition.GetProperty("operands").EnumerateArray())
                    foreach (string field in References(operand)) yield return field;
        }
    }

    internal DefinitionFormIndex(Definition definition, IReadOnlyList<DefinitionFormSection> sections,
        IReadOnlyList<DefinitionFormExperience> experiences)
    {
        Root = new Node(definition.Configuration, schemaPath: "/configuration");
        Roots = definition.Fields.ToDictionary(field => field.Id, field => Root.Properties[field.Name], StringComparer.Ordinal);
        FieldIds = Array.AsReadOnly(definition.Fields.Select(field => field.Id).ToArray());
        RootPaths = FieldIds.ToDictionary(id => id,
            id => (IReadOnlyList<string>)Array.AsReadOnly(new[] { id }), StringComparer.Ordinal);
        FieldPositions = FieldIds.Select((id, position) => (Id: id, position))
            .ToDictionary(item => item.Id, item => item.position, StringComparer.Ordinal);
        FieldNames = definition.Fields.ToDictionary(field => field.Id, field => field.Name, StringComparer.Ordinal);
        FieldIdsByName = definition.Fields.ToDictionary(field => field.Name, field => field.Id, StringComparer.Ordinal);
        RootIdentities = definition.Fields.ToDictionary(field => field.Id,
            field => "field-" + definition.FamilyId.ToString("N") + "-" +
                Convert.ToHexString(Encoding.UTF8.GetBytes(field.Id)), StringComparer.Ordinal);
        RootPathKeys = definition.Fields.ToDictionary(field => field.Id,
            field => DefinitionPath.FromSegments([field.Id]), StringComparer.Ordinal);
        SectionsByField = sections.SelectMany(section => section.Fields.Select(field => (field, section)))
            .ToDictionary(item => item.field, item => item.section, StringComparer.Ordinal);
        ExperienceSections = experiences.ToDictionary(experience => experience.Id, experience =>
        {
            var order = experience.Sections.Select((id, position) => (id, position))
                .ToDictionary(item => item.id, item => item.position, StringComparer.Ordinal);
            return (IReadOnlyList<DefinitionFormSection>)Array.AsReadOnly(sections
                .OrderBy(section => order.GetValueOrDefault(section.Id, int.MaxValue)).ToArray());
        }, StringComparer.Ordinal);
        var bindings = new Dictionary<string, (HashSet<string> Artifacts, HashSet<string> Keys)>(StringComparer.Ordinal);
        foreach (ArtifactDefinition artifact in definition.Artifacts)
            CollectBindings(artifact.Projection, artifact.Id, [], bindings, FieldIds);
        Bindings = bindings.ToDictionary(pair => pair.Key, pair => (
            Artifacts: (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.Artifacts.ToArray()),
            Keys: (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.Keys.ToArray())), StringComparer.Ordinal);
    }

    internal Dictionary<string, Node> Roots { get; }
    internal Node Root { get; }
    internal Dictionary<string, int> FieldPositions { get; }
    private Dictionary<string, string> FieldNames { get; }
    internal Dictionary<string, string> FieldIdsByName { get; }
    internal IReadOnlyList<string> FieldIds { get; }
    internal Dictionary<string, IReadOnlyList<string>> RootPaths { get; }
    internal Dictionary<string, string> RootIdentities { get; }
    private Dictionary<string, DefinitionPath> RootPathKeys { get; }
    internal Dictionary<string, DefinitionFormSection> SectionsByField { get; }
    internal Dictionary<string, IReadOnlyList<DefinitionFormSection>> ExperienceSections { get; }
    internal Dictionary<string, (IReadOnlyList<string> Artifacts, IReadOnlyList<string> Keys)> Bindings { get; }

    internal DefinitionPath PathKey(IReadOnlyList<string> path) =>
        path.Count == 1 && RootPathKeys.TryGetValue(path[0], out DefinitionPath? key)
            ? key : DefinitionPath.FromSegments(path);

    internal DefinitionPath DataPath(IReadOnlyList<string> path) => path.Count > 0 &&
        FieldNames.TryGetValue(path[0], out string? name) ? DefinitionPath.FromSegments([name, .. path.Skip(1)])
            : DefinitionPath.FromSegments(path);

    private static void CollectBindings(JsonElement projection, string artifact, string[] keys,
        Dictionary<string, (HashSet<string> Artifacts, HashSet<string> Keys)> bindings,
        IReadOnlyList<string> roots)
    {
        if (projection.ValueKind != JsonValueKind.Object || !projection.TryGetProperty("op", out JsonElement operation)
            || operation.GetString() == "literal") return;
        if (projection.TryGetProperty("reference", out JsonElement reference) &&
            reference.GetProperty("scope").GetString() == "root")
        {
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            foreach (string fieldId in path.Segments.Count > 0 ? new[] { path.Segments[0] } : roots)
            {
                if (!bindings.TryGetValue(fieldId, out var binding))
                {
                    binding = (new(StringComparer.Ordinal), new(StringComparer.Ordinal));
                    bindings.Add(fieldId, binding);
                }
                binding.Artifacts.Add(artifact);
                binding.Keys.UnionWith(keys);
            }
        }
        foreach (JsonProperty child in projection.EnumerateObject())
            if (child.Name == "members" && child.Value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty member in child.Value.EnumerateObject())
                    CollectBindings(member.Value, artifact, [.. keys, member.Name], bindings, roots);
            else if (child.Value.ValueKind == JsonValueKind.Object)
                CollectBindings(child.Value, artifact, keys, bindings, roots);
            else if (child.Value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in child.Value.EnumerateArray())
                    CollectBindings(item, artifact, keys, bindings, roots);
    }
}
