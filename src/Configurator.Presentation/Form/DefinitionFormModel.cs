namespace Configurator.Presentation;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Configurator.Core.Definitions;

/// <summary>
/// Keeps field editing independent of a target application and freezes
/// the admitted definition for the lifetime of the form.
/// </summary>
public sealed partial class DefinitionFormModel : ObservableObject, IDisposable
{
    private readonly object _gate = new();
    private List<Action> _notifications = [];
    private int _mutationDepth;
    private readonly DefinitionEditingState _editing;
    private readonly DefinitionViewState _view;
    private readonly DefinitionLocalization _localization;
    private HashSet<DefinitionPath> Omitted => _editing.Omitted;
    private DefinitionFormIndex? _index;
    private DefinitionFormFacts? _facts;
    private Lazy<IReadOnlyList<DefinitionDiscoveryEntry>>? _orderedEntries;
    private Lazy<IReadOnlyList<DefinitionDiscoveryEntry>>? _relevantEntries;
    private Lazy<DefinitionDiscoverySnapshot>? _discovery;
    private bool _promotionPending;
    private DefinitionFormProjection? _projection;
    private readonly DefinitionFormUndoHistory _undoHistory = new();
    private DefinitionAcceptedEditingState? _undoBaseline;
    private string? _pendingUndoLabel;
    private bool _inputChangedInUndoScope;
    private bool _undoReplayActive;
    private bool _privacyClearingActive;
    private bool _disposed;

