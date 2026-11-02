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
            : [new DefinitionFormExperience("configure", "Configure", FieldDisclosure.Guided,
                "sections", Sections.Select(section => section.Id), null)]);
        _view = new(definition.Presentation?.DefaultExperience ?? "configure")
        {
            Locale = _localization.DefaultLocale,
            EditorVisibility = editorVisibility
        };
        _index = new DefinitionFormIndex(definition, Sections, Experiences);
    }

    public Definition Definition { get; }
    public IReadOnlyList<DefinitionFormExperience> Experiences { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public IReadOnlyList<DefinitionFormSection> DisplaySections => Array.AsReadOnly(Sections.Select(section =>
    {
        int index = Definition.Presentation?.Sections.ToList().FindIndex(item => item.Id == section.Id) ?? -1;
        string prefix = "/presentation/sections/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return index < 0 ? new DefinitionFormSection(section.Id, PresentationStrings.Get("Configure", Locale),
            section.Description, section.Fields, section.Disclosure) : new DefinitionFormSection(section.Id,
            _localization.Text(Locale, prefix + "/title", section.Title)!,
            _localization.Text(Locale, prefix + "/description", section.Description), section.Fields, section.Disclosure);
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
            && SetValue([DefinitionProfile.FieldId(selector.Location.Segments[0])], JsonSerializer.SerializeToElement(value));
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
                return Array.AsReadOnly(Facts.InputDiagnostics.Select(SafeInputDiagnostic).ToArray());
            }
        }
    }

    /// <summary>Clears protected roots and returns whether one invalidating revision was published, even if already empty.</summary>
    public bool ClearPrivateState()
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetireImportComparison();
            bool hasProtectedRoots = false;
            RetireDocument();
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
                        if (field.Definition.Sensitivity == FieldSensitivity.Secret) field.Clear();
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

    public string? GetVariantTitle(IReadOnlyList<string> path, int index)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) ||
                index < 0 || index >= node.Schema.Variants.Count) return null;
            DefinitionNode branch = node.Schema.Variants[index];
            JsonElement discriminator = branch.Properties[node.Schema.Discriminator!].Keywords.GetProperty("const");
            string fallback = branch.Keywords.TryGetProperty("title", out JsonElement title) ? title.GetString()!
                : discriminator.ValueKind == JsonValueKind.String ? discriminator.GetString()! : discriminator.GetRawText();
            return _localization.Text(Locale, node.SchemaPath + "/oneOf/" +
                index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/title", fallback);
        }
    }

    public DefinitionEditorPresentation? GetEditorPresentation(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node node, out _)) return null;
            JsonElement metadata = node.Metadata;
            JsonElement extension = metadata.TryGetProperty("x-configurator", out JsonElement configured) ? configured : default;
            var choices = new List<DefinitionChoicePresentation>();
            if (metadata.TryGetProperty("enum", out JsonElement values))
                foreach (JsonElement value in values.EnumerateArray())
                {
                    string label = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
                    string? explanation = null;
                    if (extension.ValueKind == JsonValueKind.Object && extension.TryGetProperty("choices", out JsonElement labels))
                    {
                        int index = 0;
                        foreach (JsonElement declared in labels.EnumerateArray())
                        {
                            if (JsonElement.DeepEquals(declared.GetProperty("value"), value))
                            {
                                string prefix = "x-configurator/choices/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                label = Text(prefix + "/label", declared.GetProperty("label").GetString())!;
                                explanation = Text(prefix + "/explanation",
                                    declared.TryGetProperty("explanation", out JsonElement detail) ? detail.GetString() : null);
                                break;
                            }
                            index++;
                        }
                    }
                    choices.Add(new(value, label, explanation));
                }
            string? units = extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("units", out JsonElement unit) ? unit.GetString() : null;
            IReadOnlyList<DefinitionGuidanceLink> links = extension.ValueKind == JsonValueKind.Object &&
                extension.TryGetProperty("guidance", out JsonElement declaredHelp) &&
                declaredHelp.TryGetProperty("links", out JsonElement declaredLinks)
                    ? Array.AsReadOnly(declaredLinks.EnumerateArray().Select((link, index) =>
                        new DefinitionGuidanceLink(Text("x-configurator/guidance/links/" +
                            index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/title",
                            link.GetProperty("title").GetString())!, link.GetProperty("url").GetString()!)).ToArray())
                    : Array.Empty<DefinitionGuidanceLink>();
            return new(node.Schema, Text("title", node.Title ?? path[^1])!,
                Text("description", node.Help), Text("x-configurator/guidance/instructions", node.Instructions),
                Text("x-configurator/units", units), choices.AsReadOnly(), links);

            string? Text(string suffix, string? fallback) => _localization.Text(Locale, node.SchemaPath + "/" + suffix, fallback);
        }
    }

    /// <summary>Chooses an admitted typed enum member by ordinal, never by its display label.</summary>
    public bool SelectChoice(IReadOnlyList<string> path, int index)
    {
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) &&
            node.Metadata.TryGetProperty("enum", out JsonElement choices) &&
            index >= 0 && index < choices.GetArrayLength() && SetValue(path, choices[index]);
    }

    public bool SetNull(IReadOnlyList<string> path)
    {
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) && node.Schema.IsNullable &&
            SetValue(path, JsonSerializer.SerializeToElement<object?>(null));
    }

    /// <summary>Explicitly replaces a branch with its discriminator only; Core resolves declared initialization.</summary>
    public bool SelectVariant(IReadOnlyList<string> path, int index)
    {
        using var mutation = BeginMutation();
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
    public bool ApplySuggestion(IReadOnlyList<string> path)
    {
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryMetadataNode(path, out DefinitionFormIndex.Node node, out _) &&
            node.Metadata.TryGetProperty("x-configurator", out JsonElement metadata) &&
            metadata.TryGetProperty("suggestedValue", out JsonElement suggested) && SetValue(path, suggested);
    }

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
            if (node.Variants.Count > 0)
            {
                if (ReadValue(prefix, validation) is not { } supplied ||
                    DefinitionValidator.SelectVariant(node.Schema, supplied) is not { } branch) return false;
                node = node.Variants.Single(variant => ReferenceEquals(variant.Schema, branch));
                inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
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
                _ = new AnswerSnapshot(InputRevision, values, Omitted.Select(path => DataPath(path.Segments)).Where(path =>
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
        Omitted.RemoveWhere(path => _index!.FieldPositions.TryGetValue(path.Segments[0], out int position) &&
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

    /// <summary>Whether discarding would lose supplied values, explicit omissions or retained invalid drafts, including inactive drafts.</summary>
    public bool HasWorkspaceChanges
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Omitted.Count > 0 || Fields.Any(input => input.Value is not null || input.InputDiagnosticCode is not null);
            }
        }
    }

    public AnswerSnapshot Snapshot()
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
            return new AnswerSnapshot(InputRevision, values, Omitted.Select(path => DataPath(path.Segments)));
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
        using (BeginMutation())
        {
            if (!ImportIsCurrent(generation, revision, cancellationToken))
                return new(false, null, "import-stale");
            if (!HasImportStorage(result.Answers!))
                return new(false, null, "import-workspace-unsupported");
            DefinitionFieldInput? pendingDraft = Fields.FirstOrDefault(field =>
                result.ProvidedFields.Contains(field.Definition.Id) && field.InputDiagnosticCode is not null);
            if (pendingDraft is not null)
                return new(false, null, "answer-conflict")
                {
                    Diagnostic = new("answer-conflict", pendingDraft.Definition.Id)
                };
            foreach (DefinitionFieldInput field in Fields.Where(field => result.ProvidedFields.Contains(field.Definition.Id)))
                if (result.Answers!.Values.TryGetValue(field.Definition.Name, out JsonElement value))
                {
                    ClearRowIdentities([field.Definition.Id]);
                    field.SetImported(value);
                }
            Omitted.Clear();
            Omitted.UnionWith(result.Answers!.Omitted.Select(path => DefinitionPath.FromSegments([
                Definition.Fields.Single(field => field.Name == path.Segments[0]).Id, .. path.Segments.Skip(1)])));
            OnInputChanged();
            return result;
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
            RetireSecretResult();
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
