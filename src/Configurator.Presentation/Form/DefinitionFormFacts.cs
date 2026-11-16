namespace Configurator.Presentation;

using System.Collections.ObjectModel;
using Configurator.Core.Definitions;

/// <summary>One current input revision's private derived facts, without another answer tree.</summary>
internal sealed class DefinitionFormFacts
{
    private static readonly DefinitionPath s_emptyPath = DefinitionPath.FromSegments([]);
    private static readonly IReadOnlyList<DefinitionFieldCorrectionReason> s_requiredReason = [DefinitionFieldCorrectionReason.Required];
    private static readonly IReadOnlyList<DefinitionFieldCorrectionReason> s_typeReason = [DefinitionFieldCorrectionReason.Type];
    private static readonly IReadOnlyList<DefinitionFieldCorrectionReason> s_duplicateReason = [DefinitionFieldCorrectionReason.Duplicate];
    private static readonly IReadOnlyList<DefinitionFieldCorrectionReason> s_constraintReason = [DefinitionFieldCorrectionReason.Constraint];
    private readonly Dictionary<DefinitionPath, DefinitionNodeState> _nodes;
    private readonly DefinitionFormIndex _index;
    private readonly HashSet<string> _activeFields;
    private readonly HashSet<DefinitionPath> _correctionPaths = [];
    private readonly HashSet<DefinitionPath> _promotionPaths = [];
    private readonly HashSet<DefinitionPath> _requiredPaths = [];
    private readonly record struct EntryState(DefinitionFormIndex.Node Metadata, DefinitionFormSection Section,
        DefinitionNodeState Node, DefinitionPath LiteralPath, bool InputError, bool Deferred, DefinitionAnswerOrigin Origin,
        IReadOnlyList<string>? Artifacts, IReadOnlyList<string>? BindingKeys);

