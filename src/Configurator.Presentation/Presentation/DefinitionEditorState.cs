namespace Configurator.Presentation;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public enum DefinitionAnswerOrigin { Omitted, Defaulted, Imported, Explicit }

public sealed partial class DefinitionFormModel
{
    public bool SetEditorExpanded(IReadOnlyList<string> path, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadata(path, out _, out _)) return false;
        string identity = GetEditorIdentity(path);
        _view.ClearEditorExpansion(identity, includeSelf: false);
        _view.EditorExpansion[identity] = expanded;
        if (!expanded)
            _view.ClearPromotions(identity);
        OnViewChanged();
        return true;
    }

    public bool IsEditorExpanded(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!TryMetadata(path, out _, out _)) return false;
            if (TryLocalExpansion(path, out bool expanded)) return expanded;
            DefinitionFormSection section = Sections.Single(item => item.Fields.Contains(path[0]));
            bool all = _view.SectionExpansion.TryGetValue(section.Id, out bool sectionExpanded)
                ? sectionExpanded : OptionalEditorsExpanded;
            return all || EffectiveDisclosure == SetupFieldDisclosure.Advanced;
        }
    }

    private bool TryLocalExpansion(IReadOnlyList<string> path, out bool expanded)
    {
        for (int length = path.Count; length > 0; length--)
            if (_view.EditorExpansion.TryGetValue(GetEditorIdentity(path.Take(length).ToArray()), out expanded))
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
            if (!TryMetadata(path, out _, out _) || !CanEdit(path)) return null;
            return Fields[_index!.FieldPositions[path[0]]].DraftText(path);
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
        if (omitted && _omitted.Any(existing =>
            path.Take(existing.Segments.Count).SequenceEqual(existing.Segments))) return;
        _omitted.RemoveWhere(existing => existing.Segments.Take(path.Count).SequenceEqual(path) ||
            path.Take(existing.Segments.Count).SequenceEqual(existing.Segments));
        if (omitted) _omitted.Add(DefinitionPath.FromSegments(path));
    }

    /// <summary>Removes the supplied override and suppression so Core may initialize this location again.</summary>
    public bool ResetValue(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadataNode(path, out _, out _) || !CanEdit(path)) return false;
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
        _omitted.RemoveWhere(existing => existing.Segments.Take(path.Count).SequenceEqual(path));
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
            string identity = _index!.RootIdentities[path[0]];
            if (path.Count == 1) return identity;
            var prefix = new List<string> { path[0] };
            foreach (string segment in path.Skip(1))
            {
                if (ReadValue(prefix) is { ValueKind: JsonValueKind.Array } rows)
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

    private void RetainRowOrigins(IReadOnlyList<string> path, DefinitionAnswerOrigin[] origins)
    {
        DefinitionFieldInput input = Fields.Single(item => item.Definition.Id == path[0]);
        for (int index = 0; index < origins.Length; index++)
            input.RecordOrigin([.. path, index.ToString(CultureInfo.InvariantCulture)], origins[index], clearDescendants: false);
        input.RecordOrigin(path, DefinitionAnswerOrigin.Explicit, clearDescendants: false);
    }

    private void RecordMaterializedOrigins(IReadOnlyList<string> path)
    {
        DefinitionValidationResult validation = Facts.Validation;
        DefinitionFieldInput input = Fields[_index!.FieldPositions[path[0]]];
        foreach (DefinitionNodeState node in validation.Nodes.Where(node => node.FieldId == path[0] && node.IsProvided))
        {
            string[] location = [node.FieldId, .. node.RelativePath];
            if (location.Take(path.Count).SequenceEqual(path) &&
                AnswerOrigin(location, validation) == DefinitionAnswerOrigin.Defaulted)
                input.RecordOrigin(location, DefinitionAnswerOrigin.Defaulted, clearDescendants: false);
        }
    }

    private DefinitionDiagnostic SafeInputDiagnostic(DefinitionDiagnostic diagnostic)
    {
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
        var resolved = new List<string> { fieldId };
        path = resolved;
        if (!_index!.Roots.ContainsKey(fieldId)) return false;
        foreach (DefinitionLocationSegment segment in location.Segments)
        {
            if (segment.Kind == DefinitionLocationSegmentKind.Property)
                resolved.Add(segment.PropertyName!);
            else if (segment.Kind == DefinitionLocationSegmentKind.ArrayRow)
                resolved.Add(segment.Index!.Value.ToString(CultureInfo.InvariantCulture));
            else
            {
                if (!TryMetadataNode(resolved, out DefinitionFormIndex.Node owner, out _, validation) ||
                    !validation.TryGetEffectiveValue(DataPath(resolved), out JsonElement mapping) ||
                    mapping.ValueKind != JsonValueKind.Object) return false;
                string[] names = mapping.EnumerateObject().Select(property => property.Name)
                    .Where(name => !owner.Properties.ContainsKey(name)).ToArray();
                if (segment.Index is not { } index || index >= names.Length) return false;
                resolved.Add(names[index]);
            }
        }
        return TryMetadataNode(resolved, out _, out _, validation);
    }

    public bool FocusCorrection(string fieldId, DefinitionCorrectionLocation location, bool preserveDiscovery = false)
    {
        ArgumentNullException.ThrowIfNull(location);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return location.InputRevision == InputRevision &&
            TryCorrectionPath(fieldId, location, Facts.Validation, out IReadOnlyList<string> path) &&
            FocusEditor(path, preserveDiscovery);
    }

    /// <summary>Opens exact literal ancestors for correction without changing answers or granting consent.</summary>
    public bool FocusEditor(IReadOnlyList<string> path, bool preserveDiscovery = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryMetadata(path, out _, out _) ||
            !Validate().Fields.Any(state => state.Id == path[0] && state.IsActive)) return false;
        DefinitionFormSection? section = Sections.FirstOrDefault(item => item.Fields.Contains(path[0]));
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
        for (int length = 1; length <= path.Count; length++)
            _view.PromotedPaths.Add(GetEditorIdentity(path.Take(length).ToArray()));
        OnViewChanged();
        return true;
    }
}
