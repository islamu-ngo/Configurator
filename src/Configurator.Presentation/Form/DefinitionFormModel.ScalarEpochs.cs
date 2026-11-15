namespace Configurator.Presentation;

using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    private long _presentationEpoch = 1;
    private long _scalarEpochSequence = 1;
    private long _reconciledInputRevision = -1;
    private long _reconciledPresentationEpoch = -1;
    private long[]? _fieldScalarEpochs;
    private Dictionary<string, long>? _sectionScalarEpochs;
    private Dictionary<string, int>? _sectionIndicesById;
    private int[][]? _fieldSectionIndices;
    private Dictionary<string, DefinitionSection>? _authoredSectionsById;

    private DefinitionValidationResult? _epochBaselineValidation;
    private long[]? _baselineInputVersions;
    private int[]? _baselineDiagHashes;
    private int[]? _baselineNodeHashes;
    private bool[]? _baselineSectionApplicable;
    private int[]? _scratchDiagHashes;
    private int[]? _scratchNodeHashes;
    private int _baselineGlobalDiagHash;
    private int _baselineOmittedCount;
    private int _baselinePromotedCount;
    private DefinitionSessionPrivacy _baselineSessionPrivacy;
    private FieldDisclosure? _baselineDisclosureOverride;
    private DefinitionConfigurationContext? _baselineContext;
    private bool _baselineHasUndecided;
    private ProfileComparisonProjection? _baselineProfile;
    private BrowserProfileProjection? _baselineBrowserProfile;

    private void BumpPresentationEpoch() => System.Threading.Interlocked.Increment(ref _presentationEpoch);

    /// <summary>Monotonic per-section scalar-stamp epoch that advances only when inputs, validation, or view options affecting that section change.</summary>
    public long GetSectionScalarEpoch(string sectionId)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureScalarEpochsCurrent();
            return _sectionScalarEpochs!.TryGetValue(sectionId, out long epoch) ? epoch : Volatile.Read(ref _scalarEpochSequence);
        }
    }

    /// <summary>Monotonic per-root-field scalar-stamp epoch that advances only when inputs, validation, or view options affecting that field change.</summary>
    public long GetRootScalarEpoch(string fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureScalarEpochsCurrent();
            return _index!.FieldPositions.TryGetValue(fieldId, out int position)
                ? _fieldScalarEpochs![position]
                : Volatile.Read(ref _scalarEpochSequence);
        }
    }

    internal bool? TryGetSectionExpansion(string sectionId) =>
        _view.SectionExpansion.TryGetValue(sectionId, out bool expanded) ? expanded : null;

    internal bool AreContinuousPathsAlwaysVisible(string sectionId) =>
        Definition.Deployment is null && _view.EditorExpansion.Count == 0 &&
        !_index!.Root.HasVariants && SectionHasOnlySimpleRoots(sectionId);

    private bool SectionHasOnlySimpleRoots(string sectionId)
    {
        if (!TryGetFormSection(sectionId, out DefinitionFormSection? section)) return false;
        foreach (string fieldId in section.Fields)
        {
            if (!_index!.Roots.TryGetValue(fieldId, out DefinitionFormIndex.Node? root)) return false;
            if (root.Schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence ||
                root.Schema.Variants.Count > 0)
                return false;
        }
        return true;
    }

    private bool TryGetFormSection(string sectionId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DefinitionFormSection? found)
    {
        foreach (DefinitionFormSection section in Sections)
        {
            if (string.Equals(section.Id, sectionId, StringComparison.Ordinal))
            {
                found = section;
                return true;
            }
        }
        found = null;
        return false;
    }

    internal DefinitionSection? FindAuthoredSectionCached(string sectionId)
    {
        if (Definition.Presentation is not { } presentation) return null;
        if (_authoredSectionsById is null)
        {
            var map = new Dictionary<string, DefinitionSection>(presentation.Sections.Count, StringComparer.Ordinal);
            foreach (DefinitionSection section in presentation.Sections) map[section.Id] = section;
            _authoredSectionsById = map;
        }
        return _authoredSectionsById.GetValueOrDefault(sectionId);
    }

    internal bool IsContinuousActivePathVisibleLocked(DefinitionPath path, string sectionId, bool sectionApplicable, bool sectionCollapsed)
    {
        if (!TryEditorMetadata(path, out _, out _)) return false;
        if (path.Segments.Count == 1 && Definition.Deployment is not null &&
            _index!.FieldIdsByName.TryGetValue(path.Segments[0], out string? selectorFieldId) &&
            IsConfigurationSelectorRoot(selectorFieldId, Facts.Validation))
            return false;
        if (!sectionApplicable) return false;
        DefinitionFormFacts facts = Facts;
        if (facts.NeedsCorrection(path) || facts.HasRequiredDescendant(path) ||
            (_view.PromotedPaths.Count > 0 && _view.PromotedPaths.Contains(GetEditorIdentity(path))))
            return true;
        if (_view.EditorExpansion.Count > 0 && TryLocalExpansion(path, out bool continuousExpanded))
            return continuousExpanded;
        return !sectionCollapsed;
    }

    private void EnsureScalarEpochsCurrent()
    {
        long presentationEpoch = Volatile.Read(ref _presentationEpoch);
        DefinitionFormFacts facts = Facts;
        long revision = InputRevision;
        if (_fieldScalarEpochs is not null && _reconciledInputRevision == revision && _reconciledPresentationEpoch == presentationEpoch)
            return;

        EnsureEpochMapsInitialized();
        DefinitionValidationResult validation = facts.Validation;
        int fieldCount = Fields.Count;
        int sectionCount = Sections.Count;

        for (int index = 0; index < fieldCount; index++)
        {
            _scratchDiagHashes![index] = 0;
            _scratchNodeHashes![index] = 0;
        }

        int globalDiagHash = 0;
        IReadOnlyList<DefinitionDiagnostic> diagnostics = validation.Diagnostics;
        for (int index = 0; index < diagnostics.Count; index++)
        {
            DefinitionDiagnostic diagnostic = diagnostics[index];
            int hash = HashCode.Combine(diagnostic.Code, diagnostic.RelativePath.Count);
            if (diagnostic.RelativePath.Count > 0)
            {
                for (int segment = 0; segment < diagnostic.RelativePath.Count; segment++)
                    hash = HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(diagnostic.RelativePath[segment]));
            }
            if (diagnostic.FieldId.Length > 0 && _index!.FieldPositions.TryGetValue(diagnostic.FieldId, out int position))
            {
                _scratchDiagHashes![position] = HashCode.Combine(_scratchDiagHashes[position], hash);
            }
            else
            {
                globalDiagHash = HashCode.Combine(globalDiagHash, hash);
            }
        }

        IReadOnlyList<DefinitionNodeState> nodes = validation.Nodes;
        for (int index = 0; index < nodes.Count; index++)
        {
            DefinitionNodeState node = nodes[index];
            if (node.FieldId.Length == 0 || !_index!.FieldPositions.TryGetValue(node.FieldId, out int position))
                continue;
            int hash = HashCode.Combine(
                node.RelativePath.Count,
                node.IsActive,
                node.IsRequired,
                node.IsProvided,
                node.IsReady,
                node.IsObligation,
                node.IsObligationReady,
                HashCode.Combine(node.IsMissing, node.Sensitivity));
            _scratchNodeHashes![position] = HashCode.Combine(_scratchNodeHashes[position], hash);
        }

        bool fullInvalidate = _epochBaselineValidation is null ||
            _reconciledPresentationEpoch != presentationEpoch ||
            _baselineOmittedCount != 0 ||
            _editing.Omitted.Count != 0 ||
            _baselinePromotedCount != _view.PromotedPaths.Count ||
            _baselineSessionPrivacy != SessionPrivacy ||
            _baselineDisclosureOverride != DisclosureOverride ||
            !Equals(_baselineContext, validation.ConfigurationContext) ||
            _baselineHasUndecided != validation.HasUndecidedDependencies ||
            _baselineGlobalDiagHash != globalDiagHash ||
            !Equals(_baselineProfile, Profile) ||
            !Equals(_baselineBrowserProfile, BrowserProfile);

        if (!fullInvalidate)
        {
            for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
            {
                DefinitionSection? authored = FindAuthoredSectionCached(Sections[sectionIndex].Id);
                bool applicable = authored?.IsApplicable(validation) ?? true;
                if (applicable != _baselineSectionApplicable![sectionIndex])
                {
                    fullInvalidate = true;
                    break;
                }
            }
        }

        long nextEpoch = System.Threading.Interlocked.Increment(ref _scalarEpochSequence);
        if (fullInvalidate)
        {
            Array.Fill(_fieldScalarEpochs!, nextEpoch);
            for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
            {
                DefinitionFormSection section = Sections[sectionIndex];
                _sectionScalarEpochs![section.Id] = nextEpoch;
                DefinitionSection? authored = FindAuthoredSectionCached(section.Id);
                _baselineSectionApplicable![sectionIndex] = authored?.IsApplicable(validation) ?? true;
            }
            for (int index = 0; index < fieldCount; index++)
                _baselineInputVersions![index] = _editing.Input(index).Version;
        }
        else
        {
            IReadOnlyList<FieldState> prevStates = _epochBaselineValidation!.Fields;
            IReadOnlyList<FieldState> currStates = validation.Fields;
            for (int index = 0; index < fieldCount; index++)
            {
                long version = _editing.Input(index).Version;
                if (version != _baselineInputVersions![index] ||
                    _scratchDiagHashes![index] != _baselineDiagHashes![index] ||
                    _scratchNodeHashes![index] != _baselineNodeHashes![index] ||
                    !FieldStateUnchanged(prevStates[index], currStates[index]))
                {
                    _baselineInputVersions[index] = version;
                    _fieldScalarEpochs![index] = nextEpoch;
                    int[] mappedSections = _fieldSectionIndices![index];
                    for (int s = 0; s < mappedSections.Length; s++)
                        _sectionScalarEpochs![Sections[mappedSections[s]].Id] = nextEpoch;
                }
            }
        }

        Array.Copy(_scratchDiagHashes!, _baselineDiagHashes!, fieldCount);
        Array.Copy(_scratchNodeHashes!, _baselineNodeHashes!, fieldCount);
        _epochBaselineValidation = validation;
        _baselineGlobalDiagHash = globalDiagHash;
        _baselineOmittedCount = _editing.Omitted.Count;
        _baselinePromotedCount = _view.PromotedPaths.Count;
        _baselineSessionPrivacy = SessionPrivacy;
        _baselineDisclosureOverride = DisclosureOverride;
        _baselineContext = validation.ConfigurationContext;
        _baselineHasUndecided = validation.HasUndecidedDependencies;
        _baselineProfile = Profile;
        _baselineBrowserProfile = BrowserProfile;
        _reconciledInputRevision = revision;
        _reconciledPresentationEpoch = presentationEpoch;
    }

    private static bool FieldStateUnchanged(FieldState left, FieldState right) =>
        ReferenceEquals(left.Definition, right.Definition) &&
        left.IsActive == right.IsActive &&
        left.IsRequired == right.IsRequired &&
        left.IsProvided == right.IsProvided &&
        left.HasError == right.HasError;

    private void EnsureEpochMapsInitialized()
    {
        if (_fieldScalarEpochs is not null) return;
        int fieldCount = Fields.Count;
        int sectionCount = Sections.Count;
        _fieldScalarEpochs = new long[fieldCount];
        Array.Fill(_fieldScalarEpochs, 1L);
        _sectionScalarEpochs = new Dictionary<string, long>(sectionCount, StringComparer.Ordinal);
        _sectionIndicesById = new Dictionary<string, int>(sectionCount, StringComparer.Ordinal);
        var sectionsByField = new List<int>[fieldCount];
        for (int index = 0; index < fieldCount; index++) sectionsByField[index] = [];
        for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
        {
            DefinitionFormSection section = Sections[sectionIndex];
            _sectionScalarEpochs[section.Id] = 1L;
            _sectionIndicesById[section.Id] = sectionIndex;
            foreach (string fieldId in section.Fields)
            {
                if (_index!.FieldPositions.TryGetValue(fieldId, out int fieldPosition))
                    sectionsByField[fieldPosition].Add(sectionIndex);
            }
        }
        _fieldSectionIndices = new int[fieldCount][];
        for (int index = 0; index < fieldCount; index++)
            _fieldSectionIndices[index] = sectionsByField[index].Count == 0 ? [] : sectionsByField[index].ToArray();
        _baselineInputVersions = new long[fieldCount];
        _baselineDiagHashes = new int[fieldCount];
        _baselineNodeHashes = new int[fieldCount];
        _scratchDiagHashes = new int[fieldCount];
        _scratchNodeHashes = new int[fieldCount];
        _baselineSectionApplicable = new bool[sectionCount];
    }
}