    internal DefinitionFormFacts(DefinitionFormModel form, DefinitionFormIndex index,
        DefinitionValidationResult validation)
    {
        _index = index;
        Validation = validation;
        _nodes = new Dictionary<DefinitionPath, DefinitionNodeState>(validation.Nodes.Count);
        _activeFields = new HashSet<string>(validation.Fields.Count, StringComparer.Ordinal);
        List<DefinitionDiagnostic>? activeDiagnostics = null;
        foreach (FieldState fieldState in validation.Fields)
        {
            if (!fieldState.IsActive) continue;
            _activeFields.Add(fieldState.Id);
            if (index.FieldPositions.ContainsKey(fieldState.Id))
            {
                foreach (DefinitionDiagnostic diag in form.GetFieldEditor(fieldState.Id).Input.InputDiagnostics)
                    (activeDiagnostics ??= []).Add(diag);
            }
        }
        IEnumerable<DefinitionDiagnostic> inputDiagnostics = activeDiagnostics ?? Enumerable.Empty<DefinitionDiagnostic>();
        if (validation.ConfigurationContext is { } context && activeDiagnostics is not null)
        {
            var relevant = context.RelevantLocations.ToHashSet();
            inputDiagnostics = inputDiagnostics.Where(diagnostic => relevant.Contains(DefinitionPath.FromSegments([
                form.Fields[index.FieldPositions[diagnostic.FieldId]].Definition.Name, .. diagnostic.RelativePath])));
        }
        List<DefinitionDiagnostic>? merged = null;
        foreach (DefinitionDiagnostic diag in inputDiagnostics)
            (merged ??= []).Add(diag);
        foreach (DefinitionDiagnostic diag in form.RootInputDiagnostics())
            (merged ??= []).Add(diag);
        InputDiagnostics = merged is null ? [] : Array.AsReadOnly(merged.ToArray());
        HasInputErrors = InputDiagnostics.Count > 0;

        bool singleFastPath = form.Sections.Count == 1 && index.SectionsByPointer.Count <= 1;
        DefinitionFormSection? singleSection = form.Sections.Count > 0 ? form.Sections[0] : null;
        var totalBucket = new ProgressAccumulator();
        Dictionary<string, ProgressAccumulator>? sectionBuckets = singleFastPath
            ? null
            : new Dictionary<string, ProgressAccumulator>(form.Sections.Count, StringComparer.Ordinal);
        if (sectionBuckets is not null)
        {
            foreach (DefinitionFormSection section in form.Sections)
                sectionBuckets[section.Id] = new ProgressAccumulator();
        }

        Dictionary<string, List<(DefinitionDiagnostic Diagnostic, string? SectionId)>>? inputByField = null;
        if (HasInputErrors)
        {
            inputByField = new Dictionary<string, List<(DefinitionDiagnostic, string?)>>(StringComparer.Ordinal);
            foreach (DefinitionDiagnostic diagnostic in InputDiagnostics)
            {
                DefinitionPath literal = DiagnosticLiteralPath(diagnostic);
                AddPrefixes(literal, correction: true, promote: true);
                AddCorrectionReason(literal, diagnostic);
                string? diagSectionId = singleFastPath ? singleSection?.Id : index.ResolveSection(literal)?.Id;
                totalBucket.AddInvalidPath(literal);
                if (diagSectionId is not null && sectionBuckets is not null &&
                    sectionBuckets.TryGetValue(diagSectionId, out ProgressAccumulator? diagBucket))
                    diagBucket.AddInvalidPath(literal);
                if (!inputByField.TryGetValue(diagnostic.FieldId, out var bucket))
                    inputByField.Add(diagnostic.FieldId, bucket = []);
                bucket.Add((diagnostic, diagSectionId));
            }
        }

        var sectionActivePaths = new Dictionary<string, HashSet<DefinitionPath>>(form.Sections.Count, StringComparer.Ordinal);
        var sectionSuppliedOptional = new Dictionary<string, int>(form.Sections.Count, StringComparer.Ordinal);
        var sectionActiveCount = new Dictionary<string, int>(form.Sections.Count, StringComparer.Ordinal);
        foreach (DefinitionFormSection section in form.Sections)
        {
            sectionActivePaths[section.Id] = [];
            sectionSuppliedOptional[section.Id] = 0;
            sectionActiveCount[section.Id] = 0;
        }

        bool hasDeployment = form.Definition.Deployment is not null;
        var entries = new List<EntryState>(validation.Nodes.Count);
        foreach (DefinitionNodeState node in validation.Nodes)
        {
            IReadOnlyList<string> path;
            DefinitionPath literal;
            bool isRootNode = node.FieldId.Length > 0 && node.Location.Segments.Count == 0;
            if (isRootNode &&
                index.TryGetRootDataSegments(node.FieldId, out IReadOnlyList<string>? cachedSegments) &&
                index.TryGetRootDataPath(node.FieldId, out DefinitionPath? cachedPath))
            {
                path = cachedSegments;
                literal = cachedPath;
            }
            else
            {
                bool hasResolved = form.TryCorrectionPath(node.FieldId, node.Location, validation, out path);
                if (!hasResolved)
                {
                    if (node.IsObligation)
                    {
                        DefinitionPath fallbackLiteral = NodeFallbackLiteralPath(node);
                        DefinitionFormSection? fallbackSection = singleFastPath ? singleSection : index.ResolveSection(fallbackLiteral);
                        bool hasErr = HasExactInputError(node.FieldId, node.RelativePath, fallbackSection?.Id, inputByField);
                        bool deferredObl = !hasErr && IsDeferredLeafPath(node, fallbackLiteral.Segments, validation);
                        totalBucket.AddObligation(node, hasErr, deferredObl);
                        if (fallbackSection is not null && sectionBuckets is not null &&
                            sectionBuckets.TryGetValue(fallbackSection.Id, out ProgressAccumulator? fallbackBucket))
                            fallbackBucket.AddObligation(node, hasErr, deferredObl);
                    }
                    continue;
                }

                literal = node.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path);
            }

            _nodes[literal] = node;
            if (node.IsActive && (node.HasError || node.IsObligation && node.IsMissing))
                AddPrefixes(literal, correction: true);
            if (node.IsActive && node.IsRequired)
                AddPrefixes(literal, correction: false);

            DefinitionFormIndex.Node metadata;
            if (isRootNode && index.Roots.TryGetValue(node.FieldId, out DefinitionFormIndex.Node? rootMeta))
            {
                metadata = rootMeta;
            }
            else if (!(node.FieldId.Length == 0 ? form.TryRootMetadata(path, out metadata, out _, validation)
                : form.TryMetadataNode(path, out metadata, out _, validation)))
            {
                if (node.IsObligation)
                {
                    DefinitionFormSection? unresolvedMetaSection = singleFastPath ? singleSection : index.ResolveSection(literal);
                    bool hasErr = HasExactInputError(node.FieldId, node.RelativePath, unresolvedMetaSection?.Id, inputByField);
                    bool deferredObl = !hasErr && IsDeferredLeafPath(node, literal.Segments, validation);
                    totalBucket.AddObligation(node, hasErr, deferredObl);
                    if (unresolvedMetaSection is not null && sectionBuckets is not null &&
                        sectionBuckets.TryGetValue(unresolvedMetaSection.Id, out ProgressAccumulator? bucket))
                        bucket.AddObligation(node, hasErr, deferredObl);
                }
                continue;
            }

            DefinitionFormSection section = singleFastPath && singleSection is not null
                ? singleSection
                : index.ResolveSection(literal) ?? form.Sections[0];
            bool inputError = HasPrefixInputError(node.FieldId, node.RelativePath, inputByField);
            bool deferred = !inputError && IsDeferredLeaf(node, literal, metadata);
            DefinitionAnswerOrigin origin = isRootNode
                ? form.RootFieldAnswerOrigin(node.FieldId, literal, inputError, validation)
                : node.FieldId.Length == 0
                    ? form.RootAnswerOrigin(path, validation)
                    : form.AnswerOrigin(path, validation);
            if (node.IsActive && !(hasDeployment && node.FieldId.Length > 0 && form.IsConfigurationSelectorRoot(node.FieldId, validation)))
            {
                sectionActivePaths[section.Id].Add(literal);
                sectionActiveCount[section.Id]++;
                if (!node.IsObligation && (node.IsProvided ||
                    origin is DefinitionAnswerOrigin.Explicit or DefinitionAnswerOrigin.Imported or DefinitionAnswerOrigin.Generated))
                {
                    sectionSuppliedOptional[section.Id]++;
                }
            }
            if (node.IsObligation)
            {
                bool exactInputError = HasExactInputError(node.FieldId, node.RelativePath, section.Id, inputByField);
                bool obligationDeferred = exactInputError ? false : deferred;
                totalBucket.AddObligation(node, exactInputError, obligationDeferred);
                if (sectionBuckets is not null && sectionBuckets.TryGetValue(section.Id, out ProgressAccumulator? secBucket))
                    secBucket.AddObligation(node, exactInputError, obligationDeferred);
            }
            index.Bindings.TryGetValue(node.FieldId, out var binding);
            entries.Add(new(metadata, section, node, literal, inputError, deferred, origin, binding.Artifacts, binding.Keys));
        }

