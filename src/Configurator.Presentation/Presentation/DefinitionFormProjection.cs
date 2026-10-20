namespace Configurator.Presentation;

using System.Text.Json;
using System.Collections.ObjectModel;
using Configurator.Core.Definitions;

public sealed record DefinitionFormExperience
{
    internal DefinitionFormExperience(string id, string title, SetupFieldDisclosure initialDisclosure,
        string navigation, IEnumerable<string> sections)
    {
        Id = id;
        Title = title;
        InitialDisclosure = initialDisclosure;
        Navigation = navigation;
        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public SetupFieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
}

public sealed record DefinitionFormProgress(int Ready, int Required, int Missing, int Invalid, int Deferred)
{
    public int? Percent => Required == 0 ? null : 100 * Ready / Required;
}

public sealed record DefinitionFormSection
{
    internal DefinitionFormSection(string id, string title, string? description, IEnumerable<string> fields,
        SetupFieldDisclosure disclosure = SetupFieldDisclosure.Guided)
    {
        Id = id;
        Title = title;
        Description = description;
        Fields = Array.AsReadOnly(fields.ToArray());
        Disclosure = disclosure;
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<string> Fields { get; }
    public SetupFieldDisclosure Disclosure { get; }
}

/// <summary>Value-free workspace projection shared by native and browser clients.</summary>
public sealed class DefinitionFormProjection
{
    private readonly HashSet<string> _visibleFields;

    private DefinitionFormProjection(DefinitionFormProgress progress,
        DefinitionDiscoverySnapshot discovery, IEnumerable<DefinitionDiscoveryEntry> entries,
        IEnumerable<DefinitionFormSection> sections, IEnumerable<string> visibleFields,
        Dictionary<string, DefinitionFormProgress> sectionProgress)
    {
        Progress = progress;
        Discovery = discovery;
        Entries = Array.AsReadOnly(entries.ToArray());
        Sections = Array.AsReadOnly(sections.ToArray());
        SectionProgress = new ReadOnlyDictionary<string, DefinitionFormProgress>(
            new Dictionary<string, DefinitionFormProgress>(sectionProgress, StringComparer.Ordinal));
        _visibleFields = visibleFields.ToHashSet(StringComparer.Ordinal);
    }

    public DefinitionFormProgress Progress { get; }
    public DefinitionDiscoverySnapshot Discovery { get; }
    public IReadOnlyList<DefinitionDiscoveryMatch> Matches => Discovery.Matches;
    public IReadOnlyList<DefinitionDiscoveryEntry> Entries { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public IReadOnlyDictionary<string, DefinitionFormProgress> SectionProgress { get; }
    public bool IsFieldVisible(string fieldId) => _visibleFields.Contains(fieldId);

    internal static DefinitionFormProjection Create(DefinitionFormModel form)
    {
        DefinitionValidationResult validation = form.Validate();
        IReadOnlyList<DefinitionDiagnostic> inputErrors = form.InputDiagnostics;
        DefinitionFormProgress progress = CountProgress(form, validation.Nodes, validation.Diagnostics, inputErrors);
        DefinitionFormExperience experience = form.Experiences.Single(item => item.Id == form.SelectedExperienceId);
        DefinitionFormSection[] sections = form.Sections.OrderBy(section =>
            experience.Sections.Contains(section.Id) ? experience.Sections.ToList().IndexOf(section.Id) : int.MaxValue).ToArray();
        var sectionProgress = sections.ToDictionary(section => section.Id, section =>
            CountProgress(form, validation.Nodes.Where(node => section.Fields.Contains(node.FieldId)),
                validation.Diagnostics.Where(diagnostic => section.Fields.Contains(diagnostic.FieldId)),
                inputErrors.Where(diagnostic => section.Fields.Contains(diagnostic.FieldId)).ToArray()), StringComparer.Ordinal);
        var bindings = new Dictionary<string, (HashSet<string> Artifacts, HashSet<string> Keys)>(StringComparer.Ordinal);
        foreach (SetupArtifactDefinition artifact in form.Definition.Artifacts)
            CollectBindings(artifact.Projection, artifact.Id, [], bindings);
        var entries = new List<DefinitionDiscoveryEntry>();
        foreach (DefinitionNodeState node in validation.Nodes)
        {
            string[] path = [node.FieldId, .. node.RelativePath];
            if (!form.TryMetadata(path, out JsonElement schema, out _)) continue;
            DefinitionFormSection section = sections.First(item => item.Fields.Contains(node.FieldId));
            string type = schema.GetProperty("type").GetString()!;
            bool inputError = inputErrors.Any(diagnostic => diagnostic.FieldId == node.FieldId &&
                diagnostic.RelativePath.Take(node.RelativePath.Count).SequenceEqual(node.RelativePath));
            JsonElement guidance = schema.TryGetProperty("guidance", out JsonElement help) ? help : default;
            string? instructions = guidance.ValueKind == JsonValueKind.Object &&
                guidance.TryGetProperty("instructions", out JsonElement instruction) ? instruction.GetString() : null;
            string[] terms = guidance.ValueKind == JsonValueKind.Object &&
                guidance.TryGetProperty("searchTerms", out JsonElement searchTerms)
                ? searchTerms.EnumerateArray().Select(term => term.GetString()!).ToArray() : [];
            bindings.TryGetValue(node.FieldId, out var binding);
            entries.Add(new(node.FieldId, node.RelativePath,
                schema.TryGetProperty("title", out JsonElement title) ? title.GetString()! : section.Title + " item",
                section.Id, section.Title, type, node.Sensitivity, node.IsActive, node.IsRequired,
                node.IsProvided, node.IsActive && node.IsReady && !inputError,
                node.IsActive && (node.HasError || inputError), node.IsActive && !inputError && IsDeferredLeaf(form, node),
                form.AnswerOrigin(path), terms,
                schema.TryGetProperty("help", out JsonElement comment) ? comment.GetString() : null,
                instructions, binding.Artifacts, binding.Keys, Array.IndexOf(sections, section),
                node.IsActive ? [] : ActiveControllers(path)));
        }
        return new(progress, DefinitionDiscovery.Search(entries, form.DiscoverySelection), entries, sections,
            validation.Fields.Where(state => state.IsActive && form.IsEditorVisible([state.Id])).Select(state => state.Id),
            sectionProgress);

        IReadOnlyList<string> ActiveControllers(IReadOnlyList<string> path)
        {
            var controllers = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int length = 1; length <= path.Count; length++)
                if (form.TryMetadata(path.Take(length).ToArray(), out JsonElement metadata, out _))
                    Follow(metadata);
            return controllers.Order(StringComparer.Ordinal).ToArray();

            void Follow(JsonElement metadata)
            {
                if (!metadata.TryGetProperty("activeWhen", out JsonElement condition)) return;
                foreach (string fieldId in References(condition))
                {
                    if (!visited.Add(fieldId)) continue;
                    if (validation.Fields.Single(field => field.Id == fieldId).IsActive)
                        controllers.Add(fieldId);
                    else Follow(form.Definition.Fields.Single(field => field.Id == fieldId).Constraints);
                }
            }
        }
    }

