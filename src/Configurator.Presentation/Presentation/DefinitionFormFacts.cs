namespace Configurator.Presentation;

using System.Collections.ObjectModel;
using Configurator.Core.Definitions;

/// <summary>One current input revision's private derived facts, without another answer tree.</summary>
internal sealed class DefinitionFormFacts
{
    private readonly DefinitionFormIndex _index;
    private readonly HashSet<string> _activeFields;
    private readonly Dictionary<string, string[]> _correctionPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _promotionPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requiredPaths = new(StringComparer.Ordinal);
    private readonly record struct EntryState(DefinitionFormIndex.Node Metadata, DefinitionFormSection Section,
        DefinitionNodeState Node, bool InputError, bool Deferred, DefinitionAnswerOrigin Origin,
        IReadOnlyList<string>? Artifacts, IReadOnlyList<string>? BindingKeys);

    internal DefinitionFormFacts(DefinitionFormModel form, DefinitionFormIndex index,
        DefinitionValidationResult validation)
    {
        _index = index;
        Validation = validation;
        _activeFields = validation.Fields.Where(state => state.IsActive).Select(state => state.Id)
            .ToHashSet(StringComparer.Ordinal);
        DefinitionFieldInput[] activeInputs = validation.Fields.Where(state => state.IsActive)
            .Select(state => form.GetFieldEditor(state.Id).Input).ToArray();
        InputDiagnostics = Array.AsReadOnly(activeInputs.SelectMany(field => field.InputDiagnostics).ToArray());
        HasInputErrors = activeInputs.Any(field => field.InputDiagnosticCode is not null);
        foreach (DefinitionNodeState node in validation.Nodes)
        {
            IReadOnlyList<string> path = node.RelativePath.Count == 0
                ? index.RootPaths[node.FieldId] : [node.FieldId, .. node.RelativePath];
            if (node.IsActive && (node.HasError || node.IsObligation && node.IsMissing))
                AddPrefixes(path, correction: true);
            if (node.IsActive && node.IsRequired) AddPrefixes(path, correction: false);
        }
        foreach (DefinitionDiagnostic diagnostic in validation.Diagnostics.Where(item => item.Code != "value-required"))
            AddPrefixes([diagnostic.FieldId, .. diagnostic.RelativePath], correction: true, promote: true);
        foreach (DefinitionDiagnostic diagnostic in InputDiagnostics)
            AddPrefixes([diagnostic.FieldId, .. diagnostic.RelativePath], correction: true, promote: true);
        Progress = CountProgress(validation.Nodes, validation.Diagnostics, InputDiagnostics);
        SectionProgress = new ReadOnlyDictionary<string, DefinitionFormProgress>(form.Sections.ToDictionary(
            section => section.Id, section => form.Sections.Count == 1 &&
                section.Fields.Count == index.FieldIds.Count ? Progress : CountProgress(
                validation.Nodes.Where(node => index.SectionsByField[node.FieldId].Id == section.Id),
                validation.Diagnostics.Where(diagnostic => index.SectionsByField.TryGetValue(diagnostic.FieldId, out var owner) &&
                    owner.Id == section.Id),
                InputDiagnostics.Where(diagnostic => index.SectionsByField.TryGetValue(diagnostic.FieldId, out var owner) &&
                    owner.Id == section.Id).ToArray()),
            StringComparer.Ordinal));
        var entries = new List<EntryState>(validation.Nodes.Count);
        foreach (DefinitionNodeState node in validation.Nodes)
        {
            IReadOnlyList<string> path = node.RelativePath.Count == 0
                ? index.RootPaths[node.FieldId] : [node.FieldId, .. node.RelativePath];
            if (!form.TryMetadataNode(path, out DefinitionFormIndex.Node metadata, out _, validation)) continue;
            DefinitionFormSection section = index.SectionsByField[node.FieldId];
            bool inputError = false;
            for (int diagnosticIndex = 0; diagnosticIndex < InputDiagnostics.Count; diagnosticIndex++)
            {
                DefinitionDiagnostic diagnostic = InputDiagnostics[diagnosticIndex];
                if (diagnostic.FieldId != node.FieldId ||
                    !diagnostic.RelativePath.Take(node.RelativePath.Count).SequenceEqual(node.RelativePath)) continue;
                inputError = true;
                break;
            }
            bool deferred = !inputError && IsDeferredLeaf(node);
            DefinitionAnswerOrigin origin = form.AnswerOrigin(path, validation);
            index.Bindings.TryGetValue(node.FieldId, out var binding);
            entries.Add(new(metadata, section, node, inputError, deferred, origin, binding.Artifacts, binding.Keys));
        }
        EntryState[] capturedEntries = entries.ToArray();
        Entries = CreateEntries(capturedEntries, index, _activeFields);
        RelevantPaths = CreateRelevantPaths(capturedEntries, index);

        bool IsDeferredLeaf(DefinitionNodeState node) =>
            node.IsObligation && node.IsMissing && node.Sensitivity == SetupFieldSensitivity.Secret &&
            form.TryMetadataNode([node.FieldId, .. node.RelativePath], out DefinitionFormIndex.Node metadata,
                out _, validation) &&
            metadata.Schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);

