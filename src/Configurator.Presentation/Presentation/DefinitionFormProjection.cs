namespace Configurator.Presentation;

using System.Text.Json;
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
        IEnumerable<DefinitionFormSection> sections, IEnumerable<string> visibleFields)
    {
        Progress = progress;
        Discovery = discovery;
        Entries = Array.AsReadOnly(entries.ToArray());
        Sections = Array.AsReadOnly(sections.ToArray());
        _visibleFields = visibleFields.ToHashSet(StringComparer.Ordinal);
    }

    public DefinitionFormProgress Progress { get; }
    public DefinitionDiscoverySnapshot Discovery { get; }
    public IReadOnlyList<DefinitionDiscoveryMatch> Matches => Discovery.Matches;
    public IReadOnlyList<DefinitionDiscoveryEntry> Entries { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public bool IsFieldVisible(string fieldId) => _visibleFields.Contains(fieldId);

    internal static DefinitionFormProjection Create(DefinitionFormModel form)
    {
        DefinitionValidationResult validation = form.Validate();
        DefinitionNodeState[] obligations = validation.Nodes.Where(node => node.IsObligation).ToArray();
        IReadOnlyList<DefinitionDiagnostic> inputErrors = form.InputDiagnostics;
        static string Key(DefinitionDiagnostic diagnostic) =>
            diagnostic.FieldId + JsonSerializer.Serialize(diagnostic.RelativePath);
        var invalidPaths = validation.Diagnostics.Where(diagnostic => diagnostic.Code != "value-required")
            .Concat(inputErrors).Select(Key).ToHashSet(StringComparer.Ordinal);
        bool HasInputError(DefinitionNodeState node) => inputErrors.Any(diagnostic =>
            diagnostic.FieldId == node.FieldId && diagnostic.RelativePath.SequenceEqual(node.RelativePath));
        var progress = new DefinitionFormProgress(
            obligations.Count(node => node.IsObligationReady && !HasInputError(node)),
            obligations.Length,
            obligations.Count(node => node.IsMissing && !HasInputError(node)),
            invalidPaths.Count,
            obligations.Count(node => node.Sensitivity == SetupFieldSensitivity.Secret && node.IsMissing));
        DefinitionFormExperience experience = form.Experiences.Single(item => item.Id == form.SelectedExperienceId);
        DefinitionFormSection[] sections = form.Sections.OrderBy(section =>
            experience.Sections.Contains(section.Id) ? experience.Sections.ToList().IndexOf(section.Id) : int.MaxValue).ToArray();
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
                node.IsActive && (node.HasError || inputError), node.IsActive &&
                node.IsObligation && node.Sensitivity == SetupFieldSensitivity.Secret && node.IsMissing,
                form.AnswerOrigin(path), terms,
                schema.TryGetProperty("help", out JsonElement comment) ? comment.GetString() : null,
                instructions, binding.Artifacts, binding.Keys, Array.IndexOf(sections, section)));
        }
        return new(progress, DefinitionDiscovery.Search(entries, form.DiscoverySelection), entries, sections,
            validation.Fields.Where(state => state.IsActive && form.IsEditorVisible([state.Id])).Select(state => state.Id));
    }

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