        if (validation.Diagnostics.Count > 0)
        {
            foreach (DefinitionDiagnostic diagnostic in validation.Diagnostics)
            {
                DefinitionPath? resolvedLiteral = null;
                if (diagnostic.FieldId.Length > 0 && diagnostic.Location is { Segments.Count: 0 } &&
                    index.TryGetRootDataPath(diagnostic.FieldId, out DefinitionPath? fastLiteral))
                {
                    resolvedLiteral = fastLiteral;
                }
                else
                {
                    bool hasLocation = diagnostic.Location is { } location;
                    IReadOnlyList<string>? resolved = null;
                    bool hasResolved = hasLocation && form.TryCorrectionPath(diagnostic.FieldId, diagnostic.Location!, validation, out resolved);
                    if (hasResolved)
                    {
                        resolvedLiteral = diagnostic.FieldId.Length == 0
                            ? DefinitionPath.FromSegments(resolved!)
                            : form.DataPath(resolved!);
                    }
                }

                if (diagnostic.Code != "value-required" && resolvedLiteral is not null)
                    AddPrefixes(resolvedLiteral, correction: true, promote: true);

                if (resolvedLiteral is not null)
                    AddCorrectionReason(resolvedLiteral, diagnostic);
                else if (diagnostic.Location is null && diagnostic.FieldId.Length == 0 && diagnostic.RelativePath.Count == 0)
                    AddCorrectionReason(s_emptyPath, diagnostic);

                if (diagnostic.Code != "value-required")
                {
                    DefinitionPath progressKey = resolvedLiteral ?? DiagnosticFallbackLiteralPath(diagnostic);
                    totalBucket.AddInvalidPath(progressKey);
                    if (!singleFastPath && index.ResolveSection(progressKey) is { } diagSection &&
                        sectionBuckets!.TryGetValue(diagSection.Id, out ProgressAccumulator? diagBucket))
                        diagBucket.AddInvalidPath(progressKey);
                }
            }
        }