        DefinitionFormProgress CountProgress(IEnumerable<DefinitionNodeState> nodes,
            IEnumerable<DefinitionDiagnostic> diagnostics, IReadOnlyList<DefinitionDiagnostic> inputErrors)
        {
            string Key(DefinitionDiagnostic diagnostic) =>
                _index.PathKey([diagnostic.FieldId, .. diagnostic.RelativePath]);
            var invalidPaths = diagnostics.Where(diagnostic => diagnostic.Code != "value-required")
                .Concat(inputErrors).Select(Key).ToHashSet(StringComparer.Ordinal);
            bool HasInputError(DefinitionNodeState node)
            {
                for (int diagnosticIndex = 0; diagnosticIndex < inputErrors.Count; diagnosticIndex++)
                {
                    DefinitionDiagnostic diagnostic = inputErrors[diagnosticIndex];
                    if (diagnostic.FieldId == node.FieldId &&
                        diagnostic.RelativePath.SequenceEqual(node.RelativePath)) return true;
                }
                return false;
            }
            int ready = 0, required = 0, missing = 0, deferred = 0;
            foreach (DefinitionNodeState node in nodes)
            {
                if (!node.IsObligation) continue;
                required++;
                if (HasInputError(node)) continue;
                if (node.IsObligationReady) ready++;
                if (node.IsMissing) missing++;
                if (IsDeferredLeaf(node)) deferred++;
            }
            return new(ready, required, missing, invalidPaths.Count, deferred);
        }

    }

    private static Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> CreateEntries(EntryState[] entries,
        DefinitionFormIndex index, IReadOnlySet<string> activeFields) =>
        new(() => Array.AsReadOnly(entries.Select(entry => new DefinitionDiscoveryEntry(
            entry.Metadata, entry.Section, entry.Node, entry.InputError, entry.Deferred, entry.Origin,
            entry.Artifacts, entry.BindingKeys, entry.Node.IsActive ? [] :
                ActiveControllers(entry.Metadata, index, activeFields))).ToArray()));

    private static Lazy<IReadOnlySet<string>> CreateRelevantPaths(EntryState[] entries, DefinitionFormIndex index) =>
        new(() =>
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (EntryState entry in entries)
            {
                DefinitionNodeState node = entry.Node;
                if (!node.IsActive || !(node.IsRequired || node.HasError || entry.InputError || entry.Deferred ||
                    node.IsProvided || entry.Origin == DefinitionAnswerOrigin.Defaulted)) continue;
                for (int length = 0; length <= node.RelativePath.Count; length++)
                    paths.Add(index.PathKey([node.FieldId, .. node.RelativePath.Take(length)]));
            }
            return paths;
        });

    private static IReadOnlyList<string> ActiveControllers(DefinitionFormIndex.Node node,
        DefinitionFormIndex index, IReadOnlySet<string> activeFields)
    {
        if (node.Parent is null && node.Controllers.All(activeFields.Contains)) return node.Controllers;
        var controllers = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (DefinitionFormIndex.Node? metadata = node; metadata is not null; metadata = metadata.Parent)
            Follow(metadata);
        return Array.AsReadOnly(controllers.Order(StringComparer.Ordinal).ToArray());

        void Follow(DefinitionFormIndex.Node metadata)
        {
            foreach (string fieldId in metadata.Controllers)
            {
                if (!visited.Add(fieldId)) continue;
                if (activeFields.Contains(fieldId)) controllers.Add(fieldId);
                else Follow(index.Roots[fieldId]);
            }
        }
    }

    internal DefinitionValidationResult Validation { get; }
    internal IReadOnlyList<DefinitionDiagnostic> InputDiagnostics { get; }
    internal bool HasInputErrors { get; }
    internal DefinitionFormProgress Progress { get; }
    internal IReadOnlyDictionary<string, DefinitionFormProgress> SectionProgress { get; }
    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> Entries { get; }
    internal Lazy<IReadOnlySet<string>> RelevantPaths { get; }
    internal IEnumerable<string[]> CorrectionPaths => _correctionPaths.Values;
    internal IEnumerable<string[]> PromotionPaths => _promotionPaths.Values;
    internal bool IsActive(string fieldId) => _activeFields.Contains(fieldId);
    internal bool NeedsCorrection(IReadOnlyList<string> path) =>
        _correctionPaths.ContainsKey(_index.PathKey(path));
    internal bool HasRequiredDescendant(IReadOnlyList<string> path) =>
        _requiredPaths.Contains(_index.PathKey(path));
    private void AddPrefixes(IReadOnlyList<string> path, bool correction, bool promote = false)
    {
        for (int length = 1; length <= path.Count; length++)
        {
            string[] prefix = path.Take(length).ToArray();
            string key = _index.PathKey(prefix);
            if (correction) _correctionPaths.TryAdd(key, prefix);
            else _requiredPaths.Add(key);
            if (promote) _promotionPaths.TryAdd(key, prefix);
        }
    }
}