    private static IEnumerable<string> References(JsonElement condition) =>
        condition.TryGetProperty("field", out JsonElement field) ? [field.GetString()!]
            : condition.GetProperty("operands").EnumerateArray().SelectMany(References);

    private static DefinitionFormProgress CountProgress(DefinitionFormModel form,
        IEnumerable<DefinitionNodeState> nodes, IEnumerable<DefinitionDiagnostic> diagnostics,
        IReadOnlyList<DefinitionDiagnostic> inputErrors)
    {
        DefinitionNodeState[] obligations = nodes.Where(node => node.IsObligation).ToArray();
        static string Key(DefinitionDiagnostic diagnostic) =>
            diagnostic.FieldId + JsonSerializer.Serialize(diagnostic.RelativePath);
        var invalidPaths = diagnostics.Where(diagnostic => diagnostic.Code != "value-required")
            .Concat(inputErrors).Select(Key).ToHashSet(StringComparer.Ordinal);
        bool HasInputError(DefinitionNodeState node) => inputErrors.Any(diagnostic =>
            diagnostic.FieldId == node.FieldId && diagnostic.RelativePath.SequenceEqual(node.RelativePath));
        return new(
            obligations.Count(node => node.IsObligationReady && !HasInputError(node)), obligations.Length,
            obligations.Count(node => node.IsMissing && !HasInputError(node)), invalidPaths.Count,
            obligations.Count(node => !HasInputError(node) && IsDeferredLeaf(form, node)));
    }

    private static bool IsDeferredLeaf(DefinitionFormModel form, DefinitionNodeState node) =>
        node.IsObligation && node.IsMissing && node.Sensitivity == SetupFieldSensitivity.Secret &&
        form.TryMetadata([node.FieldId, .. node.RelativePath], out JsonElement metadata, out _) &&
        metadata.GetProperty("type").GetString() is not ("object" or "list");

    private static void CollectBindings(JsonElement projection, string artifact, string[] keys,
        Dictionary<string, (HashSet<string> Artifacts, HashSet<string> Keys)> bindings)
    {
        if (projection.ValueKind != JsonValueKind.Object || !projection.TryGetProperty("op", out JsonElement operation)
            || operation.GetString() == "literal") return;
        if (projection.TryGetProperty("field", out JsonElement reference) &&
            reference.GetString() is { } fieldId && !fieldId.StartsWith('$'))
        {
            if (!bindings.TryGetValue(fieldId, out var binding))
            {
                binding = (new(StringComparer.Ordinal), new(StringComparer.Ordinal));
                bindings.Add(fieldId, binding);
            }
            binding.Artifacts.Add(artifact);
            binding.Keys.UnionWith(keys);
        }
        foreach (JsonProperty child in projection.EnumerateObject())
            if (child.Name == "members" && child.Value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty member in child.Value.EnumerateObject())
                    CollectBindings(member.Value, artifact, [.. keys, member.Name], bindings);
            else if (child.Value.ValueKind == JsonValueKind.Object)
                CollectBindings(child.Value, artifact, keys, bindings);
            else if (child.Value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in child.Value.EnumerateArray())
                    CollectBindings(item, artifact, keys, bindings);
    }
}