        Progress = totalBucket.ToProgress();
        OptionalInvalidCount = CountOptionalInvalidPaths(totalBucket.InvalidPaths);
        if (singleFastPath)
        {
            var singleMap = new Dictionary<string, DefinitionFormProgress>(form.Sections.Count, StringComparer.Ordinal);
            foreach (DefinitionFormSection section in form.Sections)
                singleMap[section.Id] = Progress;
            SectionProgress = new ReadOnlyDictionary<string, DefinitionFormProgress>(singleMap);
        }
        else
        {
            var sectionProgress = new Dictionary<string, DefinitionFormProgress>(form.Sections.Count, StringComparer.Ordinal);
            foreach (DefinitionFormSection section in form.Sections)
                sectionProgress[section.Id] = sectionBuckets![section.Id].ToProgress();
            SectionProgress = new ReadOnlyDictionary<string, DefinitionFormProgress>(sectionProgress);
        }

        SectionActivePaths = sectionActivePaths;
        SectionActivity = form.Sections.ToDictionary(
            section => section.Id,
            section => (sectionActiveCount[section.Id], sectionSuppliedOptional[section.Id]),
            StringComparer.Ordinal);
        EntryState[] capturedEntries = entries.ToArray();
        var entriesWithPaths = CreateEntries(capturedEntries, index, _activeFields);
        Entries = new(() => entriesWithPaths.Value.Entries);
        EntryLiteralPaths = new(() => entriesWithPaths.Value.LiteralPaths);
        RelevantPaths = CreateRelevantPaths(capturedEntries, index);

        void AddCorrectionReason(DefinitionPath path, DefinitionDiagnostic diagnostic)
        {
            DefinitionFieldCorrectionReason reason = diagnostic.Code switch
            {
                "value-required" => DefinitionFieldCorrectionReason.Required,
                "input-type-invalid" or "value-type-invalid" => DefinitionFieldCorrectionReason.Type,
                "value-duplicate" => DefinitionFieldCorrectionReason.Duplicate,
                _ => DefinitionFieldCorrectionReason.Constraint
            };
            if (!_correctionReasonsBuilder.TryGetValue(path, out IReadOnlyList<DefinitionFieldCorrectionReason>? existing))
            {
                _correctionReasonsBuilder.Add(path, reason switch
                {
                    DefinitionFieldCorrectionReason.Required => s_requiredReason,
                    DefinitionFieldCorrectionReason.Type => s_typeReason,
                    DefinitionFieldCorrectionReason.Duplicate => s_duplicateReason,
                    _ => s_constraintReason
                });
                return;
            }
            if (existing is List<DefinitionFieldCorrectionReason> mutable)
            {
                if (!mutable.Contains(reason)) mutable.Add(reason);
                return;
            }
            if (existing[0] == reason) return;
            _correctionReasonsBuilder[path] = new List<DefinitionFieldCorrectionReason>(2) { existing[0], reason };
        }

        DefinitionPath NodeFallbackLiteralPath(DefinitionNodeState node) =>
            form.DataPath(PrependSegment(node.FieldId, node.RelativePath));

        DefinitionPath DiagnosticFallbackLiteralPath(DefinitionDiagnostic diagnostic) =>
            form.DataPath(PrependSegment(diagnostic.FieldId, diagnostic.RelativePath));

        DefinitionPath DiagnosticLiteralPath(DefinitionDiagnostic diagnostic) =>
            diagnostic.Location is { } location && form.TryCorrectionPath(diagnostic.FieldId, location, validation, out IReadOnlyList<string> resolved)
                ? diagnostic.FieldId.Length == 0 ? DefinitionPath.FromSegments(resolved) : form.DataPath(resolved)
                : DiagnosticFallbackLiteralPath(diagnostic);