    public DefinitionFormModel(Definition definition,
        DefinitionSessionPrivacy sessionPrivacy = DefinitionSessionPrivacy.NativeComplete,
        IReadOnlySet<string>? supportedCapabilities = null,
        DefinitionEditorVisibilityPolicy editorVisibility = DefinitionEditorVisibilityPolicy.Authored)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(sessionPrivacy)) throw new ArgumentOutOfRangeException(nameof(sessionPrivacy));
        if (!Enum.IsDefined(editorVisibility)) throw new ArgumentOutOfRangeException(nameof(editorVisibility));
        if (supportedCapabilities is not null && definition.RequiredCapabilities.Any(capability => !supportedCapabilities.Contains(capability)))
            throw new ArgumentException("consumer-capability-unsupported", nameof(supportedCapabilities));
        Definition = definition;
        _localization = new(definition);
        _editing = new(definition.Fields, sessionPrivacy, (field, readState) =>
            new DefinitionFieldInput(field, _gate, readState, Edit, (text, value) => AllowsValue(field, text, value),
                () => _disposed ? null : ReadValue([field.Id]), RecordOverride, Notify));
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var assignedPointers = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<DefinitionFormSection>();
        if (definition.Presentation is { } authored)
            foreach (DefinitionSection section in authored.Sections)
            {
                string[] ownedPointers = section.Pointers.Where(assignedPointers.Add).ToArray();
                string[] sectionFields = ownedPointers
                    .Select(pointer => DefinitionPath.Parse(pointer).Segments)
                    .Where(segments => segments.Count > 0)
                    .Select(segments => DefinitionProfile.FieldId(segments[0]))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                foreach (string fieldId in sectionFields)
                    assigned.Add(fieldId);
                DefinitionFormSectionGroup[] groups = section.Groups.Select(group =>
                    new DefinitionFormSectionGroup(
                        group.Id,
                        group.Title,
                        group.Description,
                        group.Disclosure,
                        group.InitiallyCollapsed,
                        IsApplicable: true,
                        HasBlockingIssue: false,
                        IsExpanded: !group.InitiallyCollapsed,
                        group.Pointers,
                        SectionFieldIdsFromPointers(group.Pointers))).ToArray();
                sections.Add(new(section.Id, section.Title, section.Description,
                    sectionFields, section.Disclosure, ownedPointers, groups));
            }
        string[] unlisted = definition.Fields.Select(field => field.Id).Where(id => !assigned.Contains(id)).ToArray();
        string? fallback = null;
        if (unlisted.Length > 0 || sections.Count == 0)
        {
            fallback = "configure";
            for (int suffix = 1; sections.Any(section => section.Id == fallback); suffix++)
                fallback = "configure-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sections.Add(new(fallback, "Configure", null, unlisted,
                pointers: unlisted.Select(id => "/" + definition.Fields.First(f => f.Id == id).Name)));
        }
        Sections = Array.AsReadOnly(sections.ToArray());
        Experiences = Array.AsReadOnly(definition.Presentation is { } presentation
            ? presentation.Experiences.Select(experience => new DefinitionFormExperience(experience.Id,
                experience.Title, experience.InitialDisclosure, experience.Navigation,
                fallback is null ? experience.Sections : experience.Sections.Append(fallback),
                experience.Description)).ToArray()
            : [new DefinitionFormExperience("configure", "Configure", FieldDisclosure.Guided,
                "sections", Sections.Select(section => section.Id), null)]);
        _view = new(definition.Presentation?.DefaultExperience ?? "configure")
        {
            Locale = _localization.DefaultLocale,
            EditorVisibility = editorVisibility
        };
        _index = new DefinitionFormIndex(definition, Sections, Experiences);
    }

    private static string[] SectionFieldIdsFromPointers(IReadOnlyList<string> pointers) =>
        pointers.Select(pointer => DefinitionPath.Parse(pointer).Segments)
            .Where(segments => segments.Count > 0)
            .Select(segments => DefinitionProfile.FieldId(segments[0]))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public Definition Definition { get; }
    public IReadOnlyList<DefinitionFormExperience> Experiences { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public IReadOnlyList<DefinitionFormSection> DisplaySections => Array.AsReadOnly(Sections.Select(section =>
    {
        int index = Definition.Presentation?.Sections.ToList().FindIndex(item => item.Id == section.Id) ?? -1;
        string prefix = "/presentation/sections/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (index < 0)
        {
            return new DefinitionFormSection(section.Id, PresentationStrings.Get("Configure", Locale),
                section.Description, section.Fields, section.Disclosure, section.Pointers, section.Groups, section.IsApplicable);
        }
        DefinitionFormSectionGroup[] localizedGroups = section.Groups.Select((group, groupIndex) =>
        {
            string gPrefix = prefix + "/groups/" + groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return group with
            {
                Title = _localization.Text(Locale, gPrefix + "/title", group.Title)!,
                Description = _localization.Text(Locale, gPrefix + "/description", group.Description)
            };
        }).ToArray();
        return new DefinitionFormSection(section.Id,
            _localization.Text(Locale, prefix + "/title", section.Title)!,
            _localization.Text(Locale, prefix + "/description", section.Description),
            section.Fields, section.Disclosure, section.Pointers, localizedGroups, section.IsApplicable);
    }).ToArray());
    public IReadOnlyList<DefinitionFormExperience> DisplayExperiences => Array.AsReadOnly(Experiences.Select(experience =>
    {
        int index = Definition.Presentation?.Experiences.ToList().FindIndex(item => item.Id == experience.Id) ?? -1;
        string prefix = "/presentation/journeys/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return index < 0 ? new DefinitionFormExperience(experience.Id, PresentationStrings.Get("Configure", Locale),
            experience.InitialDisclosure, experience.Navigation, experience.Sections, experience.Description) : new DefinitionFormExperience(experience.Id,
            _localization.Text(Locale, prefix + "/title", experience.Title)!, experience.InitialDisclosure,
            experience.Navigation, experience.Sections, _localization.Text(Locale, prefix + "/description", experience.Description));
    }).ToArray());
    public string SelectedExperienceId => _view.SelectedExperienceId;
    public string Locale => _view.Locale;
    public string Direction => _localization.Direction(Locale);

    /// <summary>Edits only a declared output choice; absent or unrelated entry metadata never overrides initialization.</summary>
    public bool SelectOutput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        DefinitionOutputSelector? selector = Definition.Presentation?.OutputSelector;
        return selector is not null && selector.Options.Any(option => option.Value == value)
            && SetValue(selector.Location, JsonSerializer.SerializeToElement(value));
    }

    /// <summary>Changes public display metadata without changing answers or retiring prepared output.</summary>
    public bool SelectLocale(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_localization.Locales.Contains(locale, StringComparer.Ordinal)) return false;
        if (Locale == locale) return true;
        _view.Locale = locale;
        _orderedEntries = null;
        _relevantEntries = null;
        _discovery = null;
        OnPropertyChanged(nameof(Locale));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(DisplaySections));
        OnPropertyChanged(nameof(DisplayExperiences));
        OnViewChanged();
        return true;
    }

    public string? LocalizedText(DefinitionPath location, string? baseText)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(location);
            return _localization.Text(Locale, location.ToPointer(), baseText);
        }
    }
    public bool OptionalEditorsExpanded => _view.OptionalEditorsExpanded;
    public FieldDisclosure? DisclosureOverride => _view.DisclosureOverride;
    public event EventHandler? ViewChanged;

    /// <summary>Selects optional detail independently of the authored experience without editing answers.</summary>
    public void SetDisclosureOverride(FieldDisclosure? disclosure)
    {
        if (disclosure is { } value && !Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(disclosure));
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (DisclosureOverride == disclosure) return;
        _view.DisclosureOverride = disclosure;
        _view.OptionalEditorsExpanded = false;
        _view.ResetExpansion(clearPromotions: false);
        OnPropertyChanged(nameof(DisclosureOverride));
        OnViewChanged();
    }

    private FieldDisclosure EffectiveDisclosure =>
        DisclosureOverride ?? Experiences.Single(item => item.Id == SelectedExperienceId).InitialDisclosure;

    public bool SelectExperience(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Experiences.Any(experience => experience.Id == id)) return false;
        if (SelectedExperienceId == id) return true;
        _view.SelectedExperienceId = id;
        _orderedEntries = null;
        _relevantEntries = null;
        _discovery = null;
        _view.OptionalEditorsExpanded = false;
        _view.ResetExpansion(clearPromotions: true);
        OnPropertyChanged(nameof(SelectedExperienceId));
        OnViewChanged();
        return true;
    }

    public void SetAllSectionsExpanded(bool expanded)
    {
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _view.OptionalEditorsExpanded = expanded;
        _view.ResetExpansion(clearPromotions: true);
        OnViewChanged();
    }

    public bool SetSectionExpanded(string sectionId, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Experiences.Single(experience => experience.Id == SelectedExperienceId).Sections.Contains(sectionId))
            return false;
        _view.SectionExpansion[sectionId] = expanded;
        foreach (string rootId in Sections.Single(section => section.Id == sectionId).Fields)
        {
            string identity = GetEditorIdentity([rootId]);
            _view.ClearEditorExpansion(identity, includeSelf: true);
            _view.ClearPromotions(identity);
        }
        OnViewChanged();
        return true;
    }

    public bool IsEditorVisible(IReadOnlyList<string> path) => IsEditorVisible(DataPath(path));

    public bool IsEditorVisible(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node indexed, out _)) return false;
            bool ordinary = TryOrdinaryRootPath(path.Segments, out string[] fieldPath);
            if (ordinary && IsConfigurationSelector(fieldPath)) return false;
            DefinitionFormFacts facts = Facts;
            if (!IsEditorActive(path)) return false;
            bool needsCorrection = facts.NeedsCorrection(path);
            if (needsCorrection || facts.HasRequiredDescendant(path) || indexed.HasEssentialDecision ||
                _view.PromotedPaths.Count > 0 && _view.PromotedPaths.Contains(GetEditorIdentity(path))) return true;
            if (_view.EditorExpansion.Count > 0 && TryLocalExpansion(path, out bool locallyExpanded))
                return locallyExpanded || facts.HasRequiredDescendant(path);
            DefinitionFormExperience experience = Experiences.Single(item => item.Id == SelectedExperienceId);
            DefinitionFormSection? section = path.Segments.Count > 0 ? _index!.ResolveSection(path) : null;
            if (section is not null && !experience.Sections.Contains(section.Id)) return false;
            if (_view.EditorVisibility == DefinitionEditorVisibilityPolicy.Direct && CanEdit(path)) return true;
            bool expanded = OptionalEditorsExpanded;
            if (section is not null && _view.SectionExpansion.TryGetValue(section.Id, out bool sectionExpanded))
                expanded = sectionExpanded;
            if (expanded) return true;
            FieldDisclosure disclosure = indexed.Disclosure;
            return disclosure == FieldDisclosure.Essential || disclosure <= EffectiveDisclosure &&
                (section is null || section.Disclosure <= EffectiveDisclosure);
        }
    }

    private void OnViewChanged(bool discoveryOnly = false)
    {
        _projection = discoveryOnly ? _projection?.WithDiscovery(CurrentDiscovery) : null;
        _promotionPending = true;
        OnPropertyChanged(nameof(Projection));
        OnPropertyChanged(nameof(VisibleFields));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        Notify(() => ViewChanged?.Invoke(this, EventArgs.Empty));
    }
    internal DefinitionFormFacts Facts => _facts ??= new DefinitionFormFacts(this, _index!,
        DefinitionValidator.Validate(Definition, Snapshot()));

    internal IReadOnlyList<DefinitionFormSection> OrderedSections
    {
        get
        {
            var display = DisplaySections.ToDictionary(section => section.Id, StringComparer.Ordinal);
            return Array.AsReadOnly(_index!.ExperienceSections[SelectedExperienceId]
                .Select(section => display[section.Id]).ToArray());
        }
    }

    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> OrderedEntries =>
        _orderedEntries ??= OrderEntries(Facts.Entries, OrderedSections);

    private Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> OrderEntries(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, IReadOnlyList<DefinitionFormSection> sections)
    {
        var order = sections.Select((section, position) => (section.Id, position))
            .ToDictionary(item => item.Id, item => item.position, StringComparer.Ordinal);
        string locale = Locale;
        return new(() => Array.AsReadOnly(entries.Value
            .Select(entry => entry.WithDisplay(_localization, locale,
                sections[order[entry.SectionId]].Title, order[entry.SectionId])).ToArray()));
    }

    internal Lazy<DefinitionDiscoverySnapshot> CurrentDiscovery =>
        _discovery ??= CreateDiscovery(OrderedEntries, DiscoverySelection);

    private static Lazy<DefinitionDiscoverySnapshot> CreateDiscovery(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, DefinitionDiscoverySelection selection) =>
        new(() => DefinitionDiscovery.Search(entries.Value, selection));

    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> CurrentRelevantEntries =>
        _relevantEntries ??= SelectRelevantEntries(OrderedEntries, Facts.RelevantPaths, _index!);

    private static Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> SelectRelevantEntries(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, Lazy<IReadOnlySet<DefinitionPath>> paths, DefinitionFormIndex index) =>
        new(() => Array.AsReadOnly(entries.Value.Where(entry =>
            paths.Value.Contains(index.PathKey([entry.FieldId, .. entry.RelativePath]))).ToArray()));

    public DefinitionFormProjection Projection
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ReconcileReviewSignatures(Facts);
                return _projection ??= DefinitionFormProjection.Create(this);
            }
        }
    }
    public DefinitionDiscoverySelection DiscoverySelection => _view.DiscoverySelection;
    public string DiscoveryQuery => DiscoverySelection.Query;

    public void SetDiscovery(string query)
    {
        SetDiscoverySelection(new(query, DiscoverySelection.IncludeInactive, DiscoverySelection.Facets, DiscoverySelection.Filter));
    }

    public void SetDiscoverySelection(DefinitionDiscoverySelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _view.DiscoverySelection = selection;
            _discovery = null;
            OnPropertyChanged(nameof(DiscoverySelection));
            OnPropertyChanged(nameof(DiscoveryQuery));
            OnViewChanged(discoveryOnly: true);
        }
    }

    public void ClearDiscoveryFilters() => SetDiscoverySelection(new(DiscoveryQuery));
    public void ClearDiscoveryQuery() => SetDiscovery(string.Empty);
    public void ResetDiscovery() => SetDiscoverySelection(new());

    public IReadOnlyList<DefinitionDiagnostic> InputDiagnostics
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Array.AsReadOnly(Facts.InputDiagnostics.Select(SafeInputDiagnostic).ToArray());
            }
        }
    }

    /// <summary>
    /// Removes every declared non-public value, plus drafts at masked locations, while public siblings remain.
    /// Returns whether one invalidating revision was published, even if nothing protected was supplied.
    /// </summary>
    public bool ClearPrivateState()
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _privacyClearingActive = true;
            try
            {
                RetireProfileSession();
                OnPropertyChanged(nameof(Profile));
                RetireOutputWorkflow(clearHistory: true);
                RetireTransferReviews();
                RetireImportComparison();
                RetireDocument();
                Dictionary<string, JsonElement>? originalBranches = _index!.Root.HasVariants ? _editing.Root.ToDictionary() : null;
                bool hasProtectedRoots = DefinitionSecretPolicy.IsProtected(Definition.Configuration);
                bool configurationProtected = DefinitionSecretPolicy.DeclaresProtected(Definition.Configuration);
                var retiredOmissions = new HashSet<string[]>(SegmentPathComparer.Instance);
                foreach (DefinitionFieldInput field in Fields)
                {
                    FieldDefinition definition = field.Definition;
                    if (!configurationProtected && !DefinitionSecretPolicy.IsProtected(definition.Node)) continue;
                    if (configurationProtected || DefinitionSecretPolicy.DeclaresProtected(definition.Node))
                    {
                        ClearRowIdentities([definition.Id]);
                        field.Clear();
                        continue;
                    }
                    JsonNode? original = field.Value is { } supplied ? JsonNode.Parse(supplied.GetRawText()) : null;
                    JsonNode? retained = original?.DeepClone();
                    if (original is not null && DefinitionSecretPolicy.RemoveProtected(definition.Node, retained))
                    {
                        ClearRowIdentities([definition.Id]);
                        field.Clear();
                        continue;
                    }
                    // Location and omission checks resolve rows and branches against the current value, so they run before pruning.
                    (HashSet<string[]> drafts, HashSet<string[]> origins) = DiscardedPaths(field.PrivateStatePaths(),
                        path => RequiresMaskingAt(path, literal: false), original, retained, skip: 1);
                    foreach (DefinitionPath omitted in Omitted.Where(path =>
                                 path.Segments.Count > 1 && string.Equals(path.Segments[0], definition.Id, StringComparison.Ordinal)))
                    {
                        string[] segments = omitted.Segments.ToArray();
                        if (Vanished(original, retained, segments, skip: 1) || RequiresMaskingAt(segments, literal: false))
                            retiredOmissions.Add(segments);
                    }
                    JsonElement? retainedElement = field.Value is { ValueKind: JsonValueKind.Null } nullable ? nullable :
                        retained is null ? null : JsonSerializer.SerializeToElement(retained);
                    field.RetainPublic(retainedElement, drafts.Contains, origins.Contains);
                }
                DefinitionFieldState rootInput = _editing.RootInput;
                JsonObject rootOriginal = JsonSerializer.SerializeToNode(_editing.Root)!.AsObject();
                JsonObject rootRetained = rootOriginal.DeepClone().AsObject();
                foreach (string name in _editing.Root.Keys.Where(name => !Fields.Any(field => field.Definition.Name == name)).ToArray())
                {
                    bool resolved = TryRootMetadata([name], out DefinitionFormIndex.Node node, out _, out bool inheritedProtected);
                    if (!resolved || inheritedProtected || DefinitionSecretPolicy.RemoveProtected(node.Schema, rootRetained[name]))
                        rootRetained.Remove(name);
                }
                (HashSet<string[]> rootDrafts, HashSet<string[]> rootOrigins) = DiscardedPaths(
                    rootInput.DraftTexts.Select(draft => draft.Path).Concat(rootInput.InvalidPaths)
                        .Concat(rootInput.Origins.Select(origin => origin.Path)),
                    path => RequiresMaskingAt(path, literal: true), rootOriginal, rootRetained, skip: 0);
                foreach (DefinitionPath omitted in Omitted.Where(path =>
                             path.Segments.Count > 0 && !Fields.Any(field => field.Definition.Id == path.Segments[0])))
                {
                    string[] segments = omitted.Segments.ToArray();
                    if (RequiresMaskingAt(segments, literal: true) ||
                        segments.Length > 1 && Vanished(rootOriginal, rootRetained, segments, skip: 0) ||
                        !TryRootMetadata([segments[0]], out _, out _, out _))
                        retiredOmissions.Add(segments);
                }
                Omitted.RemoveWhere(path => retiredOmissions.Contains(path.Segments.ToArray()));
                rootInput.DraftTexts.RemoveAll(draft => rootDrafts.Contains(draft.Path));
                rootInput.InvalidPaths.RemoveAll(rootDrafts.Contains);
                rootInput.Origins.RemoveAll(origin => rootOrigins.Contains(origin.Path));
                _editing.Root.Clear();
                foreach ((string name, JsonNode? value) in rootRetained)
                    _editing.Root.Add(name, JsonSerializer.SerializeToElement(value));
                if (originalBranches is not null)
                    foreach (string identity in ChangedBranchIdentities(_editing.Root, originalBranches))
                        _editing.RetireBranch(identity);
                _undoHistory.PurgeProtected(Definition, Fields, IsProtectedPathForUndo);
                NotifyUndoPropertiesChanged();
                if (hasProtectedRoots) OnInputChanged();
                else NotifySecretSessions(consentChanged: true, formDisposed: false);
                return hasProtectedRoots;
            }
            finally { _privacyClearingActive = false; }
        }
    }

    /// <summary>
    /// Drafts and invalid markers are discarded where masked or where pruning removed their value; origins only where
    /// pruning removed their value, so public descendants keep the origin recorded on a container.
    /// </summary>
    private static (HashSet<string[]> Drafts, HashSet<string[]> Origins) DiscardedPaths(IEnumerable<string[]> paths,
        Func<IReadOnlyList<string>, bool> masked, JsonNode? original, JsonNode? retained, int skip)
    {
        var drafts = new HashSet<string[]>(SegmentPathComparer.Instance);
        var origins = new HashSet<string[]>(SegmentPathComparer.Instance);
        foreach (string[] path in paths)
        {
            bool vanished = Vanished(original, retained, path, skip);
            if (vanished) origins.Add(path);
            if (vanished || masked(path)) drafts.Add(path);
        }
        return (drafts, origins);
    }

    private sealed class SegmentPathComparer : IEqualityComparer<string[]>
    {
        public static readonly SegmentPathComparer Instance = new();

        public bool Equals(string[]? x, string[]? y) =>
            ReferenceEquals(x, y) || x is not null && y is not null && x.AsSpan().SequenceEqual(y, StringComparer.Ordinal);

        public int GetHashCode(string[] obj)
        {
            var hash = new HashCode();
            foreach (string segment in obj) hash.Add(segment, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    /// <summary>Unresolved locations fail closed; a public value is masked when its whole value can hold a protected one.</summary>
    private bool RequiresMaskingAt(IReadOnlyList<string> path, bool literal)
    {
        bool found = literal
            ? TryRootMetadata(path, out DefinitionFormIndex.Node node, out _, out bool inheritedProtected)
            : TryMetadataNode(path, out node, out _, out inheritedProtected);
        return !found || inheritedProtected || node.IsProtected;
    }

    /// <summary>Whether a location, or a container above it, held a value before pruning and holds none afterwards.</summary>
    private static bool Vanished(JsonNode? original, JsonNode? retained, string[] path, int skip)
    {
        for (int length = skip + 1; length <= path.Length; length++)
            if (TryLocate(original, path, skip, length) && !TryLocate(retained, path, skip, length)) return true;
        return false;

        static bool TryLocate(JsonNode? node, string[] path, int skip, int length)
        {
            for (int index = skip; index < length; index++)
            {
                if (node is JsonObject mapping && mapping.TryGetPropertyValue(path[index], out JsonNode? child)) node = child;
                else if (node is JsonArray rows && int.TryParse(path[index], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int row) && row < rows.Count) node = rows[row];
                else return false;
            }
            return true;
        }
    }

    public DefinitionSessionPrivacy SessionPrivacy => _editing.Privacy;
    public void SetSecretEntryEnabled(bool enabled)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (SessionPrivacy == DefinitionSessionPrivacy.NativeComplete) return;
            DefinitionSessionPrivacy next = enabled ? DefinitionSessionPrivacy.BrowserSecretsEnabled
                : DefinitionSessionPrivacy.BrowserNoSecrets;
            if (SessionPrivacy == next) return;
            _privacyClearingActive = true;
            try
            {
                _editing.Privacy = next;
                RetireOutputWorkflow(clearHistory: true);
                RetireTransferReviews();
                if (!enabled)
                {
                    RetireProfileSession();
                    OnPropertyChanged(nameof(Profile));
                    foreach (DefinitionFieldInput field in Fields)
                    {
                        field.ClearDeniedDrafts(CanMutate);
                        if (field.Value is not { } value)
                        {
                            if (field.Definition.Sensitivity == FieldSensitivity.Secret) field.Clear();
                            continue;
                        }
                        JsonNode? retained = DefinitionSecretPolicy.RemoveSecrets(field.Definition.Node,
                            JsonNode.Parse(value.GetRawText()), DefinitionSecretPolicy.IsSecret(Definition.Configuration));
                        if (retained is null) field.Clear();
                        else field.RetainValue(JsonSerializer.SerializeToElement(retained));
                    }
                    JsonNode? root = DefinitionSecretPolicy.RemoveSecrets(Definition.Configuration,
                        JsonSerializer.SerializeToNode(_editing.Root));
                    _editing.Root.Clear();
                    if (root is JsonObject mapping)
                        foreach ((string name, JsonNode? value) in mapping)
                            _editing.Root.Add(name, JsonSerializer.SerializeToElement(value));
                    _editing.RootInput.DraftTexts.RemoveAll(draft => !CanMutate(draft.Path));
                    _editing.RootInput.InvalidPaths.RemoveAll(path => !CanMutate(path));
                    _undoHistory.PurgeSecrets(Definition, Fields, IsSecretPathForUndo);
                    NotifyUndoPropertiesChanged();
                }
                OnPropertyChanged(nameof(SessionPrivacy));
                OnInputChanged();
                NotifySecretSessions(consentChanged: true, formDisposed: false);
            }
            finally { _privacyClearingActive = false; }
        }
    }

    public bool CanEdit(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            return GetFieldValidation(path).CanEdit;
        }
    }

    public bool CanEdit(DefinitionPath path)
    {
        lock (_gate) return !_disposed && GetFieldValidation(path).CanEdit;
    }

    // Mutation custody remains independent of the current display affordance.
    private bool CanMutate(IReadOnlyList<string> path) =>
        !ProfileBlocks(DataPath(path)) && (SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets ||
        TryMetadataNode(path, out DefinitionFormIndex.Node node, out bool inheritedSecret) &&
            !DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret));

    public bool SetValue(IReadOnlyList<string> path, JsonElement? value) => ApplyValue(path, value, clearDrafts: true);

    public DefinitionNode? GetEditorSchema(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ? node.Schema : null;
        }
    }

    public DefinitionNodeState? GetEditorState(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Facts.EditorState(path);
        }
    }

    public string? GetVariantTitle(IReadOnlyList<string> path, int index) => GetVariantTitle(DataPath(path), index);

    public string? GetVariantTitle(DefinitionPath path, int index)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                index < 0 || index >= node.Schema.Variants.Count) return null;
            DefinitionNode branch = node.Schema.Variants[index];
            JsonElement discriminator = branch.Properties[node.Schema.Discriminator!].Keywords.GetProperty("const");
            string fallback = branch.Keywords.TryGetProperty("title", out JsonElement title) ? title.GetString()!
                : discriminator.ValueKind == JsonValueKind.String ? discriminator.GetString()! : discriminator.GetRawText();
            return _localization.Text(Locale, node.SchemaPath + "/oneOf/" +
                index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/title", fallback);
        }
    }

    public DefinitionEditorPresentation? GetEditorPresentation(IReadOnlyList<string> path) =>
        GetEditorPresentation(DataPath(path));

    public DefinitionEditorPresentation? GetEditorPresentation(DefinitionPath path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _)) return null;
            JsonElement metadata = node.Metadata;
            JsonElement extension = metadata.TryGetProperty("x-configurator", out JsonElement configured) ? configured : default;
            DefinitionFieldPresentationMetadata schemaMeta = node.Schema.PresentationMetadata;
            bool requiresMasking = RequiresMaskingAt(path.Segments, literal: true);
            bool isDynamicMapEntry = node.SchemaPath.EndsWith("/additionalProperties", StringComparison.Ordinal);
            bool hasCopyableWireToken = path.Segments.Count > 0 && !(isDynamicMapEntry && requiresMasking);
            string wireToken = path.Segments.Count == 0
                ? Definition.ApplicationId
                : (isDynamicMapEntry && requiresMasking ? string.Empty : path.Segments[^1]);

            string? explicitTitle = Text("title", node.Title);
            bool hasExplicitTitle = node.Title is not null;
            string inferred = path.Segments.Count == 0
                ? Definition.Title
                : schemaMeta.DisplayRawKey
                    ? wireToken
                    : DefinitionDisplayText.Humanize(wireToken.Length > 0 ? wireToken : "entry");
            string displayLabel = explicitTitle ?? inferred;

            WorkspaceNamingMode namingMode = _view.PresentationOptions.NamingMode;
            string activeTitle = namingMode == WorkspaceNamingMode.Technical && !string.IsNullOrEmpty(wireToken) && path.Segments.Count > 0
                ? wireToken
                : displayLabel;
            string secondaryLabel = namingMode == WorkspaceNamingMode.Technical
                ? displayLabel
                : (hasCopyableWireToken ? wireToken : string.Empty);

            var choices = new List<DefinitionChoicePresentation>();
            foreach (DefinitionChoice choice in node.Schema.OptionMetadata)
            {
                string choiceDisplay = choice.DisplayLabel;
                string? explanation = choice.Explanation;
                string? choiceDeprecation = choice.DeprecationGuidance;
                if (choice.MetadataIndex is { } index)
                {
                    string prefix = "x-configurator/choices/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (choice.HasExplicitLabel) choiceDisplay = Text(prefix + "/label", choiceDisplay) ?? choiceDisplay;
                    explanation = Text(prefix + "/explanation", explanation);
                    choiceDeprecation = Text(prefix + "/deprecationMessage", choiceDeprecation);
                }
                bool choiceDeprecated = choice.Deprecated || choiceDeprecation is not null;
                string choiceWire = choice.WireToken;
                string choiceActive = namingMode == WorkspaceNamingMode.Technical && !string.IsNullOrEmpty(choiceWire)
                    ? choiceWire
                    : choiceDisplay;
                choices.Add(new(choice.Value, choiceActive, explanation, choiceDisplay, choiceWire,
                    choiceDeprecated, choiceDeprecation, !requiresMasking));
            }

            string? units = Text("x-configurator/units", schemaMeta.Units);
            string? placeholder = null;
            if (!requiresMasking)
            {
                placeholder = Text("x-configurator/placeholder", schemaMeta.Placeholder);
                if (placeholder is null && metadata.TryGetProperty("examples", out JsonElement examples) &&
                    examples.ValueKind == JsonValueKind.Array && examples.GetArrayLength() > 0)
                {
                    JsonElement firstExample = examples[0];
                    placeholder = firstExample.ValueKind == JsonValueKind.String
                        ? firstExample.GetString()
                        : firstExample.GetRawText();
                }
            }
            string? defaultText = Text("x-configurator/defaultText", schemaMeta.DefaultText);
            string? ifOmittedDisplay = string.IsNullOrEmpty(defaultText)
                ? null
                : PresentationStrings.Format("IfOmittedFormat", Locale, defaultText);
            string? deprecationGuidance = Text("x-configurator/deprecationMessage", schemaMeta.DeprecationGuidance);
            bool isDeprecated = (metadata.TryGetProperty("deprecated", out JsonElement dep) && dep.ValueKind == JsonValueKind.True) ||
                deprecationGuidance is not null;
            string trueLabel = Text("x-configurator/booleanLabels/true", schemaMeta.TrueLabel)
                ?? PresentationStrings.Get("BooleanTrueDefault", Locale);
            string falseLabel = Text("x-configurator/booleanLabels/false", schemaMeta.FalseLabel)
                ?? PresentationStrings.Get("BooleanFalseDefault", Locale);

            IReadOnlyList<DefinitionGuidanceLink> links = extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("guidance", out JsonElement declaredHelp) &&
                declaredHelp.TryGetProperty("links", out JsonElement declaredLinks)
                    ? Array.AsReadOnly(declaredLinks.EnumerateArray().Select((link, index) =>
                        new DefinitionGuidanceLink(Text("x-configurator/guidance/links/" +
                            index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/title",
                            link.GetProperty("title").GetString())!, link.GetProperty("url").GetString()!)).ToArray())
                    : Array.Empty<DefinitionGuidanceLink>();

            (int controlledFields, int controlledSections) = path.Segments.Count > 0 &&
                _index!.DependentsByController.TryGetValue(DataPath(path.Segments), out var counts)
                    ? counts
                    : (0, 0);
            string? controllerSummary = controlledFields > 0
                ? PresentationStrings.Format("ControllerImpactSummary", Locale, controlledFields, controlledSections)
                : null;

            return new(
                node.Schema,
                activeTitle,
                Text("description", node.Help),
                Text("x-configurator/guidance/instructions", node.Instructions),
                units,
                choices.AsReadOnly(),
                links,
                namingMode,
                displayLabel,
                wireToken,
                secondaryLabel,
                hasExplicitTitle,
                hasCopyableWireToken,
                placeholder,
                defaultText,
                ifOmittedDisplay,
                isDeprecated,
                deprecationGuidance,
                schemaMeta.Widget,
                schemaMeta.Rows,
                schemaMeta.Syntax,
                new DefinitionBooleanLabels(trueLabel, falseLabel),
                schemaMeta.ItemTitlePointer,
                node.Schema.UniqueBy,
                node.Schema.OrderedPropertyNames,
                controlledFields,
                controlledSections,
                controllerSummary);

            string? Text(string suffix, string? fallback) => _localization.Text(Locale, node.SchemaPath + "/" + suffix, fallback);
        }
    }

    /// <summary>Chooses an admitted typed enum member by ordinal, never by its display label.</summary>
    public bool SelectChoice(IReadOnlyList<string> path, int index) => SelectChoice(DataPath(path), index);

    public bool SelectChoice(DefinitionPath path, int index)
    {
        using var mutation = BeginMutation(PresentationStrings.Get("UndoActionSelectOption", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) &&
            node.Metadata.TryGetProperty("enum", out JsonElement choices) &&
            index >= 0 && index < choices.GetArrayLength() && SetValue(path, choices[index]);
    }

    public bool SetNull(IReadOnlyList<string> path) => SetNull(DataPath(path));

    public bool SetNull(DefinitionPath path)
    {
        using var mutation = BeginMutation(PresentationStrings.Get("UndoActionEdit", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) && node.Schema.IsNullable &&
            SetValue(path, JsonSerializer.SerializeToElement<object?>(null));
    }

    /// <summary>Explicitly replaces a branch with its discriminator only; Core resolves declared initialization.</summary>
    public bool SelectVariant(IReadOnlyList<string> path, int index)
    {
        using var mutation = BeginMutation(PresentationStrings.Get("UndoActionChangeVariant", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
            index < 0 || index >= node.Schema.Variants.Count || node.Schema.Discriminator is not { } discriminator)
            return false;
        JsonElement value = node.Schema.Variants[index].Properties[discriminator].Keywords.GetProperty("const");
        if (ReadValue(path) is { } current &&
            ReferenceEquals(DefinitionValidator.SelectVariant(node.Schema, current), node.Schema.Variants[index]))
            return SetValue([.. path, discriminator], value);
        return SetValue(path, JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>
        { [discriminator] = value }));
    }

    /// <summary>Adopts a suggestion only through an explicit current-workspace edit.</summary>
    public bool ApplySuggestion(IReadOnlyList<string> path) => ApplySuggestion(DataPath(path));

    public bool ApplySuggestion(DefinitionPath path)
    {
        using var mutation = BeginMutation(PresentationStrings.Get("UndoActionEdit", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (TryEditorMetadata(path, out DefinitionFormIndex.Node linked, out _)
            && linked.Schema.PresentationMetadata.SuggestedSource is not null)
        {
            DefinitionTransitionTransferPlan review = ReviewSuggestion(path);
            return review.Candidates.Count == 1 && ApplyTransfer(review, [review.Candidates[0].Id]).Succeeded;
        }
        return TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) &&
            node.Metadata.TryGetProperty("x-configurator", out JsonElement metadata) &&
            metadata.TryGetProperty("suggestedValue", out JsonElement suggested) && SetValue(path, suggested);
    }

    private bool ApplyValue(IReadOnlyList<string> path, JsonElement? value, bool clearDrafts)
    {
        using (BeginMutation(PresentationStrings.Get(value is null ? "UndoActionOmit" : "UndoActionEdit", Locale)))
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ProfileBlocks(DataPath(path))) return false;
            if (IsRootLocation(path)) return ApplyRootValue(path, value, clearDrafts);
            if (path.Count == 0 || !TryMetadata(path, out _, out _) || value is not null && !CanMutate(path)) return false;
            DefinitionFieldInput field = Fields[_index!.FieldPositions[path[0]]];
            if (path.Count == 1)
            {
                bool accepted = field.TrySetValue(value, recordOrigin: clearDrafts);
                if (accepted && clearDrafts)
                {
                    field.ClearDrafts(path);
                    ClearRowIdentities(path);
                }
                if (accepted) RemoveSuppliedOmissions();
                return accepted;
            }
            JsonElement? existing = ReadValue([path[0]]);
            bool hasValue = !field.HasInputError([path[0]]) && existing is not null;
            if (!hasValue && value is null)
            {
                field.RecordOrigin(path, DefinitionAnswerOrigin.Omitted);
                field.ClearDrafts(path);
                OnInputChanged();
                return true;
            }
            JsonNode root = hasValue
                ? JsonNode.Parse(existing!.Value.GetRawText())! : field.Definition.Node.Type == DefinitionNodeType.Sequence
                    ? new JsonArray() : new JsonObject();
            JsonNode parent = root;
            for (int index = 1; index < path.Count - 1; index++)
            {
                string segment = path[index];
                if (parent is JsonArray array)
                {
                    if (!int.TryParse(segment, out int row) || row < 0 || row >= array.Count || array[row] is null) return false;
                    parent = array[row]!;
                }
                else if (parent is JsonObject mapping)
                {
                    if (mapping[segment] is null)
                    {
                        if (value is null)
                        {
                            field.RecordOrigin(path, DefinitionAnswerOrigin.Omitted);
                            field.ClearDrafts(path);
                            OnInputChanged();
                            return true;
                        }
                        if (!TryMetadataNode(path.Take(index + 1).ToArray(), out DefinitionFormIndex.Node container, out _))
                            return false;
                        if (!mapping.ContainsKey(segment) &&
                            TryMetadataNode(path.Take(index).ToArray(), out DefinitionFormIndex.Node parentSchema, out _) &&
                            mapping.Count >= MaximumMapEntries(parentSchema.Schema)) return false;
                        mapping[segment] = container.Schema.Type == DefinitionNodeType.Sequence
                            ? new JsonArray() : new JsonObject();
                    }
                    parent = mapping[segment]!;
                }
                else return false;
            }
            JsonNode? replacement = value is { } supplied ? JsonNode.Parse(supplied.GetRawText()) : null;
            if (parent is JsonObject obj)
            {
                if (value is not null && !obj.ContainsKey(path[^1]) &&
                    TryMetadataNode(path.Take(path.Count - 1).ToArray(), out DefinitionFormIndex.Node container, out _) &&
                    obj.Count >= MaximumMapEntries(container.Schema)) return false;
                if (value is null) obj.Remove(path[^1]);
                else obj[path[^1]] = replacement;
            }
            else if (parent is JsonArray items && int.TryParse(path[^1], out int row) && row >= 0 && row < items.Count)
            {
                if (value is null) return false;
                items[row] = replacement;
            }
            else return false;
            RecordMaterializedOrigins([path[0]]);
            if (!field.TrySetValue(JsonSerializer.SerializeToElement(root), recordOrigin: false)) return false;
            RemoveSuppliedOmissions();
            if (clearDrafts)
                field.RecordOrigin(path, value is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
            if (clearDrafts)
            {
                field.ClearDrafts(path);
                ClearRowIdentities(path);
            }
            return true;
        }
    }

    /// <summary>Records one deliberate draft edit or omission (null), including rejected or unchanged input, before notifying observers.</summary>
    public bool EditValue(IReadOnlyList<string> path, string? text)
    {
        using (BeginMutation(PresentationStrings.Get(text is null ? "UndoActionOmit" : "UndoActionEdit", Locale)))
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRootLocation(path)) return EditRootValue(path, text);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) || !CanMutate(path)) return false;
            if (text is not null && Facts.NeedsCorrection(path))
                for (int length = 1; length <= path.Count; length++)
                    _view.PromotedPaths.Add(GetEditorIdentity(path.Take(length).ToArray()));
            DefinitionFieldInput field = Fields[_index!.FieldPositions[path[0]]];
            long before = InputRevision;
            if (text is null)
            {
                bool omitted = SetValue(path, null);
                if (omitted && InputRevision == before) OnInputChanged();
                return omitted;
            }
            if (path.Count == 1)
            {
                field.Text = text;
                return field.InputDiagnosticCode is null && field.TryValue(out _);
            }
            bool accepted = DefinitionScalarEditing.TryParse(node.Type,
                text, node.Metadata, out JsonElement? value) && SetValue(path, value);
            field.SetDraftValidity(path, accepted);
            field.RetainDraft(path, accepted ? null : text);
            if (InputRevision == before) OnInputChanged();
            return accepted;
        }
    }

    private JsonElement? ReadValue(IReadOnlyList<string> path, DefinitionValidationResult? validation = null)
    {
        if (IsRootLocation(path))
        {
            DefinitionValidationResult resolved = validation ?? DefinitionValidator.Validate(Definition, Snapshot());
            return resolved.TryGetEffectiveValue(DataPath(path), out JsonElement effective) ? effective : null;
        }
        if (path.Count == 0 || !_index!.FieldPositions.TryGetValue(path[0], out int position) ||
            Fields[position].HasInputError([path[0]])) return null;
        return (validation ?? Facts.Validation).TryGetEffectiveValue(DataPath(path), out JsonElement value)
            ? value : null;
    }

    internal DefinitionPath DataPath(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            // Retirement clears field origins before releasing the immutable index.
            ObjectDisposedException.ThrowIf(_index is null, this);
            return _index!.DataPath(path);
        }
    }

    internal bool TryMetadata(IReadOnlyList<string> path, out JsonElement metadata, out bool inheritedSecret)
    {
        bool found = TryMetadataNode(path, out DefinitionFormIndex.Node node, out inheritedSecret);
        metadata = found ? node.Metadata : default;
        return found;
    }

    internal bool TryMetadataNode(IReadOnlyList<string> path, out DefinitionFormIndex.Node node,
        out bool inheritedSecret, DefinitionValidationResult? validation = null) =>
        TryMetadataNode(path, out node, out inheritedSecret, out _, validation);

    /// <summary>
    /// Also reports whether the configuration, an ancestor or a selected branch above the resolved node declares a
    /// non-public sensitivity. The node's own declaration and descendants are read from the node itself.
    /// </summary>
    internal bool TryMetadataNode(IReadOnlyList<string> path, out DefinitionFormIndex.Node node,
        out bool inheritedSecret, out bool inheritedProtected, DefinitionValidationResult? validation = null)
    {
        node = null!;
        inheritedSecret = DefinitionSecretPolicy.IsSecret(Definition.Configuration);
        inheritedProtected = DefinitionSecretPolicy.DeclaresProtected(Definition.Configuration);
        if (IsRootLocation(path)) return TryRootMetadata(path, out node, out inheritedSecret, out inheritedProtected, validation);
        if (path.Count == 0 || !_index!.Roots.TryGetValue(path[0], out var root)) return false;
        node = root;
        if (path.Count == 1) return true;
        var prefix = new List<string> { path[0] };
        foreach (string segment in path.Skip(1))
        {
            inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
            inheritedProtected |= DefinitionSecretPolicy.DeclaresProtected(node.Schema);
            if (node.Variants.Count > 0)
            {
                if (ReadValue(prefix, validation) is not { } supplied ||
                    DefinitionValidator.SelectVariant(node.Schema, supplied) is not { } branch) return false;
                node = node.Variants.Single(variant => ReferenceEquals(variant.Schema, branch));
                inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
                inheritedProtected |= DefinitionSecretPolicy.DeclaresProtected(node.Schema);
            }
            if (node.Item is { } item)
            {
                if (!int.TryParse(segment, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int index) ||
                    segment != index.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    ReadValue(prefix, validation) is not { ValueKind: JsonValueKind.Array } rows ||
                    index < 0 || index >= rows.GetArrayLength()) return false;
                node = item;
                prefix.Add(segment);
                continue;
            }
            if (!node.Properties.TryGetValue(segment, out var child))
            {
                if (node.Additional is not { } additional) return false;
                child = additional;
            }
            node = child;
            prefix.Add(segment);
        }
        return true;
    }

    private bool AllowsValue(FieldDefinition field, string text, JsonElement? candidate)
    {
        if (ProfileBlocks(DefinitionPath.FromSegments([field.Name]))) return false;
        string fieldId = field.Id;
        DefinitionFieldInput input = Fields[_index!.FieldPositions[fieldId]];
        bool fitsBudget = text.Length <= 65_536;
        string? rejection = fitsBudget ? null : "answer-budget-exceeded";
        if (fitsBudget)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach ((string name, JsonElement supplied) in _editing.Root)
                if (name != field.Name) values.Add(name, supplied);
            if (candidate is { } value) values[field.Name] = value;
            try
            {
                _ = new AnswerSnapshot(InputRevision, values, Omitted.Where(path =>
                    !values.TryGetValue(path.Segments[0], out JsonElement root) ||
                    !TrySuppliedValue(root, path.Segments.Skip(1), out _)));
            }
            catch (DefinitionException exception)
            {
                fitsBudget = false;
                rejection = exception.Code is "answer-limit" or "scalar-invalid" or "document-limit"
                    ? "answer-budget-exceeded" : "answer-invalid";
            }
        }
        input.SetInputDiagnostic(rejection);
        if (!fitsBudget) return false;
        if (SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets || candidate is null && text.Length == 0) return true;
        DefinitionNode schema = field.Node;
        bool inheritedSecret = DefinitionSecretPolicy.IsSecret(Definition.Configuration);
        if (DefinitionSecretPolicy.IsSecret(schema, inheritedSecret)) return false;
        if (field.Node.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) return true;
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            return !DefinitionSecretPolicy.ContainsSecret(schema, parsed.RootElement, inheritedSecret);
        }
        catch (JsonException) { return false; }
    }

    private void RemoveSuppliedOmissions() =>
        Omitted.RemoveWhere(path => path.Segments.Count > 0 && _editing.Root.TryGetValue(path.Segments[0], out JsonElement supplied) &&
            TrySuppliedValue(supplied, path.Segments.Skip(1), out _));
    public IReadOnlyList<DefinitionFieldInput> Fields => _editing.Fields;
    /// <summary>Returns the owned root editor and its immutable admitted schema by literal field identity.</summary>
    public (DefinitionFieldInput Input, DefinitionNode Schema) GetFieldEditor(string fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_index!.FieldPositions.TryGetValue(fieldId, out int position))
                throw new ArgumentException("field-invalid", nameof(fieldId));
            return (Fields[position], _index.Roots[fieldId].Schema);
        }
    }
    /// <summary>
    /// Returns admitted list-item metadata without serializing row answers.
    /// Row titles, required state and inherited sensitivity remain the renderer's responsibility.
    /// </summary>
    public DefinitionNode GetListItemMetadata(IReadOnlyList<string> path) => GetListItemMetadata(DataPath(path));

    public DefinitionNode GetListItemMetadata(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) || node.Item is null)
                throw new ArgumentException("field-invalid", nameof(path));
            return node.Item.Schema;
        }
    }
    public long InputRevision => _editing.InputRevision;
    public bool HasAnswerBudgetError => Fields.Any(input => input.HasAnswerBudgetError);
    public event EventHandler? InputChanged;

    public IReadOnlyList<DefinitionFieldInput> VisibleFields
    {
        get
        {
            DefinitionFormProjection projection = Projection;
            return Array.AsReadOnly(Fields.Where(input => projection.IsFieldVisible(input.Definition.Id)).ToArray());
        }
    }

    public bool HasInputErrors
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Facts.HasInputErrors;
            }
        }
    }

    /// <summary>Projects current draft validity for one editor path without exposing draft values.</summary>
    public bool HasInputError(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRootLocation(path))
                return _editing.RootInput.InvalidPaths.Any(existing => existing.SequenceEqual(path));
            return path.Count > 0 && _index!.FieldPositions.TryGetValue(path[0], out int position) &&
                Fields[position].HasInputError(path);
        }
    }

    /// <summary>Whether discarding would lose supplied values, explicit omissions or retained invalid drafts, including inactive drafts.</summary>
    public bool HasWorkspaceChanges
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Omitted.Count > 0 || _editing.Root.Count > 0 ||
                    Fields.Any(input => input.InputDiagnosticCode is not null) || _editing.RootInput.InvalidPaths.Count > 0;
            }
        }
    }

    public AnswerSnapshot Snapshot()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new AnswerSnapshot(InputRevision, _editing.Root, Omitted);
        }
    }

    public DefinitionValidationResult Validate()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Facts.Validation;
        }
    }

    public async Task<DefinitionImportResult> ImportAsync(string artifactId,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        AnswerSnapshot original;
        HashSet<string> replaceableDefaults;
        long generation;
        using (BeginMutation())
        {
            RetireImportComparison();
            if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                return new(false, null, "import-secret-consent-required");
            original = Snapshot();
            generation = _importGeneration;
            replaceableDefaults = ReplaceableImportDefaults();
        }
        if (cancellationToken.IsCancellationRequested)
            return new(false, null, "import-cancelled");
        ReadOnlyMemory<byte> bytes;
        try { bytes = await read(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "import-cancelled");
        }
        try
        {
            if (cancellationToken.IsCancellationRequested) return new(false, null, "import-cancelled");
            DefinitionImportResult result = DefinitionImport.Assess(Definition, artifactId, bytes, original, replaceableDefaults);
            if (!result.Succeeded) return result;
            return AdoptImportedResult(result, original.InputRevision, generation, cancellationToken);
        }
        finally
        {
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> buffer))
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
    }

    private DefinitionImportResult AdoptImportedResult(DefinitionImportResult result, long revision,
        long generation, CancellationToken cancellationToken)
    {
        using (BeginMutation(PresentationStrings.Get("UndoActionImport", Locale)))
        {
            if (!ImportIsCurrent(generation, revision, cancellationToken))
                return new(false, null, "import-stale");
            if (!HasImportStorage(result.Answers!))
                return new(false, null, "import-workspace-unsupported");
            if (result.Answers!.Values.Keys.Any(name => ProfileBlocks(DefinitionPath.FromSegments([name])))
                || result.Answers.Omitted.Any(ProfileBlocks))
                return new(false, null, "profile-replacement-required");
            if (_editing.RootInput.InvalidPaths.Count > 0)
                return new(false, null, "answer-conflict") { Diagnostic = new("answer-conflict", "") };
            DefinitionFieldInput? pendingDraft = Fields.FirstOrDefault(field =>
                result.ProvidedFields.Contains(field.Definition.Id) && field.InputDiagnosticCode is not null);
            if (pendingDraft is not null)
                return new(false, null, "answer-conflict")
                {
                    Diagnostic = new("answer-conflict", pendingDraft.Definition.Id)
                };
            IReadOnlyList<string> retiredBranches = ChangedBranchIdentities(result.Answers!.Values);
            foreach (DefinitionFieldInput field in Fields.Where(field => result.ProvidedFields.Contains(field.Definition.Id)))
                if (result.Answers!.Values.TryGetValue(field.Definition.Name, out JsonElement value))
                {
                    ClearRowIdentities([field.Definition.Id]);
                    field.SetImported(value);
                }
            foreach ((string name, JsonElement value) in result.Answers!.Values)
                if (!Fields.Any(field => field.Definition.Name == name))
                {
                    _editing.Root[name] = value.Clone();
                    RecordRootOrigin([name], DefinitionAnswerOrigin.Imported);
                }
            Omitted.Clear();
            Omitted.UnionWith(result.Answers!.Omitted);
            foreach (string identity in retiredBranches) _editing.RetireBranch(identity);
            OnInputChanged();
            return result;
        }
    }

    private void Edit(Func<bool> edit)
    {
        using (BeginMutation(PresentationStrings.Get("UndoActionEdit", Locale)))
        {
            if (_disposed) return;
            bool hadAdmissionError = Fields.Any(input => input.InputDiagnosticCode is not null);
            Dictionary<string, JsonElement>? original = _index!.Root.HasVariants ? _editing.Root.ToDictionary() : null;
            bool changed = edit();
            if (original is not null)
                foreach (string identity in ChangedBranchIdentities(_editing.Root, original))
                    _editing.RetireBranch(identity);
            if (changed || hadAdmissionError != Fields.Any(input => input.InputDiagnosticCode is not null)) OnInputChanged();
        }
    }

    private void OnInputChanged()
    {
        _discardBrowserProfileOutput?.Invoke();
        RetireProfilePreparation();
        if (_undoBaseline is not null && !_undoReplayActive && !_privacyClearingActive)
            _inputChangedInUndoScope = true;
        RetireSecretResult();
        RetireDocument();
        RetireImportComparison();
        _facts = null;
        _orderedEntries = null;
        _relevantEntries = null;
        _discovery = null;
        _projection = null;
        _promotionPending = true;
        _editing.AdvanceRevision();
        RetireOutputWorkflow(clearHistory: false);
        RetireTransferReviews();
        OnPropertyChanged(nameof(InputRevision));
        OnPropertyChanged(nameof(HasInputErrors));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
        OnPropertyChanged(nameof(VisibleFields));
        Notify(RaiseInputChanged);
        OnPropertyChanged(nameof(ConfigurationContext));
        OnPropertyChanged(nameof(ConfigurationScopes));
        OnPropertyChanged(nameof(EligibleArtifacts));
    }

    private void RaiseInputChanged()
    {
        List<Exception>? failures = null;
        foreach (EventHandler subscriber in InputChanged?.GetInvocationList() ?? [])
        {
            try { subscriber(this, EventArgs.Empty); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is not null) throw new AggregateException(failures);
    }

    public void Dispose()
    {
        using (BeginMutation())
        {
            if (_disposed) return;
            _disposed = true;
            RetireProfileSession();
            RetireOutputWorkflow(clearHistory: true);
            RetireTransferReviews();
            _undoHistory.Clear();
            _undoBaseline?.Retire();
            _undoBaseline = null;
            RetireSecretResult();
            NotifySecretSessions(consentChanged: true, formDisposed: true);
            RetireDocument();
            RetireImportComparison();
            _editing.Clear();
            Omitted.Clear();
            _facts = null;
            _index = null;
            _orderedEntries = null;
            _relevantEntries = null;
            _discovery = null;
            _projection = null;
            _promotionPending = false;
            _view.Clear();
            InputChanged = null;
            ViewChanged = null;
            ImportComparisonChanged = null;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) =>
        Notify(() => base.OnPropertyChanged(e));

    protected override void OnPropertyChanging(PropertyChangingEventArgs e) =>
        Notify(() => base.OnPropertyChanging(e));

    private void Notify(Action notification)
    {
        if (Monitor.IsEntered(_gate)) _notifications.Add(notification);
        else notification();
    }

    internal Mutation BeginMutation(string? undoLabel = null)
    {
        Monitor.Enter(_gate);
        if (++_mutationDepth == 1 && undoLabel is not null && !_disposed && !_undoReplayActive && !_privacyClearingActive)
        {
            _undoBaseline = _editing.Capture();
            _pendingUndoLabel = undoLabel;
            _inputChangedInUndoScope = false;
        }
        return new Mutation(this);
    }

    private void EndMutation()
    {
        List<Action>? notifications = null;
        try
        {
            if (--_mutationDepth == 0)
            {
                if (_undoBaseline is not null)
                {
                    if (_inputChangedInUndoScope && !_disposed && !_undoReplayActive && !_privacyClearingActive)
                    {
                        DefinitionAcceptedEditingState after = _editing.Capture();
                        _undoHistory.Record(_pendingUndoLabel ?? "Edit", _undoBaseline, after, Definition, Fields, IsSecretPathForUndo);
                        NotifyUndoPropertiesChanged();
                    }
                    else
                    {
                        _undoBaseline.Retire();
                    }
                    _undoBaseline = null;
                    _pendingUndoLabel = null;
                    _inputChangedInUndoScope = false;
                }
                if (_promotionPending && !_disposed)
                {
                    _promotionPending = false;
                    DefinitionFormFacts facts = Facts;
                    foreach (DefinitionPath path in facts.PromotionPaths)
                        if (TryEditorMetadata(path, out _, out _)) _view.PromotedPaths.Add(GetEditorIdentity(path));
                    ReconcileReviewSignatures(facts);
                }
                notifications = _notifications;
                _notifications = [];
            }
        }
        finally { Monitor.Exit(_gate); }
        if (notifications is null) return;
        List<Exception>? failures = null;
        foreach (Action notification in notifications)
        {
            try { notification(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is not null) throw new AggregateException(failures);
    }

    public bool CanUndo { get { lock (_gate) return !_disposed && _undoHistory.CanUndo; } }
    public bool CanRedo { get { lock (_gate) return !_disposed && _undoHistory.CanRedo; } }
    public string? LastUndoLabel { get { lock (_gate) return _disposed ? null : _undoHistory.LastUndoLabel; } }
    public string? LastRedoLabel { get { lock (_gate) return _disposed ? null : _undoHistory.LastRedoLabel; } }

    public WorkspacePresentationOptions PresentationOptions { get { lock (_gate) return _view.PresentationOptions; } }
    public WorkspaceDensity WorkspaceDensity => PresentationOptions.Density;
    public WorkspaceNamingMode WorkspaceNamingMode => PresentationOptions.NamingMode;
    public WorkspaceLayoutMode WorkspaceLayoutMode => PresentationOptions.LayoutMode;
    public string? CurrentSectionId { get { lock (_gate) return _view.CurrentSectionId; } }

    public bool Undo()
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_undoHistory.TryPopUndo(out DefinitionAcceptedEditingState? restored, out _) || restored is null)
                return false;
            AdoptUndoSnapshot(restored);
            return true;
        }
    }

    public bool Redo()
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_undoHistory.TryPopRedo(out DefinitionAcceptedEditingState? restored, out _) || restored is null)
                return false;
            AdoptUndoSnapshot(restored);
            return true;
        }
    }

    private void AdoptUndoSnapshot(DefinitionAcceptedEditingState target)
    {
        _undoReplayActive = true;
        try
        {
            DefinitionAcceptedEditingState previous = _editing.Capture();
            target.InputRevision = previous.InputRevision;
            target.NextRow = Math.Max(target.NextRow, previous.NextRow);
            _editing.Adopt(target);
            previous.Retire();
            OnInputChanged();
            foreach (DefinitionFieldInput field in Fields)
                field.NotifyDocumentChanged();
            NotifyUndoPropertiesChanged();
        }
        finally { _undoReplayActive = false; }
    }

    private void NotifyUndoPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(LastUndoLabel));
        OnPropertyChanged(nameof(LastRedoLabel));
    }

    public void SetWorkspaceDensity(WorkspaceDensity density)
    {
        if (!Enum.IsDefined(density)) throw new ArgumentOutOfRangeException(nameof(density));
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view.PresentationOptions.Density == density) return;
            _view.PresentationOptions = _view.PresentationOptions with { Density = density };
            OnPropertyChanged(nameof(PresentationOptions));
            OnPropertyChanged(nameof(WorkspaceDensity));
            OnViewChanged();
        }
    }

    public void SetWorkspaceNamingMode(WorkspaceNamingMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view.PresentationOptions.NamingMode == mode) return;
            _view.PresentationOptions = _view.PresentationOptions with { NamingMode = mode };
            OnPropertyChanged(nameof(PresentationOptions));
            OnPropertyChanged(nameof(WorkspaceNamingMode));
            OnViewChanged();
        }
    }

    public void SetWorkspaceLayoutMode(WorkspaceLayoutMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view.PresentationOptions.LayoutMode == mode) return;
            _view.PresentationOptions = _view.PresentationOptions with { LayoutMode = mode };
            OnPropertyChanged(nameof(PresentationOptions));
            OnPropertyChanged(nameof(WorkspaceLayoutMode));
            OnViewChanged();
        }
    }

    public bool RecordSectionOpened(string sectionId)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Sections.Any(section => section.Id == sectionId)) return false;
            _view.CurrentSectionId = sectionId;
            _view.OpenedSections.Add(sectionId);
            OnPropertyChanged(nameof(CurrentSectionId));
            OnViewChanged();
            return true;
        }
    }

    public bool SetSectionReviewed(string sectionId, bool reviewed = true)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Sections.Any(section => section.Id == sectionId)) return false;
            _view.CurrentSectionId = sectionId;
            _view.OpenedSections.Add(sectionId);
            if (reviewed)
            {
                HashSet<DefinitionPath> signature = Facts.SectionActivePaths.TryGetValue(sectionId, out HashSet<DefinitionPath>? paths)
                    ? [.. paths]
                    : [];
                _view.ReviewedSignatures[sectionId] = signature;
                _view.DowngradedSections.Remove(sectionId);
            }
            else
            {
                _view.ReviewedSignatures.Remove(sectionId);
            }
            OnPropertyChanged(nameof(CurrentSectionId));
            OnViewChanged();
            return true;
        }
    }

    public bool SetGroupExpanded(string sectionId, string groupId, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        ArgumentNullException.ThrowIfNull(groupId);
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DefinitionFormSection? section = Sections.FirstOrDefault(s => s.Id == sectionId);
            if (section is null || !section.Groups.Any(g => g.Id == groupId)) return false;
            _view.GroupExpansion[(sectionId, groupId)] = expanded;
            OnViewChanged();
            return true;
        }
    }

    public IReadOnlyList<long> GetListRowIdentities(IReadOnlyList<string> path) => GetListRowIdentities(DataPath(path));

    public IReadOnlyList<long> GetListRowIdentities(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence)
                return Array.Empty<long>();
            if (ReadEditorValue(path) is not { ValueKind: JsonValueKind.Array } array)
                return Array.Empty<long>();
            int count = array.GetArrayLength();
            if (count == 0) return Array.Empty<long>();
            string identity = GetEditorIdentity(path);
            var ids = new long[count];
            for (int index = 0; index < count; index++)
                ids[index] = _editing.RowIdentity(identity, count, index);
            return Array.AsReadOnly(ids);
        }
    }

    public IReadOnlyList<CompactPropertyRow> GetCompactRows(
        bool includeInactive = false,
        string? sectionId = null,
        string? query = null,
        DefinitionDiscoveryFilter filter = DefinitionDiscoveryFilter.All)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
            DefinitionFormProjection projection = Projection;
            DefinitionFormFacts facts = Facts;
            string redacted = PresentationStrings.Get("RedactedValue", Locale);
            var rows = new List<CompactPropertyRow>();
            int privateEntryCounter = 0;
            IReadOnlyDictionary<string, DefinitionPath> literalPaths = facts.EntryLiteralPaths.Value;
            foreach (DefinitionDiscoveryEntry entry in projection.Entries)
            {
                if (!entry.IsActive && !includeInactive) continue;
                if (sectionId is not null && !string.Equals(entry.SectionId, sectionId, StringComparison.Ordinal)) continue;
                if (!EntryMatchesFilter(entry, filter)) continue;

                DefinitionPath dataPath = literalPaths.TryGetValue(
                    DefinitionFormFacts.SafePathKey(entry.FieldId, entry.RelativePath), out DefinitionPath? literal)
                    ? literal
                    : DataPath([entry.FieldId, .. entry.RelativePath]);
                if (!TryEditorMetadata(dataPath, out DefinitionFormIndex.Node node, out _)) continue;
                // Skip pure static property containers when they have declared child fields; keep maps, sequences, variants, and leaves.
                if (node.Schema.Type == DefinitionNodeType.Mapping &&
                    node.Schema.AdditionalProperties is null &&
                    node.Schema.Properties.Count > 0 &&
                    node.Schema.Variants.Count == 0 &&
                    entry.RelativePath.Count > 0)
                    continue;

                DefinitionEditorPresentation? presentation = GetEditorPresentation(dataPath);
                if (presentation is null) continue;

                bool isDynamicMapEntry = node.SchemaPath.EndsWith("/additionalProperties", StringComparison.Ordinal);
                bool requiresMasking = RequiresMaskingAt(dataPath.Segments, literal: true);
                bool isSecretLeaf = entry.Sensitivity == FieldSensitivity.Secret;
                bool isSecretLocked = SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets && isSecretLeaf;

                string displayLabel = presentation.DisplayLabel;
                string wireToken = presentation.WireToken;
                string primaryLabel = presentation.Title;
                string secondaryLabel = presentation.SecondaryLabel;
                bool hasCopyableWire = presentation.HasCopyableWireToken;

                if (isDynamicMapEntry && requiresMasking)
                {
                    privateEntryCounter++;
                    displayLabel = PresentationStrings.Format("PrivateMapEntryLabel", Locale, privateEntryCounter);
                    wireToken = string.Empty;
                    primaryLabel = displayLabel;
                    secondaryLabel = string.Empty;
                    hasCopyableWire = false;
                }

                string? draft = isSecretLocked ? null : ReadDraftText(dataPath);
                JsonElement? val = isSecretLocked ? null :
                    (TryOrdinaryRootPath(dataPath.Segments, out string[] ordinary)
                        ? ReadValue(ordinary)
                        : ReadRootValue(dataPath.Segments));
                string valuePreview;
                if (entry.Sensitivity != FieldSensitivity.Public)
                {
                    bool hasValue = !isSecretLocked && (val is not null || !string.IsNullOrEmpty(draft) || entry.IsProvided);
                    valuePreview = hasValue ? redacted : string.Empty;
                }
                else if (draft is not null)
                {
                    valuePreview = draft;
                }
                else if (val is { } element)
                {
                    valuePreview = element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString() ?? string.Empty,
                        JsonValueKind.Array => $"{element.GetArrayLength()} items",
                        JsonValueKind.Object => $"{element.EnumerateObject().Count()} fields",
                        JsonValueKind.Null => "null",
                        _ => element.GetRawText()
                    };
                }
                else
                {
                    valuePreview = string.Empty;
                }

                DefinitionFieldValidation valFacts = GetFieldValidation(dataPath);
                bool isSchemaReadOnly = IsSchemaReadOnly(dataPath);
                var row = new CompactPropertyRow(
                    dataPath,
                    GetEditorIdentity(dataPath),
                    entry.SectionId,
                    entry.SectionTitle,
                    displayLabel,
                    wireToken,
                    primaryLabel,
                    secondaryLabel,
                    entry.Type,
                    entry.Sensitivity,
                    entry.IsRequired,
                    entry.IsActive,
                    entry.NeedsAttention,
                    entry.IsDeferred,
                    valFacts.CanEdit,
                    isSchemaReadOnly,
                    isSecretLocked,
                    entry.Origin,
                    valuePreview,
                    primaryLabel,
                    presentation.Widget,
                    presentation.Units,
                    presentation.Description,
                    presentation.DefaultText,
                    presentation.IfOmittedDisplay,
                    presentation.IsDeprecated,
                    presentation.DeprecationGuidance,
                    presentation.Choices,
                    hasCopyableWire,
                    entry.Sensitivity != FieldSensitivity.Public);

                if (row.MatchesQuery(query))
                    rows.Add(row);
            }
            return Array.AsReadOnly(rows.ToArray());
        }
    }

    /// <summary>Returns localized metadata and output bindings for one configuration path without reading answers.</summary>
    public DefinitionDiscoveryEntry? GetFieldMetadata(DefinitionPath dataPath)
    {
        ArgumentNullException.ThrowIfNull(dataPath);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DefinitionFormProjection projection = Projection;
            DefinitionFormFacts facts = Facts;
            IReadOnlyDictionary<string, DefinitionPath> literalPaths = facts.EntryLiteralPaths.Value;
            foreach (DefinitionDiscoveryEntry entry in projection.Entries)
            {
                if (literalPaths.TryGetValue(DefinitionFormFacts.SafePathKey(entry.FieldId, entry.RelativePath), out DefinitionPath? path) &&
                    path == dataPath)
                {
                    return entry;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Applies a scalar edit or explicit omission from a compact table row, resolving ordinary versus literal root paths.
    /// </summary>
    public bool EditCompactValue(DefinitionPath dataPath, string? text)
    {
        ArgumentNullException.ThrowIfNull(dataPath);
        string[] path = TryOrdinaryRootPath(dataPath.Segments, out string[] ordinary)
            ? ordinary
            : dataPath.Segments.ToArray();
        return EditValue(path, text);
    }

    public IReadOnlyList<WorkspaceCommandDescriptor> GetAvailableCommands(string? query = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            WorkspaceNavigationProjection nav = Projection.Navigation;
            WorkspaceCommandDescriptor[] all =
            [
                new(WorkspaceCommandId.Undo, PresentationStrings.Get("CommandUndoTitle", Locale),
                    PresentationStrings.Get("CommandUndoSubtitle", Locale), "Ctrl+Z", CanUndo),
                new(WorkspaceCommandId.Redo, PresentationStrings.Get("CommandRedoTitle", Locale),
                    PresentationStrings.Get("CommandRedoSubtitle", Locale), "Ctrl+Shift+Z", CanRedo),
                new(WorkspaceCommandId.SwitchToGuided, PresentationStrings.Get("CommandGuidedTitle", Locale),
                    PresentationStrings.Get("CommandGuidedSubtitle", Locale), "Alt+1", true),
                new(WorkspaceCommandId.SwitchToCompact, PresentationStrings.Get("CommandCompactTitle", Locale),
                    PresentationStrings.Get("CommandCompactSubtitle", Locale), "Alt+2", true),
                new(WorkspaceCommandId.SwitchToReview, PresentationStrings.Get("CommandReviewTitle", Locale),
                    PresentationStrings.Get("CommandReviewSubtitle", Locale), "Alt+3", true),
                new(WorkspaceCommandId.SwitchToFiles, PresentationStrings.Get("CommandFilesTitle", Locale),
                    PresentationStrings.Get("CommandFilesSubtitle", Locale), "Alt+4", true),
                new(WorkspaceCommandId.ToggleNamingMode, PresentationStrings.Get("CommandToggleNamingTitle", Locale),
                    PresentationStrings.Get("CommandToggleNamingSubtitle", Locale), "Ctrl+Shift+K", true),
                new(WorkspaceCommandId.ToggleInspector, PresentationStrings.Get("CommandToggleInspectorTitle", Locale),
                    PresentationStrings.Get("CommandToggleInspectorSubtitle", Locale), "F2", true),
                new(WorkspaceCommandId.OpenDocumentEditor, PresentationStrings.Get("CommandDocumentTitle", Locale),
                    PresentationStrings.Get("CommandDocumentSubtitle", Locale), "Ctrl+Shift+E",
                    SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets),
                new(WorkspaceCommandId.OpenShortcutsHelp, PresentationStrings.Get("CommandShortcutsTitle", Locale),
                    PresentationStrings.Get("CommandShortcutsSubtitle", Locale), "?", true),
                new(WorkspaceCommandId.JumpToNextError, PresentationStrings.Get("CommandNextErrorTitle", Locale),
                    PresentationStrings.Get("CommandNextErrorSubtitle", Locale), "F8",
                    nav.NextErrorSectionId is not null, nav.NextErrorSectionId),
                new(WorkspaceCommandId.JumpToNextRequired, PresentationStrings.Get("CommandNextRequiredTitle", Locale),
                    PresentationStrings.Get("CommandNextRequiredSubtitle", Locale), "F9",
                    nav.NextRequiredSectionId is not null, nav.NextRequiredSectionId),
                new(WorkspaceCommandId.JumpToNextUnvisited, PresentationStrings.Get("CommandNextUnvisitedTitle", Locale),
                    PresentationStrings.Get("CommandNextUnvisitedSubtitle", Locale), "F7",
                    nav.NextUnvisitedSectionId is not null, nav.NextUnvisitedSectionId)
            ];
            if (string.IsNullOrWhiteSpace(query)) return Array.AsReadOnly(all);
            string needle = query.Trim();
            return Array.AsReadOnly(all.Where(cmd =>
                cmd.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                cmd.Subtitle.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                cmd.ShortcutHint.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToArray());
        }
    }

    private static bool EntryMatchesFilter(DefinitionDiscoveryEntry entry, DefinitionDiscoveryFilter filter) => filter switch
    {
        DefinitionDiscoveryFilter.All => true,
        DefinitionDiscoveryFilter.Invalid => entry.NeedsAttention,
        DefinitionDiscoveryFilter.MissingRequired => entry.IsRequired && !entry.IsReady && !entry.IsDeferred,
        DefinitionDiscoveryFilter.Modified => entry.IsProvided ||
            entry.Origin is DefinitionAnswerOrigin.Explicit or DefinitionAnswerOrigin.Imported or DefinitionAnswerOrigin.Generated,
        DefinitionDiscoveryFilter.DeferredSecret => entry.IsDeferred,
        _ => true
    };

    private bool IsSecretPathForUndo(IReadOnlyList<string> path)
    {
        DefinitionPath literal = TryOrdinaryRootPath(path, out string[] ordinary) ? DataPath(ordinary) : DefinitionPath.FromSegments(path);
        return TryRootMetadata(literal.Segments, out DefinitionFormIndex.Node node, out bool inheritedSecret) &&
               DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret);
    }

    private bool IsProtectedPathForUndo(IReadOnlyList<string> path) =>
        RequiresMaskingAt(path, literal: true) ||
        (TryOrdinaryRootPath(path, out string[] ordinary) && RequiresMaskingAt(ordinary, literal: false));

    internal bool? TryGetGroupExpansion(string sectionId, string groupId) =>
        _view.GroupExpansion.TryGetValue((sectionId, groupId), out bool expanded) ? expanded : null;

    internal SectionReviewCoverage GetSectionCoverage(string sectionId, out bool hasNewlyRelevantSettings)
    {
        hasNewlyRelevantSettings = _view.DowngradedSections.Contains(sectionId);
        if (_view.ReviewedSignatures.ContainsKey(sectionId))
            return SectionReviewCoverage.Reviewed;
        return _view.OpenedSections.Contains(sectionId) ? SectionReviewCoverage.Opened : SectionReviewCoverage.NotVisited;
    }

    internal bool DefinitionHasUndecidedControllersFor(
        DefinitionFormSection section,
        DefinitionValidationResult validation)
    {
        foreach (string rootId in section.Fields)
        {
            if (!_index!.Roots.TryGetValue(rootId, out DefinitionFormIndex.Node? rootNode)) continue;
            if (HasUndecidedController(rootNode)) return true;
        }
        return false;

        bool HasUndecidedController(DefinitionFormIndex.Node node)
        {
            foreach (DefinitionPath controller in node.ControllerPaths)
            {
                if (validation.IsPathUndecided(controller))
                    return true;
            }
            return node.Properties.Values.Any(HasUndecidedController);
        }
    }

    private bool IsSchemaReadOnly(DefinitionPath dataPath)
    {
        if (Definition.Configuration.Keywords.TryGetProperty("readOnly", out JsonElement rootRo) &&
            rootRo.ValueKind == JsonValueKind.True)
            return true;
        for (int length = 1; length <= dataPath.Segments.Count; length++)
        {
            if (TryRootMetadata(dataPath.Segments.Take(length).ToArray(), out DefinitionFormIndex.Node owner, out _) &&
                owner.Metadata.TryGetProperty("readOnly", out JsonElement ro) && ro.ValueKind == JsonValueKind.True)
                return true;
        }
        return false;
    }

    private void ReconcileReviewSignatures(DefinitionFormFacts facts)
    {
        if (_view.ReviewedSignatures.Count == 0) return;
        foreach ((string sectionId, HashSet<DefinitionPath> reviewedPaths) in _view.ReviewedSignatures.ToArray())
        {
            if (!facts.SectionActivePaths.TryGetValue(sectionId, out HashSet<DefinitionPath>? currentActive))
                continue;
            if (currentActive.Any(path => !reviewedPaths.Contains(path)))
            {
                _view.ReviewedSignatures.Remove(sectionId);
                _view.OpenedSections.Add(sectionId);
                _view.DowngradedSections.Add(sectionId);
                _projection = null;
            }
        }
    }

    internal readonly struct Mutation(DefinitionFormModel form) : IDisposable
    {
        public void Dispose() => form.EndMutation();
    }
}
