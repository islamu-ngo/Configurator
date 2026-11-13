namespace Configurator.Presentation;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    /// <summary>Edits literal configuration coordinates, independently of ordinary field identifiers.</summary>
    public bool EditValue(DefinitionPath path, string? text)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? EditValue(ordinary, text) : EditRootValue(path.Segments, text);
    }

    /// <summary>Supplies a typed value at literal configuration coordinates; null explicitly omits it.</summary>
    public bool SetValue(DefinitionPath path, JsonElement? value)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ApplyEditorValue(path, value, clearDrafts: true);
    }

    /// <summary>Removes the override and omission at literal configuration coordinates.</summary>
    public bool ResetValue(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? ResetValue(ordinary) : ResetRootValue(path.Segments);
    }

    private bool ResetRootValue(IReadOnlyList<string> path)
    {
        if (!TryRootMetadata(path, out DefinitionFormIndex.Node node, out bool inheritedSecret) ||
            SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets &&
                DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret)) return false;
        bool accepted = path.Count == 0
            ? ApplyRootValue(path, JsonSerializer.SerializeToElement(new Dictionary<string, object>()), true)
            : ApplyRootValue(path, null, true);
        if (!accepted) return false;
        Omitted.RemoveWhere(existing => existing.Segments.Take(path.Count).SequenceEqual(path));
        _editing.RootInput.Origins.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path));
        _facts = null;
        return true;
    }

    /// <summary>Selects the declared branch at literal configuration coordinates through Core's discriminator.</summary>
    public bool SelectVariant(DefinitionPath path, int index)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (TryOrdinaryRootPath(path.Segments, out string[] ordinary)) return SelectVariant(ordinary, index);
        if (!TryRootMetadata(path.Segments, out DefinitionFormIndex.Node node, out _) ||
            index < 0 || index >= node.Variants.Count || node.Schema.Discriminator is not { } discriminator) return false;
        JsonElement value = node.Variants[index].Schema.Properties[discriminator].Keywords.GetProperty("const");
        if (ReadRootValue(path.Segments) is { } current &&
            ReferenceEquals(DefinitionValidator.SelectVariant(node.Schema, current), node.Variants[index].Schema))
            return SetValue(DefinitionPath.FromSegments([.. path.Segments, discriminator]), value);
        return SetValue(path, JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { [discriminator] = value }));
    }

    /// <summary>Returns admitted metadata for literal configuration coordinates.</summary>
    public DefinitionNode? GetEditorSchema(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TryEditorMetadata(path, out DefinitionFormIndex.Node node, out _) ? node.Schema : null;
        }
    }

    /// <summary>Reads current Core facts by literal configuration coordinates.</summary>
    public DefinitionNodeState? GetEditorState(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Facts.EditorState(path);
        }
    }

    /// <summary>Reads answer provenance at literal configuration coordinates.</summary>
    public DefinitionAnswerOrigin AnswerOrigin(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TryOrdinaryRootPath(path.Segments, out string[] ordinary)
                ? AnswerOrigin(ordinary, Facts.Validation) : RootAnswerOrigin(path.Segments, Facts.Validation);
        }
    }

    internal DefinitionAnswerOrigin RootAnswerOrigin(IReadOnlyList<string> path, DefinitionValidationResult validation)
    {
        if (!TryRootMetadata(path, out _, out _, validation) || ReadRootValue(path, validation) is null)
            return DefinitionAnswerOrigin.Omitted;
        if (!TrySuppliedValue(JsonSerializer.SerializeToElement(_editing.Root), path, out _))
            return DefinitionAnswerOrigin.Defaulted;
        return _editing.RootInput.Origins.Where(item => path.Take(item.Path.Length).SequenceEqual(item.Path))
            .OrderByDescending(item => item.Path.Length).Select(item => item.Origin)
            .FirstOrDefault(DefinitionAnswerOrigin.Explicit);
    }

    /// <summary>Identifies the logical node at literal configuration coordinates.</summary>
    public string GetEditorIdentity(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return RootEditorIdentity(path.Segments);
        }
    }

    private bool TryOrdinaryRootPath(IReadOnlyList<string> path, out string[] ordinary)
    {
        string? fieldId = path.Count == 0 ? null : _index!.FieldIdsByName.GetValueOrDefault(path[0]);
        ordinary = fieldId is null ? [] : [fieldId, .. path.Skip(1)];
        return fieldId is not null;
    }

    private JsonElement? ReadRootValue(IReadOnlyList<string> path, DefinitionValidationResult? validation = null) =>
        (validation ?? DefinitionValidator.Validate(Definition, Snapshot()))
            .TryGetEffectiveValue(DefinitionPath.FromSegments(path), out JsonElement value) ? value : null;

    private JsonElement? ReadEditorValue(DefinitionPath path, DefinitionValidationResult? validation = null) =>
        TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? ReadValue(ordinary, validation) : ReadRootValue(path.Segments, validation);

    private bool TryEditorMetadata(DefinitionPath path, out DefinitionFormIndex.Node node, out bool inheritedSecret) =>
        TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? TryMetadataNode(ordinary, out node, out inheritedSecret)
            : TryRootMetadata(path.Segments, out node, out inheritedSecret);

    private bool IsEditorActive(DefinitionPath path)
    {
        DefinitionFormFacts facts = Facts;
        if (path.Segments.Count > 0 && facts.Validation.ConfigurationContext is { } context &&
            !context.RelevantLocations.Contains(path)) return false;
        // Core marks descendants inactive while their container is absent. Its active owner permits supply.
        for (int length = 1; length <= path.Segments.Count; length++)
        {
            DefinitionPath prefix = DefinitionPath.FromSegments(path.Segments.Take(length));
            if (facts.EditorState(prefix) is not { } state) continue;
            if (!state.IsActive) return false;
            if (!state.IsProvided && TryRootMetadata(prefix.Segments, out DefinitionFormIndex.Node owner, out _, facts.Validation) &&
                owner.Schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence) return true;
        }
        return true;
    }

    private bool ApplyEditorValue(DefinitionPath path, JsonElement? value, bool clearDrafts,
        IReadOnlyCollection<DefinitionPath>? replacementOmissions = null) =>
        TryOrdinaryRootPath(path.Segments, out string[] ordinary)
            ? ApplyValue(ordinary, value, clearDrafts) : ApplyRootValue(path.Segments, value, clearDrafts, replacementOmissions);

    private string RootEditorIdentity(IReadOnlyList<string> path)
    {
        if (TryOrdinaryRootPath(path, out string[] ordinary)) return GetEditorIdentity(ordinary);
        if (!TryRootMetadata(path, out _, out _)) throw new ArgumentException("editor-path-invalid", nameof(path));
        string identity = "root-" + Definition.FamilyId.ToString("N");
        var prefix = new List<string>();
        foreach (string segment in path)
        {
            bool hasOwner = TryRootMetadata(prefix, out DefinitionFormIndex.Node owner, out _);
            if (hasOwner && owner.Variants.Count > 0)
                identity += "-branch-" + _editing.BranchIdentity(identity).ToString(CultureInfo.InvariantCulture);
            if (hasOwner && owner.Item is not null && ReadRootValue(prefix) is { ValueKind: JsonValueKind.Array } rows)
                identity += "-row-" + _editing.RowIdentity(identity, rows.GetArrayLength(),
                    int.Parse(segment, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
            else identity += "-key-" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(segment));
            prefix.Add(segment);
        }
        return identity;
    }

    private bool IsRootLocation(IReadOnlyList<string> path) =>
        path.Count == 0 || !_index!.FieldPositions.ContainsKey(path[0]);

    private List<string> ChangedBranchIdentities(IReadOnlyDictionary<string, JsonElement> replacement,
        IReadOnlyDictionary<string, JsonElement>? original = null)
    {
        var retired = new List<string>();
        Visit(Definition.Configuration, JsonSerializer.SerializeToElement(original ?? _editing.Root),
            JsonSerializer.SerializeToElement(replacement), []);
        return retired;

        void Visit(DefinitionNode schema, JsonElement? before, JsonElement? after, string[] path)
        {
            if (schema.Variants.Count > 0)
            {
                DefinitionNode? old = before is { } previous ? DefinitionValidator.SelectVariant(schema, previous) : null;
                DefinitionNode? next = after is { } current ? DefinitionValidator.SelectVariant(schema, current) : null;
                if (!ReferenceEquals(old, next))
                {
                    if (old is not null && TryRootMetadata(path, out _, out _)) retired.Add(RootEditorIdentity(path));
                    return;
                }
                if (old is null) return;
                schema = old;
            }
            if (before is { ValueKind: JsonValueKind.Object } mapping)
                foreach (JsonProperty property in mapping.EnumerateObject())
                {
                    DefinitionNode? child = schema.Properties.GetValueOrDefault(property.Name) ?? schema.AdditionalProperties;
                    if (child is null) continue;
                    Visit(child, property.Value, after is { ValueKind: JsonValueKind.Object } next &&
                        next.TryGetProperty(property.Name, out JsonElement value) ? value : null, [.. path, property.Name]);
                }
            else if (before is { ValueKind: JsonValueKind.Array } rows && schema.Items is { } item)
                for (int index = 0; index < rows.GetArrayLength(); index++)
                    Visit(item, rows[index], after is { ValueKind: JsonValueKind.Array } next && index < next.GetArrayLength()
                        ? next[index] : null, [.. path, index.ToString(CultureInfo.InvariantCulture)]);
        }
    }

    /// <summary>Explicit private coordinates for the current root's property and typed-map children.</summary>
    public IReadOnlyList<DefinitionPath> GetRootChildren()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DefinitionNode schema = Definition.Configuration;
            if (schema.Variants.Count > 0)
            {
                if (DefinitionValidator.SelectVariant(schema, JsonSerializer.SerializeToElement(_editing.Root)) is not { } branch)
                    return [];
                schema = branch;
            }
            return Array.AsReadOnly(schema.Properties.Keys.Concat(schema.AdditionalProperties is null ? [] :
                _editing.Root.Keys.Where(name => !schema.Properties.ContainsKey(name)))
                .Select(name => DefinitionPath.FromSegments([name])).ToArray());
        }
    }

    internal bool TryRootMetadata(IReadOnlyList<string> path, out DefinitionFormIndex.Node node,
        out bool inheritedSecret, DefinitionValidationResult? validation = null) =>
        TryRootMetadata(path, out node, out inheritedSecret, out _, validation);

    /// <summary>
    /// Also reports whether an ancestor or selected branch above the resolved node declares a non-public sensitivity.
    /// </summary>
    internal bool TryRootMetadata(IReadOnlyList<string> path, out DefinitionFormIndex.Node node,
        out bool inheritedSecret, out bool inheritedProtected, DefinitionValidationResult? validation = null)
    {
        node = _index!.Root;
        inheritedSecret = DefinitionSecretPolicy.IsSecret(node.Schema);
        inheritedProtected = false;
        var prefix = new List<string>();
        foreach (string segment in path)
        {
            inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
            inheritedProtected |= DefinitionSecretPolicy.DeclaresProtected(node.Schema);
            if (node.Variants.Count > 0)
            {
                JsonElement? value = prefix.Count == 0 ? JsonSerializer.SerializeToElement(_editing.Root)
                    : ReadRootValue(prefix, validation);
                if (value is not { } supplied || DefinitionValidator.SelectVariant(node.Schema, supplied) is not { } branch)
                    return false;
                node = node.Variants.Single(candidate => ReferenceEquals(candidate.Schema, branch));
                inheritedSecret |= DefinitionSecretPolicy.IsSecret(node.Schema);
                inheritedProtected |= DefinitionSecretPolicy.DeclaresProtected(node.Schema);
            }
            if (node.Item is { } item)
            {
                if (ReadRootValue(prefix, validation) is not { ValueKind: JsonValueKind.Array } rows ||
                    !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                    segment != index.ToString(CultureInfo.InvariantCulture) || index < 0 || index >= rows.GetArrayLength())
                    return false;
                node = item;
            }
            else if (node.Properties.TryGetValue(segment, out DefinitionFormIndex.Node? property)) node = property;
            else if (node.Additional is { } additional) node = additional;
            else return false;
            prefix.Add(segment);
        }
        return true;
    }

    private bool ApplyRootValue(IReadOnlyList<string> path, JsonElement? value, bool clearDrafts,
        IReadOnlyCollection<DefinitionPath>? replacementOmissions = null)
    {
        if (!TryRootMetadata(path, out DefinitionFormIndex.Node metadata, out bool inheritedSecret) ||
            value is not null && SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets &&
                DefinitionSecretPolicy.IsSecret(metadata.Schema, inheritedSecret)) return false;
        if (value is { } supplied && SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets &&
            DefinitionSecretPolicy.ContainsSecret(metadata.Schema, supplied, inheritedSecret)) return false;
        var root = JsonNode.Parse(JsonSerializer.SerializeToElement(_editing.Root).GetRawText())!.AsObject();
        if (path.Count == 0)
        {
            if (value is not { ValueKind: JsonValueKind.Object } mapping) return false;
            root = JsonNode.Parse(mapping.GetRawText())!.AsObject();
        }
        else
        {
            JsonNode parent = root;
            for (int index = 0; index < path.Count - 1; index++)
            {
                string segment = path[index];
                if (parent is JsonObject ancestor)
                {
                    if (ancestor[segment] is null)
                    {
                        if (value is null) return false;
                        if (!TryRootMetadata(path.Take(index + 1).ToArray(), out DefinitionFormIndex.Node container, out _) ||
                            !TryRootMetadata(path.Take(index).ToArray(), out DefinitionFormIndex.Node owner, out _) ||
                            !ancestor.ContainsKey(segment) && ancestor.Count >= MaximumMapEntries(owner.Schema)) return false;
                        ancestor[segment] = container.Item is null ? new JsonObject() : new JsonArray();
                    }
                    parent = ancestor[segment]!;
                }
                else if (parent is JsonArray rows && int.TryParse(segment, out int row) &&
                    row >= 0 && row < rows.Count && rows[row] is { } child) parent = child;
                else return false;
            }
            if (parent is JsonObject obj)
            {
                if (value is not null && !obj.ContainsKey(path[^1]) &&
                    TryRootMetadata(path.Take(path.Count - 1).ToArray(), out DefinitionFormIndex.Node owner, out _) &&
                    obj.Count >= MaximumMapEntries(owner.Schema)) return false;
                if (value is null) obj.Remove(path[^1]);
                else obj[path[^1]] = JsonNode.Parse(value.Value.GetRawText());
            }
            else if (parent is JsonArray rows && int.TryParse(path[^1], out int row) &&
                row >= 0 && row < rows.Count && value is { } item) rows[row] = JsonNode.Parse(item.GetRawText());
            else return false;
        }
        var values = DefinitionDocumentEditing.Values(root);
        DefinitionPath location = DefinitionPath.FromSegments(path);
        DefinitionPath[] omissions = (replacementOmissions ?? Omitted).Where(omitted => (!clearDrafts || !DefinitionDocumentEditing.Prefix(location, omitted)) &&
            !(value is not null && DefinitionDocumentEditing.Prefix(omitted, location))).ToArray();
        try { _ = new AnswerSnapshot(InputRevision, values, omissions); }
        catch (DefinitionException) { return false; }
        string identity = RootEditorIdentity(path);
        IReadOnlyList<string> retiredBranches = ChangedBranchIdentities(values);
        _editing.Root.Clear();
        foreach ((string name, JsonElement element) in values) _editing.Root.Add(name, element);
        foreach (string retired in retiredBranches) _editing.RetireBranch(retired);
        if (clearDrafts)
        {
            if (value is not null)
                foreach (DefinitionPath ancestor in Omitted.Where(omitted =>
                    DefinitionDocumentEditing.Prefix(omitted, location)).ToArray())
                    RecordRootOrigin(ancestor.Segments, DefinitionAnswerOrigin.Explicit, clearDescendants: false);
            _editing.RootInput.InvalidPaths.RemoveAll(existing => existing.Take(path.Count).SequenceEqual(path));
            _editing.RootInput.DraftTexts.RemoveAll(existing => existing.Path.Take(path.Count).SequenceEqual(path));
            RecordRootOrigin(path, value is null ? DefinitionAnswerOrigin.Omitted : DefinitionAnswerOrigin.Explicit);
            _editing.ClearRows(identity);
        }
        Omitted.Clear();
        Omitted.UnionWith(omissions);
        if (value is null && !Omitted.Any(omitted => DefinitionDocumentEditing.Prefix(omitted, location))) Omitted.Add(location);
        OnInputChanged();
        return true;
    }

    private bool EditRootValue(IReadOnlyList<string> path, string? text)
    {
        if (!TryRootMetadata(path, out DefinitionFormIndex.Node node, out bool inheritedSecret) ||
            SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets &&
                DefinitionSecretPolicy.IsSecret(node.Schema, inheritedSecret)) return false;
        if (text is null) return ApplyRootValue(path, null, true);
        if (DefinitionScalarEditing.TryParse(node.Type, text, node.Metadata, out JsonElement? value) &&
            ApplyRootValue(path, value, true)) return true;
        DefinitionFieldState input = _editing.RootInput;
        input.InvalidPaths.RemoveAll(existing => existing.SequenceEqual(path));
        input.InvalidPaths.Add(path.ToArray());
        input.DraftTexts.RemoveAll(existing => existing.Path.SequenceEqual(path));
        if (text.Length <= 65_536 && input.DraftTexts.Sum(draft => draft.Text.Length) + text.Length <= 65_536)
            input.DraftTexts.Add((path.ToArray(), text));
        OnInputChanged();
        return false;
    }

    private void RecordRootOrigin(IReadOnlyList<string> path, DefinitionAnswerOrigin origin, bool clearDescendants = true)
    {
        _editing.RootInput.Origins.RemoveAll(existing => existing.Path.SequenceEqual(path) ||
            clearDescendants && existing.Path.Take(path.Count).SequenceEqual(path));
        _editing.RootInput.Origins.Add((path.ToArray(), origin));
    }

    internal IEnumerable<DefinitionDiagnostic> RootInputDiagnostics()
    {
        foreach (string[] path in _editing.RootInput.InvalidPaths)
        {
            var segments = new List<DefinitionLocationSegment>();
            var prefix = new List<string>();
            foreach (string name in path)
            {
                if (!TryRootMetadata(prefix, out DefinitionFormIndex.Node owner, out _)) break;
                DefinitionNode schema = owner.Schema;
                if (schema.Variants.Count > 0)
                {
                    JsonElement? current = prefix.Count == 0 ? JsonSerializer.SerializeToElement(_editing.Root) : ReadRootValue(prefix);
                    if (current is not { } supplied || DefinitionValidator.SelectVariant(schema, supplied) is not { } branch) break;
                    schema = branch;
                }
                if (schema.Properties.ContainsKey(name)) segments.Add(DefinitionLocationSegment.Property(name));
                else if (schema.Items is not null && int.TryParse(name, out int row))
                    segments.Add(DefinitionLocationSegment.ArrayRow(row));
                else if (schema.AdditionalProperties is not null)
                {
                    JsonElement? mapping = prefix.Count == 0 ? JsonSerializer.SerializeToElement(_editing.Root) : ReadRootValue(prefix);
                    if (mapping is not { ValueKind: JsonValueKind.Object } value) break;
                    int index = Array.IndexOf(value.EnumerateObject().Select(property => property.Name)
                        .Where(key => !schema.Properties.ContainsKey(key)).ToArray(), name);
                    if (index < 0) break;
                    segments.Add(DefinitionLocationSegment.MapEntry(index));
                }
                else break;
                prefix.Add(name);
            }
            yield return new("input-type-invalid", "", new DefinitionCorrectionLocation(InputRevision, segments));
        }
    }
}