        static bool IsDeferredLeaf(DefinitionNodeState node, DefinitionPath literal, DefinitionFormIndex.Node metadata) =>
            node.IsObligation && node.IsMissing && node.Sensitivity == FieldSensitivity.Secret &&
            metadata.Schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);

        bool IsDeferredLeafPath(DefinitionNodeState node, IReadOnlyList<string> literalSegments, DefinitionValidationResult v) =>
            node.IsObligation && node.IsMissing && node.Sensitivity == FieldSensitivity.Secret &&
            form.TryRootMetadata(literalSegments, out DefinitionFormIndex.Node meta, out _, v) &&
            meta.Schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);

        static bool HasPrefixInputError(string fieldId, IReadOnlyList<string> relativePath,
            Dictionary<string, List<(DefinitionDiagnostic Diagnostic, string? SectionId)>>? map)
        {
            if (map is null || !map.TryGetValue(fieldId, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                IReadOnlyList<string> diagRel = list[i].Diagnostic.RelativePath;
                if (diagRel.Count < relativePath.Count) continue;
                bool match = true;
                for (int j = 0; j < relativePath.Count; j++)
                {
                    if (!string.Equals(diagRel[j], relativePath[j], StringComparison.Ordinal))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        bool HasExactInputError(string fieldId, IReadOnlyList<string> relativePath, string? expectedSectionId,
            Dictionary<string, List<(DefinitionDiagnostic Diagnostic, string? SectionId)>>? map)
        {
            if (map is null || !map.TryGetValue(fieldId, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var (diag, secId) = list[i];
                if (!singleFastPath && !string.Equals(secId, expectedSectionId, StringComparison.Ordinal)) continue;
                if (diag.RelativePath.Count != relativePath.Count) continue;
                bool match = true;
                for (int j = 0; j < relativePath.Count; j++)
                {
                    if (!string.Equals(diag.RelativePath[j], relativePath[j], StringComparison.Ordinal))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
    }

    private readonly Dictionary<DefinitionPath, IReadOnlyList<DefinitionFieldCorrectionReason>> _correctionReasonsBuilder = [];
    private static string[] PrependSegment(string first, IReadOnlyList<string> rest)
    {
        if (rest.Count == 0) return [first];
        var result = new string[rest.Count + 1];
        result[0] = first;
        for (int i = 0; i < rest.Count; i++) result[i + 1] = rest[i];
        return result;
    }

    private sealed class ProgressAccumulator
    {
        private int _ready;
        private int _required;
        private int _missing;
        private int _deferred;
        private HashSet<DefinitionPath>? _invalidPaths;

        public void AddObligation(DefinitionNodeState node, bool hasInputError, bool isDeferredLeaf)
        {
            _required++;
            if (hasInputError) return;
            if (node.IsObligationReady) _ready++;
            if (node.IsMissing) _missing++;
            if (isDeferredLeaf) _deferred++;
        }

        public void AddInvalidPath(DefinitionPath path) => (_invalidPaths ??= []).Add(path);

        public IEnumerable<DefinitionPath> InvalidPaths => _invalidPaths ?? Enumerable.Empty<DefinitionPath>();

        public DefinitionFormProgress ToProgress() =>
            new(_ready, _required, _missing, _invalidPaths?.Count ?? 0, _deferred);
    }

    private int CountOptionalInvalidPaths(IEnumerable<DefinitionPath> invalidPaths)
    {
        int count = 0;
        foreach (DefinitionPath path in invalidPaths)
        {
            if (IsOptionalInvalidPath(path))
                count++;
        }
        return count;
    }

    private bool IsOptionalInvalidPath(DefinitionPath path)
    {
        if (_nodes.TryGetValue(path, out DefinitionNodeState? state))
            return !state.IsObligation && !state.IsRequired;
        for (int length = path.Segments.Count - 1; length >= 1; length--)
        {
            var slice = new string[length];
            for (int i = 0; i < length; i++) slice[i] = path.Segments[i];
            if (_nodes.TryGetValue(DefinitionPath.FromSegments(slice), out DefinitionNodeState? ancestor))
                return !ancestor.IsObligation && !ancestor.IsRequired;
        }
        return false;
    }

    private static Lazy<(IReadOnlyList<DefinitionDiscoveryEntry> Entries, IReadOnlyDictionary<string, DefinitionPath> LiteralPaths)> CreateEntries(
        EntryState[] entries,
        DefinitionFormIndex index, IReadOnlySet<string> activeFields) =>
        new(() =>
        {
            var list = new DefinitionDiscoveryEntry[entries.Length];
            var paths = new Dictionary<string, DefinitionPath>(entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                EntryState entry = entries[i];
                var item = new DefinitionDiscoveryEntry(
                    entry.Metadata, entry.Section, entry.Node, entry.InputError, entry.Deferred, entry.Origin,
                    entry.Artifacts, entry.BindingKeys, entry.Node.IsActive ? [] :
                        ActiveControllers(entry.Metadata, index, activeFields));
                list[i] = item;
                paths[SafePathKey(entry.Node.FieldId, entry.Node.RelativePath)] = entry.LiteralPath;
            }
            return (Array.AsReadOnly(list), new ReadOnlyDictionary<string, DefinitionPath>(paths));
        });

    internal static string SafePathKey(string fieldId, IReadOnlyList<string> relativePath)
    {
        if (relativePath.Count == 0) return fieldId;
        var parts = new string[relativePath.Count + 1];
        parts[0] = fieldId;
        for (int i = 0; i < relativePath.Count; i++) parts[i + 1] = relativePath[i];
        return string.Join('\0', parts);
    }

    private static Lazy<IReadOnlySet<DefinitionPath>> CreateRelevantPaths(EntryState[] entries, DefinitionFormIndex index) =>
        new(() =>
        {
            var paths = new HashSet<DefinitionPath>();
            foreach (EntryState entry in entries)
            {
                DefinitionNodeState node = entry.Node;
                if (!node.IsActive || !(node.IsRequired || node.HasError || entry.InputError || entry.Deferred ||
                    node.IsProvided || entry.Origin == DefinitionAnswerOrigin.Defaulted)) continue;
                paths.Add(index.PathKey(index.RootPaths.TryGetValue(node.FieldId, out IReadOnlyList<string>? root) ? root : [node.FieldId]));
                for (int length = 1; length <= node.RelativePath.Count; length++)
                {
                    string[] prefix = new string[length + 1];
                    prefix[0] = node.FieldId;
                    for (int segmentIndex = 0; segmentIndex < length; segmentIndex++)
                        prefix[segmentIndex + 1] = node.RelativePath[segmentIndex];
                    paths.Add(index.PathKey(prefix));
                }
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
    internal int OptionalInvalidCount { get; }
    internal IReadOnlyDictionary<string, DefinitionFormProgress> SectionProgress { get; }
    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> Entries { get; }
    internal Lazy<IReadOnlyDictionary<string, DefinitionPath>> EntryLiteralPaths { get; }
    internal Lazy<IReadOnlySet<DefinitionPath>> RelevantPaths { get; }
    internal IReadOnlyDictionary<string, HashSet<DefinitionPath>> SectionActivePaths { get; }
    internal IReadOnlyDictionary<string, (int ActiveCount, int SuppliedOptionalCount)> SectionActivity { get; }
    internal IEnumerable<DefinitionPath> CorrectionPaths => _correctionPaths;
    internal IEnumerable<DefinitionPath> PromotionPaths => _promotionPaths;
    internal bool IsActive(string fieldId) => _activeFields.Contains(fieldId);
    internal DefinitionDiagnostic? GetNextActionableDiagnostic(DefinitionFormModel form, string? preferredSectionId = null) =>
        SelectNextDiagnostic(form, preferredSectionId, includeRequired: true);

    internal WorkspaceActionableTarget? ResolveNextActionableTarget(
        DefinitionFormModel form,
        string? preferredSectionId = null,
        bool errorsOnly = false)
    {
        DefinitionDiagnostic? diagnostic = SelectNextDiagnostic(form, preferredSectionId, includeRequired: !errorsOnly);
        return diagnostic is null ? null : ResolveActionableTarget(form, diagnostic);
    }

    internal WorkspaceActionableTarget ResolveActionableTarget(DefinitionFormModel form, DefinitionDiagnostic diagnostic)
    {
        DefinitionPath literalPath = DiagnosticLiteralPath(form, diagnostic);
        string sectionId = _index.ResolveSection(literalPath)?.Id ?? (form.Sections.Count > 0 ? form.Sections[0].Id : string.Empty);
        string fieldId = diagnostic.FieldId.Length > 0
            ? diagnostic.FieldId
            : literalPath.Segments.Count > 0 && _index.FieldIdsByName.TryGetValue(literalPath.Segments[0], out string? mappedFieldId)
                ? mappedFieldId
                : string.Empty;
        bool isBlockingError = diagnostic.Code != "value-required" || InputDiagnostics.Contains(diagnostic);
        bool canEdit = form.CanEdit(literalPath);
        return new WorkspaceActionableTarget(sectionId, literalPath, fieldId, diagnostic, isBlockingError, canEdit);
    }

    private DefinitionPath DiagnosticLiteralPath(DefinitionFormModel form, DefinitionDiagnostic diagnostic)
    {
        if (diagnostic.Location is { } location && form.TryCorrectionPath(diagnostic.FieldId, location, Validation, out IReadOnlyList<string> path))
            return diagnostic.FieldId.Length == 0 ? DefinitionPath.FromSegments(path) : form.DataPath(path);
        if (diagnostic.FieldId.Length == 0)
            return DefinitionPath.FromSegments(diagnostic.RelativePath);
        if (diagnostic.RelativePath.Count == 0 && _index.RootPaths.TryGetValue(diagnostic.FieldId, out IReadOnlyList<string>? root))
            return form.DataPath(root);
        return form.DataPath([diagnostic.FieldId, .. diagnostic.RelativePath]);
    }

    private DefinitionDiagnostic? SelectNextDiagnostic(DefinitionFormModel form, string? preferredSectionId, bool includeRequired)
    {
        string? SectionOf(DefinitionDiagnostic diagnostic) =>
            _index.ResolveSection(DiagnosticLiteralPath(form, diagnostic))?.Id ?? (form.Sections.Count > 0 ? form.Sections[0].Id : null);

        DefinitionDiagnostic? first = InputDiagnostics.Count > 0 ? InputDiagnostics[0] : null;
        DefinitionDiagnostic? firstNonRequired = null;
        DefinitionDiagnostic? firstRequired = null;

        if (!string.IsNullOrEmpty(preferredSectionId))
        {
            foreach (DefinitionDiagnostic diagnostic in InputDiagnostics)
            {
                if (string.Equals(SectionOf(diagnostic), preferredSectionId, StringComparison.Ordinal))
                    return diagnostic;
            }
            foreach (DefinitionDiagnostic diagnostic in Validation.Diagnostics)
            {
                if (diagnostic.Code == "value-required")
                {
                    if (includeRequired) firstRequired ??= diagnostic;
                    continue;
                }
                firstNonRequired ??= diagnostic;
                if (string.Equals(SectionOf(diagnostic), preferredSectionId, StringComparison.Ordinal))
                    return diagnostic;
            }
            if (includeRequired && firstRequired is not null)
            {
                foreach (DefinitionDiagnostic diagnostic in Validation.Diagnostics)
                {
                    if (diagnostic.Code == "value-required" &&
                        string.Equals(SectionOf(diagnostic), preferredSectionId, StringComparison.Ordinal))
                        return diagnostic;
                }
            }
            return first ?? firstNonRequired ?? (includeRequired ? firstRequired : null);
        }

        if (first is not null) return first;
        foreach (DefinitionDiagnostic diagnostic in Validation.Diagnostics)
        {
            if (diagnostic.Code != "value-required") return diagnostic;
            if (includeRequired) firstRequired ??= diagnostic;
        }
        return includeRequired ? firstRequired : null;
    }
    internal DefinitionNodeState? EditorState(IReadOnlyList<string> path) => _nodes.GetValueOrDefault(_index.DataPath(path));
    internal DefinitionNodeState? EditorState(DefinitionPath path) => _nodes.GetValueOrDefault(path);
    internal IReadOnlyList<DefinitionFieldCorrectionReason> CorrectionReasons(DefinitionPath path) =>
        _correctionReasonsBuilder.TryGetValue(path, out IReadOnlyList<DefinitionFieldCorrectionReason>? list) ? list : [];
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
            DefinitionPath key;
            if (length == path.Segments.Count)
                key = path;
            else if (length == 0)
                key = s_emptyPath;
            else
            {
                var slice = new string[length];
                for (int i = 0; i < length; i++) slice[i] = path.Segments[i];
                key = DefinitionPath.FromSegments(slice);
            }
            if (correction) _correctionPaths.Add(key);
            else _requiredPaths.Add(key);
            if (promote) _promotionPaths.Add(key);
        }
    }
}
