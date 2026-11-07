namespace Configurator.Presentation;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public enum DefinitionAnswerOrigin { Omitted, Defaulted, Imported, Explicit, Generated }

/// <summary>Controls ordinary disclosure only; activation, selected experience and edit authority remain authoritative.</summary>
public enum DefinitionEditorVisibilityPolicy { Authored, Direct }

public sealed partial class DefinitionFormModel
{
    public bool SetEditorExpanded(IReadOnlyList<string> path, bool expanded) => SetEditorExpanded(DataPath(path), expanded);

    public bool SetEditorExpanded(DefinitionPath path, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryEditorMetadata(path, out _, out _)) return false;
        string identity = GetEditorIdentity(path);
        _view.ClearEditorExpansion(identity, includeSelf: false);
        _view.EditorExpansion[identity] = expanded;
        if (!expanded)
            _view.ClearPromotions(identity);
        OnViewChanged();
        return true;
    }

    public bool IsEditorExpanded(IReadOnlyList<string> path) => IsEditorExpanded(DataPath(path));

    public bool IsEditorExpanded(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryEditorMetadata(path, out _, out _)) return false;
            if (TryLocalExpansion(path, out bool expanded)) return expanded;
            DefinitionFormSection? section = TryOrdinaryRootPath(path.Segments, out string[] ordinary)
                ? _index!.SectionsByField[ordinary[0]] : null;
            bool all = section is not null && _view.SectionExpansion.TryGetValue(section.Id, out bool sectionExpanded)
                ? sectionExpanded : OptionalEditorsExpanded;
            return all || EffectiveDisclosure == FieldDisclosure.Advanced;
        }
    }

    private bool TryLocalExpansion(DefinitionPath path, out bool expanded)
    {
        for (int length = path.Segments.Count; length >= 0; length--)
            if (_view.EditorExpansion.TryGetValue(GetEditorIdentity(DefinitionPath.FromSegments(path.Segments.Take(length))), out expanded))
                return true;
        expanded = false;
        return false;
    }

    /// <summary>Reads transient invalid editor text only through the current workspace custody fence.</summary>
    public string? ReadDraftText(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out _, out _) || !CanMutate(path)) return null;
            if (IsRootLocation(path))
                return _editing.RootInput.DraftTexts.FirstOrDefault(draft => draft.Path.SequenceEqual(path)).Text;
            return Fields[_index!.FieldPositions[path[0]]].DraftText(path);
        }
    }

    public string? ReadDraftText(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (TryOrdinaryRootPath(path.Segments, out string[] ordinary)) return ReadDraftText(ordinary);
            if (!TryRootMetadata(path.Segments, out DefinitionFormIndex.Node node, out bool inheritedSecret) ||
                SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets &&
                    DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret)) return null;
            return _editing.RootInput.DraftTexts.FirstOrDefault(draft => draft.Path.SequenceEqual(path.Segments)).Text;
        }
    }

    public bool HasInputError(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TryOrdinaryRootPath(path.Segments, out string[] ordinary) ? HasInputError(ordinary)
                : _editing.RootInput.InvalidPaths.Any(existing => existing.SequenceEqual(path.Segments));
        }
    }

    public DefinitionAnswerOrigin AnswerOrigin(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return AnswerOrigin(path, Facts.Validation);
        }
    }

    internal DefinitionAnswerOrigin AnswerOrigin(IReadOnlyList<string> path, DefinitionValidationResult validation)
    {
        if (!TryMetadataNode(path, out _, out _, validation) || ReadValue(path, validation) is null)
            return DefinitionAnswerOrigin.Omitted;
        if (IsRootLocation(path))
            return RootAnswerOrigin(path, validation);
        DefinitionFieldInput input = Fields[_index!.FieldPositions[path[0]]];
        if (!TrySuppliedValue(input.Value, path.Skip(1), out _)) return DefinitionAnswerOrigin.Defaulted;
        DefinitionAnswerOrigin origin = input.OriginAt(path);
        return origin == DefinitionAnswerOrigin.Omitted ? DefinitionAnswerOrigin.Defaulted : origin;
    }

    private static bool TrySuppliedValue(JsonElement? supplied, IEnumerable<string> segments, out JsonElement value)
    {
        value = supplied ?? default;
        if (supplied is null) return false;
        foreach (string segment in segments)
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out JsonElement child))
                value = child;
            else if (value.ValueKind == JsonValueKind.Array &&
                int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int row) &&
                segment == row.ToString(CultureInfo.InvariantCulture) && row >= 0 && row < value.GetArrayLength())
                value = value[row];
            else return false;
        return true;
    }

    private void RecordOverride(IReadOnlyList<string> path, bool omitted)
    {
        path = DataPath(path).Segments;
        if (omitted && Omitted.Any(existing =>
            path.Take(existing.Segments.Count).SequenceEqual(existing.Segments))) return;
        Omitted.RemoveWhere(existing => existing.Segments.Take(path.Count).SequenceEqual(path) ||
            path.Take(existing.Segments.Count).SequenceEqual(existing.Segments));
        if (omitted) Omitted.Add(DefinitionPath.FromSegments(path));
    }

    /// <summary>Removes the supplied override and suppression so Core may initialize this location again.</summary>
    public bool ResetValue(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadataNode(path, out _, out _) || !CanMutate(path)) return false;
        if (IsRootLocation(path))
            return ResetRootValue(path);
        DefinitionFieldInput input = Fields[_index!.FieldPositions[path[0]]];
        JsonElement? replacement = null;
        if (path.Count > 1 && input.Value is { } supplied)
        {
            JsonNode? root = JsonNode.Parse(supplied.GetRawText());
            JsonNode? parent = root;
            foreach (string segment in path.Skip(1).SkipLast(1))
            {
                if (parent is JsonObject mapping) parent = mapping[segment];
                else if (parent is JsonArray rows &&
                    int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int row) &&
                    row >= 0 && row < rows.Count) parent = rows[row];
                else { parent = null; break; }
            }
            if (parent is JsonArray) return false;
            if (parent is JsonObject owner) owner.Remove(path[^1]);
            replacement = JsonSerializer.SerializeToElement(root);
        }
        long before = InputRevision;
        if (!input.TrySetValue(replacement, recordOrigin: false)) return false;
        IReadOnlyList<string> dataPath = DataPath(path).Segments;
        Omitted.RemoveWhere(existing => existing.Segments.Take(dataPath.Count).SequenceEqual(dataPath));
        input.ForgetOrigin(path);
        input.ClearDrafts(path);
        ClearRowIdentities(path);
        if (InputRevision == before) OnInputChanged();
        return true;
    }

    /// <summary>Identifies a logical editor without incorporating answers; surviving rows retain their token.</summary>
    public string GetEditorIdentity(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out _, out _)) throw new ArgumentException("editor-path-invalid", nameof(path));
            static string Encode(string segment) => Convert.ToHexString(Encoding.UTF8.GetBytes(segment));
            bool root = IsRootLocation(path);
            string identity = root ? "root-" + Definition.FamilyId.ToString("N") : _index!.RootIdentities[path[0]];
            var prefix = root ? new List<string>() : new List<string> { path[0] };
            foreach (string segment in root ? path : path.Skip(1))
            {
                bool hasOwner = TryMetadataNode(prefix, out DefinitionFormIndex.Node owner, out _);
                if (hasOwner && owner.Variants.Count > 0)
                    identity += "-branch-" + _editing.BranchIdentity(identity).ToString(CultureInfo.InvariantCulture);
                if (hasOwner && owner.Item is not null && ReadValue(prefix) is { ValueKind: JsonValueKind.Array } rows)
                {
                    identity += "-row-" + _editing.RowIdentity(identity, rows.GetArrayLength(),
                        int.Parse(segment, CultureInfo.InvariantCulture))
                        .ToString(CultureInfo.InvariantCulture);
                }
                else identity += "-key-" + Encode(segment);
                prefix.Add(segment);
            }
            return identity;
        }
    }

    private void ClearRowIdentities(IReadOnlyList<string> path)
    {
        string identity = GetEditorIdentity(path);
        _editing.ClearRows(identity);
    }

    private void RetainRowOrigins(DefinitionPath path, DefinitionAnswerOrigin[] origins)
    {
        if (TryOrdinaryRootPath(path.Segments, out string[] ordinary))
        {
            RetainRowOrigins(ordinary, origins);
            return;
        }
        for (int index = 0; index < origins.Length; index++)
            RecordRootOrigin([.. path.Segments, index.ToString(CultureInfo.InvariantCulture)],
                origins[index], clearDescendants: false);
        RecordRootOrigin(path.Segments, DefinitionAnswerOrigin.Explicit, clearDescendants: false);
    }

    private void RetainRowOrigins(string[] path, DefinitionAnswerOrigin[] origins)
    {
        DefinitionFieldInput input = Fields.Single(item => item.Definition.Id == path[0]);
        for (int index = 0; index < origins.Length; index++)
            input.RecordOrigin([.. path, index.ToString(CultureInfo.InvariantCulture)], origins[index], clearDescendants: false);
        input.RecordOrigin(path, DefinitionAnswerOrigin.Explicit, clearDescendants: false);
    }

    private void RecordMaterializedOrigins(string[] path)
    {
        DefinitionValidationResult validation = Facts.Validation;
        DefinitionFieldInput input = Fields[_index!.FieldPositions[path[0]]];
        foreach (DefinitionNodeState node in validation.Nodes.Where(node => node.FieldId == path[0] && node.IsProvided))
        {
            string[] location = [node.FieldId, .. node.RelativePath];
            if (location.Take(path.Length).SequenceEqual(path) &&
                AnswerOrigin(location, validation) == DefinitionAnswerOrigin.Defaulted)
                input.RecordOrigin(location, DefinitionAnswerOrigin.Defaulted, clearDescendants: false);
        }
    }

    private void RecordMaterializedOrigins(DefinitionPath path)
    {
        if (TryOrdinaryRootPath(path.Segments, out string[] ordinary))
        {
            RecordMaterializedOrigins(ordinary);
            return;
        }
        DefinitionValidationResult validation = Facts.Validation;
        foreach (DefinitionNodeState node in validation.Nodes)
            if (node.IsProvided && TryCorrectionPath(node.FieldId, node.Location, validation, out var resolved))
            {
                DefinitionPath location = node.FieldId.Length == 0 ? DefinitionPath.FromSegments(resolved) : DataPath(resolved);
                if (location.Segments.Take(path.Segments.Count).SequenceEqual(path.Segments) &&
                    RootAnswerOrigin(location.Segments, validation) == DefinitionAnswerOrigin.Defaulted)
                    RecordRootOrigin(location.Segments, DefinitionAnswerOrigin.Defaulted, clearDescendants: false);
            }
    }

    private DefinitionDiagnostic SafeInputDiagnostic(DefinitionDiagnostic diagnostic)
    {
        if (diagnostic.FieldId.Length == 0) return diagnostic;
        var segments = new List<DefinitionLocationSegment>();
        var path = new List<string> { diagnostic.FieldId };
        foreach (string name in diagnostic.RelativePath)
        {
            if (!TryMetadataNode(path, out DefinitionFormIndex.Node owner, out _)) break;
            DefinitionNode schema = owner.Schema;
            if (schema.Variants.Count > 0)
            {
                if (ReadValue(path) is not { } supplied ||
                    DefinitionValidator.SelectVariant(schema, supplied) is not { } branch) break;
                schema = branch;
            }
            if (schema.Items is not null && int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int row))
                segments.Add(DefinitionLocationSegment.ArrayRow(row));
            else if (schema.Properties.ContainsKey(name))
                segments.Add(DefinitionLocationSegment.Property(name));
            else if (schema.AdditionalProperties is not null && ReadValue(path) is { ValueKind: JsonValueKind.Object } mapping)
            {
                string[] names = mapping.EnumerateObject().Select(property => property.Name)
                    .Where(key => !schema.Properties.ContainsKey(key)).ToArray();
                int index = Array.IndexOf(names, name);
                if (index < 0) break;
                segments.Add(DefinitionLocationSegment.MapEntry(index));
            }
            else break;
            path.Add(name);
        }
        return new(diagnostic.Code, diagnostic.FieldId, new DefinitionCorrectionLocation(InputRevision, segments));
    }

    internal bool TryCorrectionPath(string fieldId, DefinitionCorrectionLocation location,
        DefinitionValidationResult validation, out IReadOnlyList<string> path)
    {
        var resolved = new List<string>();
        path = resolved;
        if (fieldId.Length > 0)
        {
            FieldDefinition? field = validation.Fields.FirstOrDefault(item => item.Id == fieldId)?.Definition;
            if (field is null) return false;
            resolved.Add(field.Name);
        }
        foreach (DefinitionLocationSegment segment in location.Segments)
        {
            if (segment.Kind == DefinitionLocationSegmentKind.Property)
                resolved.Add(segment.PropertyName!);
            else if (segment.Kind == DefinitionLocationSegmentKind.ArrayRow)
                resolved.Add(segment.Index!.Value.ToString(CultureInfo.InvariantCulture));
            else
            {
                if (!TryRootMetadata(resolved, out DefinitionFormIndex.Node owner, out _, validation) ||
                    !validation.TryGetEffectiveValue(DefinitionPath.FromSegments(resolved), out JsonElement mapping) ||
                    mapping.ValueKind != JsonValueKind.Object) return false;
                if (owner.Variants.Count > 0 && DefinitionValidator.SelectVariant(owner.Schema, mapping) is { } branch)
                    owner = owner.Variants.Single(candidate => ReferenceEquals(candidate.Schema, branch));
                string[] names = mapping.EnumerateObject().Select(property => property.Name)
                    .Where(name => !owner.Properties.ContainsKey(name)).ToArray();
                if (segment.Index is not { } index || index >= names.Length) return false;
                resolved.Add(names[index]);
            }
        }
        if (!TryRootMetadata(resolved, out _, out _, validation)) return false;
        if (fieldId.Length > 0 && TryOrdinaryRootPath(resolved, out string[] ordinary)) path = ordinary;
        return true;
    }

    public bool FocusCorrection(string fieldId, DefinitionCorrectionLocation location, bool preserveDiscovery = false)
    {
        ArgumentNullException.ThrowIfNull(location);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryResolveCorrectionPath(fieldId, location, out DefinitionPath path) &&
            FocusEditor(path, preserveDiscovery);
    }

    /// <summary>Explicitly resolves current private workspace coordinates for a local renderer, never for diagnostics or transport.</summary>
    public bool TryResolveCorrection(string fieldId, DefinitionCorrectionLocation location, out IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(location);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Array.Empty<string>();
            return location.InputRevision == InputRevision &&
                TryCorrectionPath(fieldId, location, Facts.Validation, out path);
        }
    }

    /// <summary>Resolves current safe correction coordinates to a literal private editor path.</summary>
    public bool TryResolveCorrectionPath(string fieldId, DefinitionCorrectionLocation location, out DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(location);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = DefinitionPath.FromSegments([]);
            if (location.InputRevision != InputRevision ||
                !TryCorrectionPath(fieldId, location, Facts.Validation, out IReadOnlyList<string> resolved)) return false;
            path = fieldId.Length == 0 ? DefinitionPath.FromSegments(resolved) : DataPath(resolved);
            return true;
        }
    }

    /// <summary>Opens exact literal ancestors for correction without changing answers or granting consent.</summary>
    public bool FocusEditor(IReadOnlyList<string> path, bool preserveDiscovery = false) =>
        FocusEditor(DataPath(path), preserveDiscovery);

    public bool FocusEditor(DefinitionPath path, bool preserveDiscovery = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryEditorMetadata(path, out _, out _) || !IsEditorActive(path)) return false;
        DefinitionFormSection? section = TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? _index!.SectionsByField[ordinary[0]] : null;
        if (section is not null && !Experiences.Single(item => item.Id == SelectedExperienceId).Sections.Contains(section.Id) &&
            Experiences.FirstOrDefault(item => item.Sections.Contains(section.Id)) is { } experience)
            SelectExperience(experience.Id);
        if (!preserveDiscovery)
        {
            _view.DiscoverySelection = new();
            _discovery = null;
            OnPropertyChanged(nameof(DiscoveryQuery));
            OnPropertyChanged(nameof(DiscoverySelection));
        }
        for (int length = 0; length <= path.Segments.Count; length++)
            _view.PromotedPaths.Add(GetEditorIdentity(DefinitionPath.FromSegments(path.Segments.Take(length))));
        OnViewChanged();
        return true;
    }
}
