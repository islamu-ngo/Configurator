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
    private readonly HashSet<DefinitionPath> _omitted = [];
    private DefinitionFormIndex? _index;
    private DefinitionFormFacts? _facts;
    private Lazy<IReadOnlyList<DefinitionDiscoveryEntry>>? _orderedEntries;
    private Lazy<IReadOnlyList<DefinitionDiscoveryEntry>>? _relevantEntries;
    private Lazy<DefinitionDiscoverySnapshot>? _discovery;
    private bool _promotionPending;
    private DefinitionFormProjection? _projection;
    private bool _disposed;

    public DefinitionFormModel(SetupDefinition definition,
        DefinitionSessionPrivacy sessionPrivacy = DefinitionSessionPrivacy.NativeComplete)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(sessionPrivacy)) throw new ArgumentOutOfRangeException(nameof(sessionPrivacy));
        Definition = definition;
        _editing = new(Array.AsReadOnly(definition.Fields.Select(field =>
            new DefinitionFieldInput(field, Edit, (text, value) => AllowsValue(field, text, value),
                () => ReadValue([field.Id]), RecordOverride, Notify)).ToArray()),
            sessionPrivacy);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<DefinitionFormSection>();
        if (definition.Presentation is { } authored)
            foreach (DefinitionSection section in authored.Sections)
                sections.Add(new(section.Id, section.Title, section.Description,
                    section.Fields.Where(assigned.Add), section.Disclosure));
        string[] unlisted = definition.Fields.Select(field => field.Id).Where(id => !assigned.Contains(id)).ToArray();
        string? fallback = null;
        if (unlisted.Length > 0 || sections.Count == 0)
        {
            fallback = "configure";
            for (int suffix = 1; sections.Any(section => section.Id == fallback); suffix++)
                fallback = "configure-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sections.Add(new(fallback, "Configure", null, unlisted));
        }
        Sections = Array.AsReadOnly(sections.ToArray());
        Experiences = Array.AsReadOnly(definition.Presentation is { } presentation
            ? presentation.Experiences.Select(experience => new DefinitionFormExperience(experience.Id,
                experience.Title, experience.InitialDisclosure, experience.Navigation,
                fallback is null ? experience.Sections : experience.Sections.Append(fallback),
                experience.Description)).ToArray()
            : [new DefinitionFormExperience("configure", "Configure", SetupFieldDisclosure.Guided,
                "sections", Sections.Select(section => section.Id), null)]);
        _view = new(definition.Presentation?.DefaultExperience ?? "configure");
        _index = new DefinitionFormIndex(definition, Sections, Experiences);
    }

    public SetupDefinition Definition { get; }
    public IReadOnlyList<DefinitionFormExperience> Experiences { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public string SelectedExperienceId => _view.SelectedExperienceId;
    public bool OptionalEditorsExpanded => _view.OptionalEditorsExpanded;
    public SetupFieldDisclosure? DisclosureOverride => _view.DisclosureOverride;
    public event EventHandler? ViewChanged;

    /// <summary>Selects optional detail independently of the authored experience without editing answers.</summary>
    public void SetDisclosureOverride(SetupFieldDisclosure? disclosure)
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

    private SetupFieldDisclosure EffectiveDisclosure =>
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

    public bool IsEditorVisible(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node indexed, out _)) return false;
            DefinitionFormFacts facts = Facts;
            if (!facts.IsActive(path[0])) return false;
            bool needsCorrection = facts.NeedsCorrection(path);
            if (needsCorrection || facts.HasRequiredDescendant(path) || indexed.HasEssentialDecision ||
                _view.PromotedPaths.Count > 0 && _view.PromotedPaths.Contains(GetEditorIdentity(path))) return true;
            if (_view.EditorExpansion.Count > 0 && TryLocalExpansion(path, out bool locallyExpanded))
                return locallyExpanded || facts.HasRequiredDescendant(path);
            DefinitionFormExperience experience = Experiences.Single(item => item.Id == SelectedExperienceId);
            DefinitionFormSection section = _index!.SectionsByField[path[0]];
            if (section is not null && !experience.Sections.Contains(section.Id)) return false;
            bool expanded = OptionalEditorsExpanded;
            if (section is not null && _view.SectionExpansion.TryGetValue(section.Id, out bool sectionExpanded))
                expanded = sectionExpanded;
            if (expanded) return true;
            SetupFieldDisclosure disclosure = indexed.Disclosure;
            return disclosure == SetupFieldDisclosure.Essential || disclosure <= EffectiveDisclosure &&
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

    internal IReadOnlyList<DefinitionFormSection> OrderedSections =>
        _index!.ExperienceSections[SelectedExperienceId];

    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> OrderedEntries =>
        _orderedEntries ??= OrderEntries(Facts.Entries, OrderedSections);

    private static Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> OrderEntries(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, IReadOnlyList<DefinitionFormSection> sections)
    {
        var order = sections.Select((section, position) => (section.Id, position))
            .ToDictionary(item => item.Id, item => item.position, StringComparer.Ordinal);
        return new(() => Array.AsReadOnly(entries.Value
            .Select(entry => entry.WithSectionOrder(order[entry.SectionId])).ToArray()));
    }

    internal Lazy<DefinitionDiscoverySnapshot> CurrentDiscovery =>
        _discovery ??= CreateDiscovery(OrderedEntries, DiscoverySelection);

    private static Lazy<DefinitionDiscoverySnapshot> CreateDiscovery(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, DefinitionDiscoverySelection selection) =>
        new(() => DefinitionDiscovery.Search(entries.Value, selection));

    internal Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> CurrentRelevantEntries =>
        _relevantEntries ??= SelectRelevantEntries(OrderedEntries, Facts.RelevantPaths, _index!);

    private static Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> SelectRelevantEntries(
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries, Lazy<IReadOnlySet<string>> paths, DefinitionFormIndex index) =>
        new(() => Array.AsReadOnly(entries.Value.Where(entry =>
            paths.Value.Contains(index.PathKey([entry.FieldId, .. entry.RelativePath]))).ToArray()));

    public DefinitionFormProjection Projection
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _projection ??= DefinitionFormProjection.Create(this);
            }
        }
    }
    public DefinitionDiscoverySelection DiscoverySelection => _view.DiscoverySelection;
    public string DiscoveryQuery => DiscoverySelection.Query;

    public void SetDiscovery(string query)
    {
        SetDiscoverySelection(new(query, DiscoverySelection.IncludeInactive, DiscoverySelection.Facets));
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
                return Facts.InputDiagnostics;
            }
        }
    }

    /// <summary>Clears protected roots and returns whether one invalidating revision was published, even if already empty.</summary>
    public bool ClearPrivateState()
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            bool hasProtectedRoots = false;
            foreach (DefinitionFieldInput field in Fields.Where(field => field.RequiresMasking))
            {
                hasProtectedRoots = true;
                field.Clear();
            }
            if (hasProtectedRoots) OnInputChanged();
            return hasProtectedRoots;
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
            _editing.Privacy = next;
            if (!enabled)
                foreach (DefinitionFieldInput field in Fields)
                {
                    field.ClearDeniedDrafts(CanEdit);
                    if (field.Value is not { } value)
                    {
                        if (field.Definition.Sensitivity == SetupFieldSensitivity.Secret) field.Clear();
                        continue;
                    }
                    JsonNode? retained = DefinitionSecretPolicy.RemoveSecrets(field.Definition.Node,
                        JsonNode.Parse(value.GetRawText()), DefinitionSecretPolicy.IsSecret(Definition.Configuration));
                    if (retained is null) field.Clear();
                    else field.RetainValue(JsonSerializer.SerializeToElement(retained));
                }
            OnPropertyChanged(nameof(SessionPrivacy));
            OnInputChanged();
        }
    }

    public bool CanEdit(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets) return true;
            return TryMetadataNode(path, out DefinitionFormIndex.Node node, out bool inheritedSecret) &&
                !DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret);
        }
    }

    public bool SetValue(IReadOnlyList<string> path, JsonElement? value) => ApplyValue(path, value, clearDrafts: true);

    private bool ApplyValue(IReadOnlyList<string> path, JsonElement? value, bool clearDrafts)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (path.Count == 0 || !TryMetadata(path, out _, out _) || value is not null && !CanEdit(path)) return false;
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
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) || !CanEdit(path)) return false;
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

    public bool SupplyObject(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Mapping) return false;
            if (ReadNode(path) is JsonObject) return true;
            return SetValue(path, DefinitionCollectionItem.CreateObject(node.Schema));
        }
    }

    public bool SupplyList(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            if (ReadNode(path) is JsonArray) return true;
            return SetValue(path, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        }
    }

    public bool AddListItem(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                node.Schema.Type != DefinitionNodeType.Sequence) return false;
            JsonArray items = ReadNode(path) as JsonArray ?? [];
            int maximum = node.Metadata.TryGetProperty("maxItems", out JsonElement limit) ? limit.GetInt32() : 4096;
            if (items.Count >= maximum) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(index => AnswerOrigin([.. path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)])).ToArray();
            RecordMaterializedOrigins(path);
            DefinitionNode item = node.Schema.Items!;
            items.Add(item.Type switch
            {
                DefinitionNodeType.Mapping => new JsonObject(),
                DefinitionNodeType.Sequence => new JsonArray(),
                DefinitionNodeType.Text => JsonValue.Create(string.Empty),
                DefinitionNodeType.Boolean => JsonValue.Create(false),
                _ => JsonValue.Create(0)
            });
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            string[] addedPath = [.. path, origins.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            Fields[_index!.FieldPositions[path[0]]].RecordOrigin(addedPath, DefinitionAnswerOrigin.Explicit);
            return true;
        }
    }

    public bool RemoveListItem(IReadOnlyList<string> path, int index)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ReadNode(path) is not JsonArray items || index < 0 || index >= items.Count) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(row => AnswerOrigin([.. path, row.ToString(System.Globalization.CultureInfo.InvariantCulture)])).ToArray();
            DefinitionPath[] omissions = _omitted.ToArray();
            RecordMaterializedOrigins(path);
            string identity = GetEditorIdentity(path);
            string removedIdentity = identity + "-row-" +
                _editing.RowIdentity(identity, items.Count, index).ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.RemoveAt(index);
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            _editing.RemoveRow(identity, index, removedIdentity);
            _view.ClearEditorExpansion(removedIdentity, includeSelf: true);
            _view.ClearPromotions(removedIdentity);
            Fields[_index!.FieldPositions[path[0]]].RemoveItemDrafts(path, index);
            _omitted.Clear();
            foreach (DefinitionPath omitted in omissions)
            {
                if (omitted.Segments.Count <= path.Count || !omitted.Segments.Take(path.Count).SequenceEqual(path) ||
                    !omitted.TryGetArrayIndex(path.Count, out int row))
                {
                    _omitted.Add(omitted);
                    continue;
                }
                if (row == index) continue;
                _omitted.Add(row > index ? DefinitionPath.FromSegments([
                    .. omitted.Segments.Take(path.Count),
                    (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    .. omitted.Segments.Skip(path.Count + 1)]) : omitted);
            }
            return true;
        }
    }

    private JsonNode? ReadNode(IReadOnlyList<string> path)
    {
        return ReadValue(path) is { } value ? JsonNode.Parse(value.GetRawText()) : null;
    }

    private JsonElement? ReadValue(IReadOnlyList<string> path, DefinitionValidationResult? validation = null)
    {
        if (path.Count == 0 || !_index!.FieldPositions.TryGetValue(path[0], out int position) ||
            Fields[position].HasInputError([path[0]])) return null;
        return (validation ?? Facts.Validation).TryGetEffectiveValue(DataPath(path), out JsonElement value)
            ? value : null;
    }

    private DefinitionPath DataPath(IReadOnlyList<string> path) => DefinitionPath.FromSegments([
        Fields[_index!.FieldPositions[path[0]]].Definition.Name, .. path.Skip(1)]);

    internal bool TryMetadata(IReadOnlyList<string> path, out JsonElement metadata, out bool inheritedSecret)
    {
        bool found = TryMetadataNode(path, out DefinitionFormIndex.Node node, out inheritedSecret);
        metadata = found ? node.Metadata : default;
        return found;
    }

    internal bool TryMetadataNode(IReadOnlyList<string> path, out DefinitionFormIndex.Node node,
        out bool inheritedSecret, DefinitionValidationResult? validation = null)
    {
        node = null!;
        inheritedSecret = DefinitionSecretPolicy.IsSecret(Definition.Configuration);
        if (path.Count == 0 || !_index!.Roots.TryGetValue(path[0], out var root)) return false;
        node = root;
        if (path.Count == 1) return true;
        var prefix = new List<string> { path[0] };
        foreach (string segment in path.Skip(1))
        {
            inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
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
            if (!node.Properties.TryGetValue(segment, out var child)) return false;
            node = child;
            prefix.Add(segment);
        }
        return true;
    }

    private bool AllowsValue(SetupFieldDefinition field, string text, JsonElement? candidate)
    {
        string fieldId = field.Id;
        DefinitionFieldInput input = Fields[_index!.FieldPositions[fieldId]];
        bool fitsBudget = text.Length <= 65_536;
        string? rejection = fitsBudget ? null : "answer-budget-exceeded";
        if (fitsBudget)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            for (int position = 0; position < Fields.Count; position++)
                if (_index.FieldIds[position] != fieldId && Fields[position].Value is { } supplied)
                    values.Add(Fields[position].Definition.Name, supplied);
            if (candidate is { } value) values[field.Name] = value;
            try
            {
                _ = new SetupAnswerSnapshot(InputRevision, values, _omitted.Select(path => DataPath(path.Segments)).Where(path =>
                    !values.TryGetValue(path.Segments[0], out JsonElement root) ||
                    !TrySuppliedValue(root, path.Segments.Skip(1), out _)));
            }
            catch (SetupDefinitionException exception)
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
        _omitted.RemoveWhere(path => _index!.FieldPositions.TryGetValue(path.Segments[0], out int position) &&
            TrySuppliedValue(Fields[position].Value, path.Segments.Skip(1), out _));
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
    public DefinitionNode GetListItemMetadata(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) || node.Item is null)
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
            return path.Count > 0 && _index!.FieldPositions.TryGetValue(path[0], out int position) &&
                Fields[position].HasInputError(path);
        }
    }

    public SetupAnswerSnapshot Snapshot()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            for (int position = 0; position < Fields.Count; position++)
            {
                if (Fields[position].Value is { } supplied)
                    values.Add(Fields[position].Definition.Name, supplied);
            }
            return new SetupAnswerSnapshot(InputRevision, values, _omitted.Select(path => DataPath(path.Segments)));
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
        SetupAnswerSnapshot original;
        HashSet<string> replaceableDefaults;
        lock (_gate)
        {
            original = Snapshot();
            replaceableDefaults = Fields.Where(field => field.IsPristineDefault &&
                !_omitted.Any(path => path.Segments[0] == field.Definition.Id))
                .Select(field => field.Definition.Id).ToHashSet(StringComparer.Ordinal);
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
            using (BeginMutation())
            {
                if (_disposed || cancellationToken.IsCancellationRequested || InputRevision != original.InputRevision)
                    return new(false, null, "import-stale");
                if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                {
                    DefinitionFieldInput? denied = Fields.FirstOrDefault(field =>
                        result.Answers!.Values.TryGetValue(field.Definition.Name, out JsonElement value) &&
                        DefinitionSecretPolicy.ContainsSecret(field.Definition.Node, value,
                            DefinitionSecretPolicy.IsSecret(Definition.Configuration)));
                    if (denied is not null)
                        return new(false, null, "import-secret-consent-required")
                        {
                            Diagnostic = new("import-secret-consent-required", denied.Definition.Id)
                        };
                }
                foreach (DefinitionFieldInput field in Fields.Where(field => result.ProvidedFields.Contains(field.Definition.Id)))
                    if (result.Answers!.Values.TryGetValue(field.Definition.Name, out JsonElement value))
                    {
                        ClearRowIdentities([field.Definition.Id]);
                        field.SetImported(value);
                    }
                _omitted.Clear();
                _omitted.UnionWith(result.Answers!.Omitted.Select(path => DefinitionPath.FromSegments([
                    Definition.Fields.Single(field => field.Name == path.Segments[0]).Id, .. path.Segments.Skip(1)])));
                OnInputChanged();
                return result;
            }
        }
        finally
        {
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> buffer))
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
    }

    private void Edit(Func<bool> edit)
    {
        using (BeginMutation())
        {
            if (_disposed) return;
            bool hadAdmissionError = Fields.Any(input => input.InputDiagnosticCode is not null);
            if (edit() || hadAdmissionError != Fields.Any(input => input.InputDiagnosticCode is not null)) OnInputChanged();
        }
    }

    private void OnInputChanged()
    {
        _facts = null;
        _orderedEntries = null;
        _relevantEntries = null;
        _discovery = null;
        _projection = null;
        _promotionPending = true;
        _editing.AdvanceRevision();
        OnPropertyChanged(nameof(InputRevision));
        OnPropertyChanged(nameof(HasInputErrors));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
        OnPropertyChanged(nameof(VisibleFields));
        Notify(RaiseInputChanged);
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
            _editing.Clear();
            _omitted.Clear();
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

    private Mutation BeginMutation()
    {
        Monitor.Enter(_gate);
        _mutationDepth++;
        return new Mutation(this);
    }

    private void EndMutation()
    {
        List<Action>? notifications = null;
        try
        {
            if (--_mutationDepth == 0)
            {
                if (_promotionPending && !_disposed)
                {
                    _promotionPending = false;
                    foreach (string[] path in Facts.PromotionPaths)
                        if (TryMetadata(path, out _, out _)) _view.PromotedPaths.Add(GetEditorIdentity(path));
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

    private readonly struct Mutation(DefinitionFormModel form) : IDisposable
    {
        public void Dispose() => form.EndMutation();
    }
}

public sealed class DefinitionFieldInput : ObservableObject
{
    private readonly Action<Func<bool>> _changed;
    private readonly Func<string, JsonElement?, bool> _allowsValue;
    private readonly Func<JsonElement?> _readEffective;
    private readonly Action<IReadOnlyList<string>, bool> _recordOverride;
    private readonly Action<Action> _notify;
    private string _text;
    private JsonElement? _value;
    private readonly List<string[]> _invalidPaths = [];
    private readonly List<(string[] Path, string Text)> _draftTexts = [];
    private readonly List<(string[] Path, DefinitionAnswerOrigin Origin)> _origins = [];
    private DefinitionAnswerOrigin _origin;
    private string? _inputDiagnosticCode;

    internal DefinitionFieldInput(SetupFieldDefinition definition, Action<Func<bool>> changed,
        Func<string, JsonElement?, bool> allowsValue, Func<JsonElement?> readEffective,
        Action<IReadOnlyList<string>, bool> recordOverride, Action<Action> notify)
    {
        Definition = definition;
        _changed = changed;
        _allowsValue = allowsValue;
        _readEffective = readEffective;
        _recordOverride = recordOverride;
        _notify = notify;
        _text = string.Empty;
        _origin = DefinitionAnswerOrigin.Omitted;
    }

    public SetupFieldDefinition Definition { get; }
    public string Title => Definition.Title;
    public string? Help => Definition.Help;
    public bool RequiresMasking => Definition.Sensitivity != SetupFieldSensitivity.Public;
    public bool HasAnswerBudgetError => InputDiagnosticCode == "answer-budget-exceeded";
    public string? InputDiagnosticCode => _inputDiagnosticCode ?? (_invalidPaths.Count > 0 ? "input-type-invalid" : null);
    internal JsonElement? Value => _value;
    internal bool IsPristineDefault => _value is null && _origin == DefinitionAnswerOrigin.Omitted
        && _origins.Count == 0 && InputDiagnosticCode is null;
    public IReadOnlyList<string> Choices =>
        Array.AsReadOnly(Definition.Choices.Select(Display).ToArray());

    internal IEnumerable<DefinitionDiagnostic> InputDiagnostics
    {
        get
        {
            if (_inputDiagnosticCode is not null)
                yield return new(_inputDiagnosticCode, Definition.Id);
            foreach (string[] path in _invalidPaths)
                if (_inputDiagnosticCode is null || path.Length > 1)
                    yield return new("input-type-invalid", Definition.Id, path.Skip(1).ToArray());
        }
    }

    public string Text
    {
        get => !HasInputError([Definition.Id]) && _readEffective() is { } effective ? Display(effective) : _text;
        set => _changed(() =>
        {
            bool valid = TryValue(value, out JsonElement? parsed);
            if (!_allowsValue(value, valid ? parsed : _value))
            {
                if (!valid) SetDraftValidity([Definition.Id], false);
                return true;
            }
            SetDraftValidity([Definition.Id], valid);
            if (valid)
            {
                ClearDrafts([Definition.Id]);
                _value = parsed?.Clone();
                RecordOrigin([Definition.Id], parsed is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
            }
            SetProperty(ref _text, value);
            return true;
        });
    }

    internal bool TrySetValue(JsonElement? value, bool recordOrigin = true)
    {
        if (value is { } typed && !(typed.ValueKind == JsonValueKind.Null && Definition.Node.IsNullable) &&
            (Definition.Node.Type == DefinitionNodeType.Mapping && typed.ValueKind != JsonValueKind.Object ||
            Definition.Node.Type == DefinitionNodeType.Sequence && typed.ValueKind != JsonValueKind.Array)) return false;
        string text = value is { } supplied ? Display(supplied) : string.Empty;
        bool accepted = false;
        _changed(() =>
        {
            accepted = _allowsValue(text, value);
            if (accepted)
            {
                _value = value?.Clone();
                if (recordOrigin) RecordOrigin([Definition.Id],
                    value is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
                SetInputDiagnostic(null);
                if (!SetProperty(ref _text, text, nameof(Text))) OnPropertyChanged(nameof(Text));
            }
            return true;
        });
        return accepted;
    }

    internal void SetImported(JsonElement value)
    {
        SetInputDiagnostic(null);
        _invalidPaths.Clear();
        _draftTexts.Clear();
        RecordOrigin([Definition.Id], DefinitionAnswerOrigin.Imported);
        RetainValue(value);
    }

    internal void RetainValue(JsonElement value)
    {
        _value = value.Clone();
        if (!SetProperty(ref _text, Display(value), nameof(Text))) OnPropertyChanged(nameof(Text));
    }

    internal void Clear()
    {
        _value = null;
        _invalidPaths.Clear();
        _draftTexts.Clear();
        RecordOrigin([Definition.Id], DefinitionAnswerOrigin.Omitted);
        if (!SetProperty(ref _text, string.Empty, nameof(Text))) OnPropertyChanged(nameof(Text));
        SetInputDiagnostic(null);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) =>
        _notify(() => base.OnPropertyChanged(e));

    protected override void OnPropertyChanging(PropertyChangingEventArgs e) =>
        _notify(() => base.OnPropertyChanging(e));

    public bool TryValue(out JsonElement? value)
    {
        bool valid = !HasInputError([Definition.Id]);
        value = valid ? _readEffective() : null;
        return valid;
    }

    internal bool HasInputError(IReadOnlyList<string> path) =>
        path.Count == 1 && _inputDiagnosticCode is not null || _invalidPaths.Any(existing => existing.SequenceEqual(path));

    internal void SetDraftValidity(IReadOnlyList<string> path, bool valid)
    {
        _invalidPaths.RemoveAll(existing => existing.SequenceEqual(path));
        if (!valid) _invalidPaths.Add(path.ToArray());
        OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal bool ClearDrafts(IReadOnlyList<string> path)
    {
        _draftTexts.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path));
        bool changed = _invalidPaths.RemoveAll(existing => existing.Take(path.Count).SequenceEqual(path)) > 0;
        if (changed) OnPropertyChanged(nameof(InputDiagnosticCode));
        return changed;
    }

    internal void ClearDeniedDrafts(Func<IReadOnlyList<string>, bool> canEdit)
    {
        _draftTexts.RemoveAll(existing => !canEdit(existing.Path));
        if (_invalidPaths.RemoveAll(path => !canEdit(path)) > 0) OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal string? DraftText(IReadOnlyList<string> path) =>
        path.Count == 1 && HasInputError(path) ? _text :
            _draftTexts.FirstOrDefault(existing => existing.Path.SequenceEqual(path)).Text;

    internal void RetainDraft(IReadOnlyList<string> path, string? text)
    {
        _draftTexts.RemoveAll(existing => existing.Path.SequenceEqual(path));
        if (text is not null && text.Length <= 65_536 &&
            _draftTexts.Sum(existing => existing.Text.Length) + text.Length <= 65_536)
            _draftTexts.Add((path.ToArray(), text));
    }

    internal DefinitionAnswerOrigin OriginAt(IReadOnlyList<string> path) =>
        path.Count == 1 || _origins.Count == 0 ? _origin :
        _origins.Where(existing => path.Take(existing.Path.Length).SequenceEqual(existing.Path))
            .OrderByDescending(existing => existing.Path.Length).Select(existing => existing.Origin)
            .FirstOrDefault(_origin);

    internal void RecordOrigin(IReadOnlyList<string> path, DefinitionAnswerOrigin origin, bool clearDescendants = true)
    {
        if (clearDescendants && origin != DefinitionAnswerOrigin.Defaulted)
            _recordOverride(path, origin == DefinitionAnswerOrigin.Omitted);
        _origins.RemoveAll(existing => existing.Path.SequenceEqual(path) ||
            clearDescendants && existing.Path.Take(path.Count).SequenceEqual(path));
        if (path.Count == 1) _origin = origin;
        else _origins.Add((path.ToArray(), origin));
    }

    internal void ForgetOrigin(IReadOnlyList<string> path)
    {
        _origins.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path));
        if (path.Count == 1) _origin = DefinitionAnswerOrigin.Omitted;
    }

    internal void RemoveItemDrafts(IReadOnlyList<string> path, int index)
    {
        static bool IsRow(string[] candidate, IReadOnlyList<string> prefix, int row) =>
            candidate.Length > prefix.Count && candidate.Take(prefix.Count).SequenceEqual(prefix) &&
            candidate[prefix.Count] == row.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _draftTexts.RemoveAll(existing => IsRow(existing.Path, path, index));
        _origins.RemoveAll(existing => IsRow(existing.Path, path, index));
        foreach (string[] existing in _draftTexts.Select(draft => draft.Path).Concat(_origins.Select(origin => origin.Path)))
            if (existing.Length > path.Count && existing.Take(path.Count).SequenceEqual(path) &&
                int.TryParse(existing[path.Count], out int row) && row > index)
                existing[path.Count] = (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _invalidPaths.RemoveAll(existing => existing.Length > path.Count &&
            existing.Take(path.Count).SequenceEqual(path) && existing[path.Count] == index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (string[] existing in _invalidPaths.Where(existing => existing.Length > path.Count &&
            existing.Take(path.Count).SequenceEqual(path)))
            if (int.TryParse(existing[path.Count], out int row) && row > index)
                existing[path.Count] = (row - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(InputDiagnosticCode));
    }

    internal void SetInputDiagnostic(string? code)
    {
        if (_inputDiagnosticCode == code) return;
        _inputDiagnosticCode = code;
        OnPropertyChanged(nameof(InputDiagnosticCode));
        OnPropertyChanged(nameof(HasAnswerBudgetError));
    }

    internal bool TryValue(string text, out JsonElement? value)
    {
        value = null;
        if (text.Length > 65_536) return false;
        switch (Definition.Type)
        {
            case "array":
            case "object":
                if (text.Length == 0) return true;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        text, new JsonDocumentOptions { MaxDepth = 32 });
                    JsonValueKind required = Definition.Node.Type == DefinitionNodeType.Sequence
                        ? JsonValueKind.Array : JsonValueKind.Object;
                    if (document.RootElement.ValueKind != required &&
                        !(document.RootElement.ValueKind == JsonValueKind.Null && Definition.Node.IsNullable)) return false;
                    value = document.RootElement.Clone();
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                return DefinitionScalarEditing.TryParse(Definition.Type, text, Definition.Constraints, out value);
        }
    }

    private static string Display(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}
