namespace Configurator.Presentation;

using System.Collections.ObjectModel;
using Configurator.Core.Definitions;

/// <summary>One current input revision's private derived facts, without another answer tree.</summary>
internal sealed class DefinitionFormFacts
{
    private readonly Dictionary<DefinitionPath, DefinitionNodeState> _nodes = [];
    private readonly DefinitionFormIndex _index;
    private readonly HashSet<string> _activeFields;
    private readonly HashSet<DefinitionPath> _correctionPaths = [];
    private readonly HashSet<DefinitionPath> _promotionPaths = [];
    private readonly HashSet<DefinitionPath> _requiredPaths = [];
    private readonly Dictionary<DefinitionPath, IReadOnlyList<DefinitionFieldCorrectionReason>> _correctionReasons = [];
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
        DefinitionFieldInput[] activeInputs = validation.Fields.Where(state => state.IsActive && index.FieldPositions.ContainsKey(state.Id))
            .Select(state => form.GetFieldEditor(state.Id).Input).ToArray();
        IEnumerable<DefinitionDiagnostic> inputDiagnostics = activeInputs.SelectMany(field => field.InputDiagnostics);
        if (validation.ConfigurationContext is { } context)
        {
            var relevant = context.RelevantLocations.ToHashSet();
            inputDiagnostics = inputDiagnostics.Where(diagnostic => relevant.Contains(DefinitionPath.FromSegments([
                form.Fields[index.FieldPositions[diagnostic.FieldId]].Definition.Name, .. diagnostic.RelativePath])));
        }
        InputDiagnostics = Array.AsReadOnly(inputDiagnostics.Concat(form.RootInputDiagnostics()).ToArray());
        HasInputErrors = InputDiagnostics.Count > 0;
        var resolvedNodes = new List<(DefinitionNodeState Node, IReadOnlyList<string> Path)>(validation.Nodes.Count);
        foreach (DefinitionNodeState node in validation.Nodes)
        {
            if (!form.TryCorrectionPath(node.FieldId, node.Location, validation, out IReadOnlyList<string> path)) continue;
            resolvedNodes.Add((node, path));
            DefinitionPath literal = node.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path);
            _nodes[literal] = node;
            if (node.IsActive && (node.HasError || node.IsObligation && node.IsMissing))
                AddPrefixes(literal, correction: true);
            if (node.IsActive && node.IsRequired) AddPrefixes(literal, correction: false);
        }
        foreach (DefinitionDiagnostic diagnostic in validation.Diagnostics.Where(item => item.Code != "value-required"))
            if (diagnostic.Location is { } location &&
                form.TryCorrectionPath(diagnostic.FieldId, location, validation, out IReadOnlyList<string> path))
                AddPrefixes(diagnostic.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path),
                    correction: true, promote: true);
        foreach (DefinitionDiagnostic diagnostic in InputDiagnostics)
            if (diagnostic.FieldId.Length > 0)
                AddPrefixes(form.DataPath([diagnostic.FieldId, .. diagnostic.RelativePath]), correction: true, promote: true);
            else if (diagnostic.Location is { } location &&
                form.TryCorrectionPath("", location, validation, out IReadOnlyList<string> path))
                AddPrefixes(DefinitionPath.FromSegments(path), correction: true, promote: true);
        var correctionReasons = new Dictionary<DefinitionPath, List<DefinitionFieldCorrectionReason>>();
        foreach (DefinitionDiagnostic diagnostic in InputDiagnostics.Concat(validation.Diagnostics))
        {
            DefinitionPath path;
            if (diagnostic.Location is { } location &&
                form.TryCorrectionPath(diagnostic.FieldId, location, validation, out var resolved))
                path = diagnostic.FieldId.Length == 0 ? DefinitionPath.FromSegments(resolved) : form.DataPath(resolved);
            else if (diagnostic.Location is null && diagnostic.FieldId.Length == 0 && diagnostic.RelativePath.Count == 0)
                path = DefinitionPath.FromSegments([]);
            else continue;
            if (!correctionReasons.TryGetValue(path, out List<DefinitionFieldCorrectionReason>? reasons))
                correctionReasons.Add(path, reasons = []);
            reasons.Add(diagnostic.Code switch
            {
                "value-required" => DefinitionFieldCorrectionReason.Required,
                "input-type-invalid" or "value-type-invalid" => DefinitionFieldCorrectionReason.Type,
                "value-duplicate" => DefinitionFieldCorrectionReason.Duplicate,
                _ => DefinitionFieldCorrectionReason.Constraint
            });
        }
        foreach (var (path, reasons) in correctionReasons)
            _correctionReasons.Add(path, Array.AsReadOnly(reasons.Distinct().ToArray()));
        Progress = CountProgress(validation.Nodes, validation.Diagnostics, InputDiagnostics);
        SectionProgress = new ReadOnlyDictionary<string, DefinitionFormProgress>(form.Sections.ToDictionary(
            section => section.Id, section => form.Sections.Count == 1 &&
                section.Fields.Count == index.FieldIds.Count ? Progress : CountProgress(
                validation.Nodes.Where(node => index.SectionsByField.TryGetValue(node.FieldId, out var owner) &&
                    owner.Id == section.Id),
                validation.Diagnostics.Where(diagnostic => index.SectionsByField.TryGetValue(diagnostic.FieldId, out var owner) &&
                    owner.Id == section.Id),
                InputDiagnostics.Where(diagnostic => index.SectionsByField.TryGetValue(diagnostic.FieldId, out var owner) &&
                    owner.Id == section.Id).ToArray()),
            StringComparer.Ordinal));
        var entries = new List<EntryState>(validation.Nodes.Count);
        foreach (var (node, path) in resolvedNodes)
        {
            DefinitionFormIndex.Node metadata;
            if (!(node.FieldId.Length == 0 ? form.TryRootMetadata(path, out metadata, out _, validation)
                : form.TryMetadataNode(path, out metadata, out _, validation))) continue;
            DefinitionFormSection section = index.SectionsByField.GetValueOrDefault(node.FieldId) ?? form.Sections[0];
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
            DefinitionAnswerOrigin origin = node.FieldId.Length == 0 ? form.RootAnswerOrigin(path, validation)
                : form.AnswerOrigin(path, validation);
            index.Bindings.TryGetValue(node.FieldId, out var binding);
            entries.Add(new(metadata, section, node, inputError, deferred, origin, binding.Artifacts, binding.Keys));
        }
        EntryState[] capturedEntries = entries.ToArray();
        Entries = CreateEntries(capturedEntries, index, _activeFields);
        RelevantPaths = CreateRelevantPaths(capturedEntries, index);

        bool IsDeferredLeaf(DefinitionNodeState node) =>
            node.IsObligation && node.IsMissing && node.Sensitivity == FieldSensitivity.Secret &&
            form.TryCorrectionPath(node.FieldId, node.Location, validation, out IReadOnlyList<string> path) &&
            form.TryRootMetadata((node.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path)).Segments,
                out DefinitionFormIndex.Node metadata, out _, validation) &&
            metadata.Schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);

        DefinitionFormProgress CountProgress(IEnumerable<DefinitionNodeState> nodes,
            IEnumerable<DefinitionDiagnostic> diagnostics, IReadOnlyList<DefinitionDiagnostic> inputErrors)
        {
            DefinitionPath Key(DefinitionDiagnostic diagnostic) =>
                diagnostic.Location is { } location &&
                form.TryCorrectionPath(diagnostic.FieldId, location, validation, out IReadOnlyList<string> path)
                    ? diagnostic.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path)
                    : form.DataPath([diagnostic.FieldId, .. diagnostic.RelativePath]);
            var invalidPaths = diagnostics.Where(diagnostic => diagnostic.Code != "value-required")
                .Concat(inputErrors).Select(Key).ToHashSet();
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

    private static Lazy<IReadOnlySet<DefinitionPath>> CreateRelevantPaths(EntryState[] entries, DefinitionFormIndex index) =>
        new(() =>
        {
            var paths = new HashSet<DefinitionPath>();
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
    internal Lazy<IReadOnlySet<DefinitionPath>> RelevantPaths { get; }
    internal IEnumerable<DefinitionPath> CorrectionPaths => _correctionPaths;
    internal IEnumerable<DefinitionPath> PromotionPaths => _promotionPaths;
    internal bool IsActive(string fieldId) => _activeFields.Contains(fieldId);
    internal DefinitionNodeState? EditorState(IReadOnlyList<string> path) => _nodes.GetValueOrDefault(_index.DataPath(path));
    internal DefinitionNodeState? EditorState(DefinitionPath path) => _nodes.GetValueOrDefault(path);
    internal IReadOnlyList<DefinitionFieldCorrectionReason> CorrectionReasons(DefinitionPath path) =>
        _correctionReasons.GetValueOrDefault(path) ?? [];
    internal bool NeedsCorrection(IReadOnlyList<string> path) =>
        NeedsCorrection(_index.DataPath(path));
    internal bool NeedsCorrection(DefinitionPath path) => _correctionPaths.Contains(path);
    internal bool HasRequiredDescendant(IReadOnlyList<string> path) =>
        HasRequiredDescendant(_index.DataPath(path));
    internal bool HasRequiredDescendant(DefinitionPath path) => _requiredPaths.Contains(path);
    private void AddPrefixes(DefinitionPath path, bool correction, bool promote = false)
    {
        for (int length = 0; length <= path.Segments.Count; length++)
        {
            DefinitionPath key = DefinitionPath.FromSegments(path.Segments.Take(length));
            if (correction) _correctionPaths.Add(key);
            else _requiredPaths.Add(key);
            if (promote) _promotionPaths.Add(key);
        }
    }
}
