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
    private long _inputRevision;
    private DefinitionValidationResult? _validationResult;
    private DefinitionFormProjection? _projection;
    private readonly HashSet<string> _promotedPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _sectionExpansion = new(StringComparer.Ordinal);
    private bool _disposed;

    public DefinitionFormModel(SetupDefinition definition,
        DefinitionSessionPrivacy sessionPrivacy = DefinitionSessionPrivacy.NativeComplete)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(sessionPrivacy)) throw new ArgumentOutOfRangeException(nameof(sessionPrivacy));
        SessionPrivacy = sessionPrivacy;
        Definition = definition;
        Fields = Array.AsReadOnly(definition.Fields.Select(field =>
            new DefinitionFieldInput(field, Edit, (text, value) => AllowsValue(field, text, value), Notify)).ToArray());
        Sections = Array.AsReadOnly(definition.Presentation is { } authored
            ? authored.Sections.Select(section => new DefinitionFormSection(section.Id, section.Title,
                section.Description, section.Fields, section.Disclosure)).ToArray()
            : definition.Fields.GroupBy(input => input.Group ?? "", StringComparer.Ordinal).Select((group, index) =>
                new DefinitionFormSection(group.Key.Length == 0 ? "configure" :
                    "group-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    group.Key.Length == 0 ? "Configure" : group.Key, null,
                    group.Select(input => input.Id))).ToArray());
        Experiences = Array.AsReadOnly(definition.Presentation is { } presentation
            ? presentation.Experiences.Select(experience => new DefinitionFormExperience(experience.Id,
                experience.Title, experience.InitialDisclosure, experience.Navigation, experience.Sections)).ToArray()
            : [new DefinitionFormExperience("configure", "Configure", SetupFieldDisclosure.Guided,
                "sections", Sections.Select(section => section.Id))]);
        SelectedExperienceId = definition.Presentation?.DefaultExperience ?? "configure";
    }

    public SetupDefinition Definition { get; }
    public IReadOnlyList<DefinitionFormExperience> Experiences { get; }
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public string SelectedExperienceId { get; private set; }
    public bool OptionalEditorsExpanded { get; private set; }
    public event EventHandler? ViewChanged;

    public bool SelectExperience(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Experiences.Any(experience => experience.Id == id)) return false;
        if (SelectedExperienceId == id) return true;
        SelectedExperienceId = id;
        OptionalEditorsExpanded = false;
        _sectionExpansion.Clear();
        _editorExpansion.Clear();
        _promotedPaths.Clear();
        OnPropertyChanged(nameof(SelectedExperienceId));
        OnViewChanged();
        return true;
    }

    public void SetAllSectionsExpanded(bool expanded)
    {
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        OptionalEditorsExpanded = expanded;
        _sectionExpansion.Clear();
        _editorExpansion.Clear();
        _promotedPaths.Clear();
        OnViewChanged();
    }

    public bool SetSectionExpanded(string sectionId, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(sectionId);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Experiences.Single(experience => experience.Id == SelectedExperienceId).Sections.Contains(sectionId))
            return false;
        _sectionExpansion[sectionId] = expanded;
        foreach (string rootId in Sections.Single(section => section.Id == sectionId).Fields)
        {
            string identity = GetEditorIdentity([rootId]);
            foreach (string key in _editorExpansion.Keys.Where(key => key == identity ||
                key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
                _editorExpansion.Remove(key);
            _promotedPaths.RemoveWhere(key => key == identity || key.StartsWith(identity + "-", StringComparison.Ordinal));
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
            if (!TryMetadata(path, out JsonElement metadata, out _)) return false;
            DefinitionValidationResult validation = Validate();
            if (!validation.Fields.Any(state => state.Id == path[0] && state.IsActive)) return false;
            string[] relative = path.Skip(1).ToArray();
            bool needsCorrection = validation.Nodes.Any(node => node.FieldId == path[0] &&
                node.RelativePath.Take(relative.Length).SequenceEqual(relative) &&
                (node.HasError || node.IsObligation && node.IsMissing)) ||
                InputDiagnostics.Any(diagnostic => diagnostic.FieldId == path[0] &&
                    diagnostic.RelativePath.Take(relative.Length).SequenceEqual(relative));
            string key = GetEditorIdentity(path);
            if (needsCorrection) _promotedPaths.Add(key);
            if (needsCorrection || _promotedPaths.Contains(key) || ContainsEssentialDecision(metadata)) return true;
            if (TryLocalExpansion(path, out bool locallyExpanded))
                return locallyExpanded || validation.Nodes.Any(node => node.FieldId == path[0] &&
                    node.RelativePath.Take(relative.Length).SequenceEqual(relative) && node.IsRequired);
            DefinitionFormExperience experience = Experiences.Single(item => item.Id == SelectedExperienceId);
            DefinitionFormSection? section = Sections
                .FirstOrDefault(item => item.Fields.Contains(path[0]));
            if (section is not null && !experience.Sections.Contains(section.Id)) return false;
            bool expanded = OptionalEditorsExpanded;
            if (section is not null && _sectionExpansion.TryGetValue(section.Id, out bool sectionExpanded))
                expanded = sectionExpanded;
            if (expanded) return true;
            SetupFieldDisclosure disclosure = metadata.TryGetProperty("disclosure", out JsonElement declared)
                ? declared.GetString() switch
                {
                    "essential" => SetupFieldDisclosure.Essential,
                    "advanced" => SetupFieldDisclosure.Advanced,
                    _ => SetupFieldDisclosure.Guided
                } : SetupFieldDisclosure.Guided;
            return disclosure == SetupFieldDisclosure.Essential || disclosure <= experience.InitialDisclosure &&
                (section is null || section.Disclosure <= experience.InitialDisclosure);
        }
    }

    private static bool ContainsEssentialDecision(JsonElement metadata) =>
        metadata.TryGetProperty("disclosure", out JsonElement disclosure) && disclosure.GetString() == "essential" ||
        metadata.TryGetProperty("properties", out JsonElement properties) &&
        properties.EnumerateArray().Any(ContainsEssentialDecision);

    private void OnViewChanged()
    {
        _projection = null;
        OnPropertyChanged(nameof(Projection));
        OnPropertyChanged(nameof(VisibleFields));
        OnPropertyChanged(nameof(OptionalEditorsExpanded));
        Notify(() => ViewChanged?.Invoke(this, EventArgs.Empty));
    }
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
    public DefinitionDiscoverySelection DiscoverySelection { get; private set; } = new();
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
            DiscoverySelection = selection;
            OnPropertyChanged(nameof(DiscoverySelection));
            OnPropertyChanged(nameof(DiscoveryQuery));
            OnViewChanged();
        }
    }

    public void ClearDiscoveryFilters() => SetDiscoverySelection(new(DiscoveryQuery));
    public void ClearDiscoveryQuery() => SetDiscovery(string.Empty);
    public void ResetDiscovery() => SetDiscoverySelection(new());

    public IReadOnlyList<DefinitionDiagnostic> InputDiagnostics
    {
        get
        {
            var active = Validate().Fields.Where(state => state.IsActive)
                .Select(state => state.Id).ToHashSet(StringComparer.Ordinal);
            return Array.AsReadOnly(Fields.Where(input => active.Contains(input.Definition.Id))
                .SelectMany(input => input.InputDiagnostics).ToArray());
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

    public DefinitionSessionPrivacy SessionPrivacy { get; private set; }
    public void SetSecretEntryEnabled(bool enabled)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (SessionPrivacy == DefinitionSessionPrivacy.NativeComplete) return;
            DefinitionSessionPrivacy next = enabled ? DefinitionSessionPrivacy.BrowserSecretsEnabled
                : DefinitionSessionPrivacy.BrowserNoSecrets;
            if (SessionPrivacy == next) return;
            SessionPrivacy = next;
            if (!enabled)
                foreach (DefinitionFieldInput field in Fields)
                {
                    field.ClearDeniedDrafts(CanEdit);
                    if (field.Value is not { } value)
                    {
                        if (field.Definition.Sensitivity == SetupFieldSensitivity.Secret) field.Clear();
                        continue;
                    }
                    JsonNode? retained = DefinitionSecretPolicy.RemoveSecrets(field.Definition.Constraints,
                        JsonNode.Parse(value.GetRawText()));
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
            return TryMetadata(path, out JsonElement metadata, out bool inheritedSecret) &&
                !DefinitionSecretPolicy.IsSecret(metadata, inheritedSecret);
        }
    }

    public bool SetValue(IReadOnlyList<string> path, JsonElement? value) => ApplyValue(path, value, clearDrafts: true);

    private bool ApplyValue(IReadOnlyList<string> path, JsonElement? value, bool clearDrafts)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (path.Count == 0 || !TryMetadata(path, out _, out _) || value is not null && !CanEdit(path)) return false;
            DefinitionFieldInput field = Fields.Single(item => item.Definition.Id == path[0]);
            if (path.Count == 1)
            {
                bool accepted = field.TrySetValue(value, recordOrigin: clearDrafts);
                if (accepted && clearDrafts)
                {
                    field.ClearDrafts(path);
                    ClearRowIdentities(path);
                }
                return accepted;
            }
            bool hasValue = field.TryValue(out JsonElement? existing) && existing is not null;
            if (!hasValue && value is null)
            {
                if (field.ClearDrafts(path)) OnInputChanged();
                return true;
            }
            JsonNode root = hasValue
                ? JsonNode.Parse(existing!.Value.GetRawText())! : field.Definition.Type == "list" ? new JsonArray() : new JsonObject();
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
                            if (field.ClearDrafts(path)) OnInputChanged();
                            return true;
                        }
                        mapping[segment] = new JsonObject();
                    }
                    parent = mapping[segment]!;
                }
                else return false;
            }
            JsonNode? replacement = value is { } supplied ? JsonNode.Parse(supplied.GetRawText()) : null;
            if (parent is JsonObject obj)
            {
                if (replacement is null) obj.Remove(path[^1]);
                else obj[path[^1]] = replacement;
            }
            else if (parent is JsonArray items && int.TryParse(path[^1], out int row) && row >= 0 && row < items.Count)
                items[row] = replacement;
            else return false;
            if (!field.TrySetValue(JsonSerializer.SerializeToElement(root), recordOrigin: false)) return false;
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
            if (!TryMetadata(path, out JsonElement metadata, out _) || !CanEdit(path)) return false;
            DefinitionFieldInput field = Fields.Single(item => item.Definition.Id == path[0]);
            long before = _inputRevision;
            if (text is null)
            {
                bool omitted = SetValue(path, null);
                if (omitted && _inputRevision == before) OnInputChanged();
                return omitted;
            }
            if (path.Count == 1)
            {
                field.Text = text;
                return field.InputDiagnosticCode is null && field.TryValue(out _);
            }
            bool accepted = DefinitionScalarEditing.TryParse(metadata.GetProperty("type").GetString()!,
                text, metadata, out JsonElement? value) && SetValue(path, value);
            field.SetDraftValidity(path, accepted);
            field.RetainDraft(path, accepted ? null : text);
            if (_inputRevision == before) OnInputChanged();
            return accepted;
        }
    }

    public bool SupplyObject(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "object") return false;
            if (ReadNode(path) is JsonObject) return true;
            if (!SetValue(path, DefinitionCollectionItem.CreateObject(metadata))) return false;
            RecordMaterializedDefaults(path, metadata);
            return true;
        }
    }

    public bool SupplyList(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "list") return false;
            if (ReadNode(path) is JsonArray) return true;
            return SetValue(path, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        }
    }

    public bool AddListItem(IReadOnlyList<string> path)
    {
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out JsonElement metadata, out _) || metadata.GetProperty("type").GetString() != "list") return false;
            JsonArray items = ReadNode(path) as JsonArray ?? [];
            int maximum = metadata.TryGetProperty("maxItems", out JsonElement limit) ? limit.GetInt32() : 4096;
            if (items.Count >= maximum) return false;
            DefinitionAnswerOrigin[] origins = Enumerable.Range(0, items.Count)
                .Select(index => AnswerOrigin([.. path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)])).ToArray();
            string type = metadata.GetProperty("itemType").GetString()!;
            items.Add(type == "object" ? JsonNode.Parse(DefinitionCollectionItem.CreateObject(metadata).GetRawText()) : type switch
            {
                "string" => JsonValue.Create(string.Empty),
                "boolean" => JsonValue.Create(false),
                _ => JsonValue.Create(0)
            });
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            string[] addedPath = [.. path, origins.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            Fields.Single(input => input.Definition.Id == path[0]).RecordOrigin(addedPath, DefinitionAnswerOrigin.Explicit);
            if (TryMetadata(addedPath, out JsonElement addedMetadata, out _))
                RecordMaterializedDefaults(addedPath, addedMetadata);
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
            string identity = GetEditorIdentity(path);
            EnsureRowIdentities(identity, items.Count);
            string removedIdentity = identity + "-row-" +
                _rowIdentities[identity][index].ToString(System.Globalization.CultureInfo.InvariantCulture);
            items.RemoveAt(index);
            if (!ApplyValue(path, JsonSerializer.SerializeToElement(items), clearDrafts: false)) return false;
            RetainRowOrigins(path, origins);
            _rowIdentities[identity].RemoveAt(index);
            foreach (string key in _rowIdentities.Keys.Where(key =>
                key.StartsWith(removedIdentity + "-", StringComparison.Ordinal)).ToArray())
                _rowIdentities.Remove(key);
            foreach (string key in _editorExpansion.Keys.Where(key => key == removedIdentity ||
                key.StartsWith(removedIdentity + "-", StringComparison.Ordinal)).ToArray())
                _editorExpansion.Remove(key);
            _promotedPaths.RemoveWhere(key => key == removedIdentity ||
                key.StartsWith(removedIdentity + "-", StringComparison.Ordinal));
            Fields.Single(field => field.Definition.Id == path[0]).RemoveItemDrafts(path, index);
            return true;
        }
    }

    private JsonNode? ReadNode(IReadOnlyList<string> path)
    {
        if (path.Count == 0 || Fields.FirstOrDefault(item => item.Definition.Id == path[0]) is not { } field ||
            !field.TryValue(out JsonElement? value) || value is null) return null;
        JsonNode? node = JsonNode.Parse(value.Value.GetRawText());
        foreach (string segment in path.Skip(1))
            node = node is JsonObject mapping ? mapping[segment] : node is JsonArray array && int.TryParse(segment, out int row)
                && row >= 0 && row < array.Count ? array[row] : null;
        return node;
    }

    internal bool TryMetadata(IReadOnlyList<string> path, out JsonElement metadata, out bool inheritedSecret)
    {
        metadata = default;
        inheritedSecret = false;
        if (path.Count == 0 || Fields.FirstOrDefault(item => item.Definition.Id == path[0]) is not { } field) return false;
        metadata = field.Definition.Constraints;
        var prefix = new List<string> { path[0] };
        foreach (string segment in path.Skip(1))
        {
            inheritedSecret |= DefinitionSecretPolicy.IsSecret(metadata);
            if (metadata.GetProperty("type").GetString() == "list")
            {
                if (!int.TryParse(segment, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int index) ||
                    segment != index.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    ReadNode(prefix) is not JsonArray rows || index < 0 || index >= rows.Count) return false;
                metadata = JsonSerializer.SerializeToElement(new
                {
                    type = metadata.GetProperty("itemType").GetString(),
                    properties = metadata.TryGetProperty("properties", out JsonElement properties) ? properties : JsonSerializer.SerializeToElement(Array.Empty<object>()),
                    sensitivity = "public"
                });
                prefix.Add(segment);
                continue;
            }
            if (!metadata.TryGetProperty("properties", out JsonElement children)) return false;
            JsonElement child = children.EnumerateArray().FirstOrDefault(item => item.GetProperty("id").GetString() == segment);
            if (child.ValueKind == JsonValueKind.Undefined) return false;
            metadata = child;
            prefix.Add(segment);
        }
        return true;
    }

    private bool AllowsValue(SetupFieldDefinition field, string text, JsonElement? candidate)
    {
        DefinitionFieldInput input = Fields.Single(item => item.Definition.Id == field.Id);
        bool fitsBudget = text.Length <= 65_536;
        string? rejection = fitsBudget ? null : "answer-budget-exceeded";
        if (fitsBudget)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (DefinitionFieldInput current in Fields)
                if (current.Definition.Id != field.Id && current.Value is { } supplied)
                    values.Add(current.Definition.Id, supplied);
            if (candidate is { } value) values[field.Id] = value;
            try { _ = new SetupAnswerSnapshot(_inputRevision, values); }
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
        JsonElement metadata = field.Constraints;
        if (DefinitionSecretPolicy.IsSecret(metadata)) return false;
        if (field.Type is not ("object" or "list")) return true;
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            return !DefinitionSecretPolicy.ContainsSecret(metadata, parsed.RootElement);
        }
        catch (JsonException) { return false; }
    }
    public IReadOnlyList<DefinitionFieldInput> Fields { get; }
    public long InputRevision => _inputRevision;
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
            var active = Validate().Fields
                .Where(state => state.IsActive)
                .Select(state => state.Id)
                .ToHashSet(StringComparer.Ordinal);
            return Fields.Any(input => active.Contains(input.Definition.Id) &&
                (input.InputDiagnosticCode is not null || !input.TryValue(out _)));
        }
    }

    /// <summary>Projects current draft validity for one editor path without exposing draft values.</summary>
    public bool HasInputError(IReadOnlyList<string> path)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return path.Count > 0 && Fields.Any(field => field.Definition.Id == path[0] && field.HasInputError(path));
        }
    }

    public SetupAnswerSnapshot Snapshot()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in Fields)
            {
                if (field.Value is { } supplied)
                    values.Add(field.Definition.Id, supplied);
            }
            return new SetupAnswerSnapshot(_inputRevision, values);
        }
    }

    public DefinitionValidationResult Validate()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _validationResult ??= DefinitionValidator.Validate(Definition, Snapshot());
        }
    }

    public async Task<DefinitionImportResult> ImportAsync(string artifactId,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        SetupAnswerSnapshot original = Snapshot();
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
            DefinitionImportResult result = DefinitionImport.Assess(Definition, artifactId, bytes, original);
            if (!result.Succeeded) return result;
            using (BeginMutation())
            {
                if (_disposed || cancellationToken.IsCancellationRequested || _inputRevision != original.InputRevision)
                    return new(false, null, "import-stale");
                if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets && Fields.Any(field =>
                    result.Answers!.Values.TryGetValue(field.Definition.Id, out JsonElement value) &&
                    DefinitionSecretPolicy.ContainsSecret(field.Definition.Constraints, value)))
                    return new(false, null, "import-secret-consent-required");
                _rowIdentities.Clear();
                foreach (DefinitionFieldInput field in Fields)
                    if (result.Answers!.Values.TryGetValue(field.Definition.Id, out JsonElement value))
                        field.SetImported(value);
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
        _validationResult = null;
        _projection = null;
        checked { _inputRevision++; }
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
            foreach (var field in Fields) field.Clear();
            _rowIdentities.Clear();
            _validationResult = null;
            _projection = null;
            _promotedPaths.Clear();
            _sectionExpansion.Clear();
            _editorExpansion.Clear();
            DiscoverySelection = new();
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
    private readonly Action<Action> _notify;
    private string _text;
    private JsonElement? _value;
    private readonly List<string[]> _invalidPaths = [];
    private readonly List<(string[] Path, string Text)> _draftTexts = [];
    private readonly List<(string[] Path, DefinitionAnswerOrigin Origin)> _origins = [];
    private DefinitionAnswerOrigin _origin;
    private string? _inputDiagnosticCode;

    internal DefinitionFieldInput(SetupFieldDefinition definition, Action<Func<bool>> changed,
        Func<string, JsonElement?, bool> allowsValue, Action<Action> notify)
    {
        Definition = definition;
        _changed = changed;
        _allowsValue = allowsValue;
        _notify = notify;
        _value = definition.Default?.Clone();
        _text = definition.Default is { } value ? Display(value) : string.Empty;
        _origin = definition.Default is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Defaulted;
    }

    public SetupFieldDefinition Definition { get; }
    public string Title => Definition.Title;
    public string? Help => Definition.Help;
    public bool RequiresMasking => Definition.Sensitivity != SetupFieldSensitivity.Public;
    public bool HasAnswerBudgetError => InputDiagnosticCode == "answer-budget-exceeded";
    public string? InputDiagnosticCode => _inputDiagnosticCode ?? (_invalidPaths.Count > 0 ? "input-type-invalid" : null);
    internal JsonElement? Value => _value;
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
        get => _text;
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
        if (value is { } typed && (Definition.Type == "object" && typed.ValueKind != JsonValueKind.Object ||
            Definition.Type == "list" && typed.ValueKind != JsonValueKind.Array)) return false;
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
        value = _value;
        return !_invalidPaths.Any(path => path.Length == 1);
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
        _origins.Where(existing => path.Take(existing.Path.Length).SequenceEqual(existing.Path))
            .OrderByDescending(existing => existing.Path.Length).Select(existing => existing.Origin)
            .FirstOrDefault(_origin);

    internal void RecordOrigin(IReadOnlyList<string> path, DefinitionAnswerOrigin origin, bool clearDescendants = true)
    {
        _origins.RemoveAll(existing => existing.Path.SequenceEqual(path) ||
            clearDescendants && existing.Path.Take(path.Count).SequenceEqual(path));
        if (path.Count == 1) _origin = origin;
        else _origins.Add((path.ToArray(), origin));
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
            case "list":
            case "object":
                if (text.Length == 0) return true;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        text, new JsonDocumentOptions { MaxDepth = 32 });
                    JsonValueKind required = Definition.Type == "list"
                        ? JsonValueKind.Array : JsonValueKind.Object;
                    if (document.RootElement.ValueKind != required) return false;
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
