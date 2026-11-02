using Configurator.Core.Dotenv;
namespace Configurator.Presentation;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Environment;

public sealed partial class DefinitionFormModel
{
    private DefinitionDocumentSession? _documentSession;
    private long _documentPrivacyGeneration;
    internal object DocumentGate => _gate;

    /// <summary>Replaces the previous document lease. Coverage is available before browser consent.</summary>
    public DefinitionDocumentSession OpenDocument(DefinitionDocumentRepresentation representation =
        DefinitionDocumentRepresentation.Canonical, string? artifactId = null)
    {
        if (!Enum.IsDefined(representation)) throw new ArgumentOutOfRangeException(nameof(representation));
        if (representation == DefinitionDocumentRepresentation.Environment) ArgumentNullException.ThrowIfNull(artifactId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (representation == DefinitionDocumentRepresentation.Environment
                && !Definition.Artifacts.Any(artifact => artifact.Id == artifactId && artifact.Format == "dotenv"))
                throw new ArgumentException("document-artifact-invalid", nameof(artifactId));
            RetireDocument();
            return _documentSession = new(this, representation, artifactId, _documentPrivacyGeneration);
        }
    }

    private void RetireDocument()
    {
        _documentSession?.Dispose();
        _documentSession = null;
        _documentPrivacyGeneration = checked(_documentPrivacyGeneration + 1);
    }

    internal bool DocumentIsCurrent(DefinitionDocumentSession session) => !_disposed
        && ReferenceEquals(_documentSession, session)
        && session.DefinitionDigest == Definition.Digest && session.InputRevision == InputRevision
        && session.PrivacyGeneration == _documentPrivacyGeneration && session.Privacy == SessionPrivacy;

    internal DefinitionAcceptedEditingState CaptureDocumentState(DefinitionDocumentSession session)
    {
        if (DocumentDraftConflict(session)) throw new InvalidOperationException("document-draft-conflict");
        return _editing.Capture();
    }

    private bool DocumentDraftConflict(DefinitionDocumentSession session) =>
        Fields.Any(field => field.InputDiagnostics.Any(diagnostic =>
            session.Scope.Any(scope => DefinitionDocumentEditing.Overlaps(scope,
                DefinitionPath.FromSegments([field.Definition.Name, .. diagnostic.RelativePath])))));

    internal byte[] ReadDocumentSource(DefinitionDocumentSession session, CancellationToken cancellationToken)
    {
        if (DocumentDraftConflict(session)) throw new InvalidOperationException("document-draft-conflict");
        AnswerSnapshot supplied = Snapshot();
        if (DefinitionDocumentEditing.HasInvalidRepresentedValues(Definition, session.Scope,
            DefinitionValidator.Validate(Definition, supplied, cancellationToken)))
            throw new InvalidOperationException("document-value-invalid");
        if (session.EnvironmentScope is { } environment)
        {
            var entries = new List<DotenvEntry>();
            foreach (DefinitionEditableMapping mapping in environment.Mappings)
                if (DefinitionDocumentEditing.TryRead(supplied.Values, mapping.Path, out JsonElement value))
                    entries.Add(new(mapping.Key, DefinitionDocumentEditing.Display(value),
                        DotenvEntryKind.LocalHumanValue, false, DotenvProvenance.UserInput));
            cancellationToken.ThrowIfCancellationRequested();
            DotenvRenderResult rendered = DotenvCodec.Render(new(entries), true, DotenvPurpose.EditableValues);
            if (!rendered.Succeeded) throw new InvalidOperationException("document-source-invalid");
            return rendered.Bytes.ToArray();
        }
        var values = new JsonObject();
        foreach (DefinitionPath path in session.Scope)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DefinitionDocumentEditing.TryRead(supplied.Values, path, out JsonElement value))
                DefinitionDocumentEditing.Set(values, path, value);
        }
        return AnswerSnapshotCodec.Serialize(new AnswerSnapshot(InputRevision,
            DefinitionDocumentEditing.Values(values), supplied.Omitted.Where(path =>
                session.Scope.Any(scope => DefinitionDocumentEditing.Prefix(scope, path)))));
    }

    /// <summary>The only document commit seam. Everything before Adopt operates on detached state.</summary>
    internal DefinitionDocumentApplyResult AdoptDocument(DefinitionDocumentSession session,
        DefinitionDocumentCandidate candidate, CancellationToken cancellationToken)
    {
        bool committed = false;
        long revision = session.InputRevision;
        try
        {
            using (BeginMutation())
            {
                if (!candidate.BelongsTo(session) || !session.IsCurrent(candidate.Generation)
                    || SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                    return new(false, "document-stale", InputRevision);
                if (cancellationToken.IsCancellationRequested || candidate.CancellationToken.IsCancellationRequested)
                {
                    candidate.Dispose();
                    return new(false, "document-cancelled", InputRevision);
                }
                if (session.EnvironmentScope is { } scope && !scope.IsCurrent(Definition, Snapshot(), cancellationToken))
                    return new(false, "document-stale", InputRevision);
                DefinitionAcceptedEditingState? replacement = candidate.Replacement;
                if (replacement is null) return new(false, "document-consumed", InputRevision);
                // Row tokens may have been allocated by read-only rendering since capture.
                // Carry those forward, excluding only containers actually changed by this candidate.
                _editing.CopyRowsTo(replacement, candidate.RetiredRows);
                revision = checked(InputRevision + 1);
                if (cancellationToken.IsCancellationRequested || candidate.CancellationToken.IsCancellationRequested)
                {
                    candidate.Dispose();
                    return new(false, "document-cancelled", InputRevision);
                }
                candidate.Consume();
                DefinitionFieldState[] retired = candidate.ChangedFields.Select(_editing.Input).ToArray();
                for (int index = 0; index < Fields.Count; index++)
                {
                    if (!candidate.ChangedFields.Contains(index))
                    {
                        replacement.Inputs[index].Retire();
                        replacement.Inputs[index] = _editing.Input(index);
                    }
                }
                _editing.Adopt(replacement);
                committed = true;
                foreach (DefinitionFieldState field in retired) field.Retire();
                OnInputChanged();
                foreach (int index in candidate.ChangedFields) Fields[index].NotifyDocumentChanged();
            }
            return new(true, "document-applied", revision);
        }
        catch (Exception) when (committed)
        {
            // Observers may throw or make newer reentrant edits after commit.
            // Their failure is not rejection and must never restore the prior payload.
            return new(true, "document-applied", revision, ObserverFailed: true);
        }
        catch (OperationCanceledException)
        {
            candidate.Dispose();
            return new(false, "document-cancelled", InputRevision);
        }
    }
}

internal static class DefinitionDocumentEditing
{
    internal static IReadOnlyList<DefinitionDocumentCoverage> CaptureCoverage(DefinitionNode root)
    {
        var result = new List<DefinitionDocumentCoverage>();
        foreach ((string name, DefinitionNode node) in root.Properties)
            Visit(node, DefinitionPath.FromSegments([name]), DefinitionSensitivityPolicy.SensitivityOf(root));
        return Array.AsReadOnly(result.ToArray());

        void Visit(DefinitionNode node, DefinitionPath path, FieldSensitivity inherited)
        {
            bool ownPublic = inherited == FieldSensitivity.Public
                && DefinitionSensitivityPolicy.SensitivityOf(node) == FieldSensitivity.Public;
            if (ownPublic && EntirelyPublic(node)) result.Add(new(path, true));
            else if (ownPublic && node.Type == DefinitionNodeType.Mapping && node.AdditionalProperties is null
                && node.Variants.Count == 0)
                foreach ((string name, DefinitionNode child) in node.Properties)
                    Visit(child, DefinitionPath.FromSegments([.. path.Segments, name]), FieldSensitivity.Public);
            else result.Add(new(path, false));
        }
        static bool EntirelyPublic(DefinitionNode node) =>
            DefinitionSensitivityPolicy.SensitivityOf(node) == FieldSensitivity.Public
            && node.Properties.Values.Concat(node.Variants)
                .Concat(node.Items is null ? [] : new[] { node.Items })
                .Concat(node.AdditionalProperties is null ? [] : new[] { node.AdditionalProperties })
                .All(EntirelyPublic);
    }

    internal static DefinitionDocumentParseResult Parse(Definition definition, DefinitionDocumentSession session,
        long generation, DefinitionAcceptedEditingState state, ReadOnlyMemory<byte> source,
        DefinitionDocumentApplyMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AnswerSnapshot original = Snapshot(definition, state);
        var values = new JsonObject(original.Values.Select(pair =>
            KeyValuePair.Create(pair.Key, JsonNode.Parse(pair.Value.GetRawText()))));
        var omitted = original.Omitted.ToHashSet();
        var supplied = new Dictionary<DefinitionPath, JsonElement?>();
        var suppressions = new HashSet<DefinitionPath>();
        if (session.Representation == DefinitionDocumentRepresentation.Canonical)
        {
            AnswerSnapshot parsed = AnswerSnapshotCodec.ParseJson(source);
            foreach ((string name, JsonElement value) in parsed.Values)
                if (!InScope(DefinitionPath.FromSegments([name]), value))
                    return DefinitionDocumentParseResult.Reject("document-out-of-scope");
            foreach (DefinitionPath path in parsed.Omitted)
                if (!session.Scope.Any(scope => Prefix(scope, path)))
                    return DefinitionDocumentParseResult.Reject("document-out-of-scope");
            foreach (DefinitionPath path in session.Scope)
                supplied.Add(path, TryRead(parsed.Values, path, out JsonElement value) ? value : null);
            suppressions.UnionWith(parsed.Omitted);

            bool InScope(DefinitionPath path, JsonElement value) =>
                session.Scope.Any(scope => Prefix(scope, path))
                || session.Scope.Any(scope => Prefix(path, scope)) && value.ValueKind == JsonValueKind.Object
                    && value.EnumerateObject().All(member => InScope(
                        DefinitionPath.FromSegments([.. path.Segments, member.Name]), member.Value));
        }
        else
        {
            DotenvParseResult parsed = DotenvCodec.Parse(source, DotenvPurpose.EditableValues, cancellationToken);
            if (!parsed.Succeeded)
                return new(null, parsed.Diagnostics.Select((item, index) => new DefinitionDocumentDiagnostic(
                    item.Code, parsed.DiagnosticLocations[index].Line, parsed.DiagnosticLocations[index].Column)),
                    parsed.DiagnosticsTruncated);
            var diagnostics = new List<DefinitionDocumentDiagnostic>();
            var mappings = session.EnvironmentScope!.Mappings.ToDictionary(item => item.Key, StringComparer.Ordinal);
            IReadOnlyList<DotenvEntry> entries = parsed.Document!.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DotenvEntry entry = entries[index];
                string? code = !mappings.TryGetValue(entry.Key, out DefinitionEditableMapping? mapping)
                    ? "document-key-not-editable"
                    : !session.EnvironmentScope.TryConvertValue(entry.Key, entry.Value!, out _)
                        ? "document-value-invalid" : null;
                if (code is not null)
                {
                    if (diagnostics.Count <= 100)
                        diagnostics.Add(new(code, parsed.EntryLocations[index].Line, parsed.EntryLocations[index].Column));
                }
                else
                {
                    session.EnvironmentScope.TryConvertValue(entry.Key, entry.Value!, out JsonElement value);
                    supplied.Add(mapping!.Path, value);
                }
            }
            if (diagnostics.Count != 0) return new(null, diagnostics);
            if (mode == DefinitionDocumentApplyMode.ReplaceRepresentedScope)
                foreach (DefinitionPath path in session.Scope.Where(path => !supplied.ContainsKey(path)))
                {
                    supplied.Add(path, null);
                    suppressions.Add(path);
                }
        }

        var changes = new List<DefinitionDocumentChange>();
        var changedFields = new HashSet<int>();
        var retiredRows = new HashSet<string>(StringComparer.Ordinal);
        foreach ((DefinitionPath path, JsonElement? value) in supplied)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool hadValue = TryRead(original.Values, path, out JsonElement before);
            DefinitionPath[] beforeOmissions = omitted.Where(item => Prefix(path, item)).ToArray();
            DefinitionPath[] afterOmissions = suppressions.Where(item => Prefix(path, item)).ToArray();
            bool valueChanged = hadValue != value.HasValue || hadValue && value is { } after && !JsonElement.DeepEquals(before, after);
            if (!valueChanged && beforeOmissions.ToHashSet().SetEquals(afterOmissions)) continue;
            // An unrepresented ancestor suppression cannot be silently removed by a child edit.
            if (omitted.Any(item => item != path && Prefix(item, path)))
                return DefinitionDocumentParseResult.Reject("document-omission-conflict");
            Set(values, path, value);
            omitted.ExceptWith(beforeOmissions);
            omitted.UnionWith(afterOmissions);
            DefinitionDocumentChangeKind kind = value.HasValue ? DefinitionDocumentChangeKind.Set
                : afterOmissions.Contains(path) ? DefinitionDocumentChangeKind.Omit : DefinitionDocumentChangeKind.Reset;
            changes.Add(new(path, kind));
            int fieldIndex = definition.Fields.ToList().FindIndex(field => field.Name == path.Segments[0]);
            changedFields.Add(fieldIndex);
            string[] editorPath = [definition.Fields[fieldIndex].Id, .. path.Segments.Skip(1)];
            RetireChangedRows(hadValue ? before : null, value,
                "field-" + definition.FamilyId.ToString("N") + "-" +
                    Convert.ToHexString(Encoding.UTF8.GetBytes(editorPath[0])), path.Segments.Skip(1).ToArray());
        }
        AnswerSnapshot replacement = new(original.InputRevision, Values(values), omitted);
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, replacement, cancellationToken);
        // Core owns shape, constraints and requiredness. Missing required values remain Review work.
        // Ignore unrelated existing errors, but never accept invalid supplied content in this document.
        if (HasInvalidRepresentedValues(definition, session.Scope, validation))
            return DefinitionDocumentParseResult.Reject("document-value-invalid");
        foreach ((DefinitionPath path, JsonElement? value) in supplied)
            if (value is { } actual && !RetainsSuppliedChange(actual,
                validation.TryGetEffectiveValue(path, out JsonElement effective) ? effective : null,
                TryRead(original.Values, path, out JsonElement previous) ? previous : null))
                return DefinitionDocumentParseResult.Reject("document-value-inactive");
        for (int index = 0; index < definition.Fields.Count; index++)
        {
            if (!changedFields.Contains(index)) continue;
            DefinitionFieldState field = state.Inputs[index];
            JsonElement? next = replacement.Values.TryGetValue(definition.Fields[index].Name, out JsonElement value) ? value : null;
            UpdateOrigins(field, [definition.Fields[index].Id], field.Value, next);
            field.Value = next;
            field.Text = field.Value is { } typed ? Display(typed) : string.Empty;
            if (field.Text.Length > 65_536) return DefinitionDocumentParseResult.Reject("answer-budget-exceeded");
        }
        state.Omitted.Clear();
        state.Omitted.UnionWith(replacement.Omitted.Select(path => DefinitionPath.FromSegments([
            definition.Fields.Single(field => field.Name == path.Segments[0]).Id, .. path.Segments.Skip(1)])));
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(session, generation, state, changes, cancellationToken)
        {
            ChangedFields = changedFields,
            RetiredRows = retiredRows
        }, []);

        void RetireChangedRows(JsonElement? before, JsonElement? after, string identity, string[] remaining)
        {
            foreach (string segment in remaining) identity += "-key-" + Convert.ToHexString(Encoding.UTF8.GetBytes(segment));
            if (before is { } old && after is { } next && JsonElement.DeepEquals(old, next)) return;
            if (before is { ValueKind: JsonValueKind.Array } || after is { ValueKind: JsonValueKind.Array })
            {
                retiredRows.Add(identity);
                return;
            }
            IEnumerable<string> names = (before is { ValueKind: JsonValueKind.Object } b ? b.EnumerateObject().Select(p => p.Name) : [])
                .Union(after is { ValueKind: JsonValueKind.Object } a ? a.EnumerateObject().Select(p => p.Name) : [], StringComparer.Ordinal);
            foreach (string name in names)
                RetireChangedRows(Member(before, name), Member(after, name), identity, [name]);
        }
    }

    internal static bool HasInvalidRepresentedValues(Definition definition,
        IReadOnlyList<DefinitionPath> scope, DefinitionValidationResult validation) =>
        validation.Diagnostics.Any(diagnostic => diagnostic.Code != "value-required"
            && (diagnostic.FieldId.Length == 0 || scope.Any(path =>
                Overlaps(path, DefinitionPath.FromSegments([
                    definition.Fields.Single(field => field.Id == diagnostic.FieldId).Name, .. diagnostic.RelativePath])))));

    private static void UpdateOrigins(DefinitionFieldState field, string[] path, JsonElement? before, JsonElement? after)
    {
        if (before is { } b && after is { } a && JsonElement.DeepEquals(b, a)) return;
        if (before is { ValueKind: JsonValueKind.Object } old && after is { ValueKind: JsonValueKind.Object } next)
        {
            foreach (string name in old.EnumerateObject().Select(p => p.Name).Union(next.EnumerateObject().Select(p => p.Name)))
            {
                string[] childPath = [.. path, name];
                JsonElement? previous = Member(old, name);
                JsonElement? replacement = Member(next, name);
                if (previous is { } retained && replacement is { } current && JsonElement.DeepEquals(retained, current))
                {
                    // Preserve the prior inherited origin before making this changed ancestor explicit.
                    DefinitionAnswerOrigin prior = field.Origins.Where(item => childPath.Take(item.Path.Length).SequenceEqual(item.Path))
                        .OrderByDescending(item => item.Path.Length).Select(item => item.Origin).FirstOrDefault(field.Origin);
                    field.Origins.RemoveAll(item => item.Path.SequenceEqual(childPath));
                    field.Origins.Add((childPath, prior));
                }
                else UpdateOrigins(field, childPath, previous, replacement);
            }
            field.Origins.RemoveAll(item => item.Path.SequenceEqual(path));
            if (path.Length == 1) field.Origin = DefinitionAnswerOrigin.Explicit;
            else field.Origins.Add((path, DefinitionAnswerOrigin.Explicit));
            return;
        }
        field.Origins.RemoveAll(item => item.Path.Take(path.Length).SequenceEqual(path));
        DefinitionAnswerOrigin origin = after.HasValue ? DefinitionAnswerOrigin.Explicit : DefinitionAnswerOrigin.Omitted;
        if (path.Length == 1) field.Origin = origin;
        else field.Origins.Add((path, origin));
    }

    private static JsonElement? Member(JsonElement? value, string name) =>
        value is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out JsonElement child) ? child : null;

    private static bool RetainsSuppliedChange(JsonElement supplied, JsonElement? effective, JsonElement? previous)
    {
        // Core intentionally hides inactive values. Preserve unchanged dormant answers,
        // but require every newly supplied value to survive Core's active validation.
        if (previous is { } old && JsonElement.DeepEquals(supplied, old)) return true;
        if (supplied.ValueKind == JsonValueKind.Object && effective is { ValueKind: JsonValueKind.Object })
            return supplied.EnumerateObject().All(member => RetainsSuppliedChange(
                member.Value, Member(effective, member.Name), Member(previous, member.Name)));
        if (supplied.ValueKind == JsonValueKind.Array && effective is { ValueKind: JsonValueKind.Array } rows
            && supplied.GetArrayLength() == rows.GetArrayLength())
            return supplied.EnumerateArray().Select((item, index) => RetainsSuppliedChange(item, rows[index],
                previous is { ValueKind: JsonValueKind.Array } prior && index < prior.GetArrayLength() ? prior[index] : null)).All(valid => valid);
        return effective is { } value && JsonElement.DeepEquals(supplied, value);
    }

    internal static AnswerSnapshot Snapshot(Definition definition, DefinitionAcceptedEditingState state) =>
        new(state.InputRevision, definition.Fields.Select((field, index) => (field, state.Inputs[index].Value))
            .Where(item => item.Value.HasValue).ToDictionary(item => item.field.Name, item => item.Value!.Value, StringComparer.Ordinal),
            state.Omitted.Select(path => DefinitionPath.FromSegments([
                definition.Fields.Single(field => field.Id == path.Segments[0]).Name, .. path.Segments.Skip(1)])));

    internal static bool Prefix(DefinitionPath prefix, DefinitionPath path) =>
        path.Segments.Take(prefix.Segments.Count).SequenceEqual(prefix.Segments, StringComparer.Ordinal);
    internal static bool Overlaps(DefinitionPath left, DefinitionPath right) => Prefix(left, right) || Prefix(right, left);
    internal static string Display(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    internal static Dictionary<string, JsonElement> Values(JsonObject root) =>
        root.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal);
    internal static bool TryRead(IReadOnlyDictionary<string, JsonElement> values, DefinitionPath path, out JsonElement value)
    {
        value = default;
        if (path.Segments.Count == 0 || !values.TryGetValue(path.Segments[0], out value)) return false;
        foreach (string segment in path.Segments.Skip(1))
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value)) return false;
        return true;
    }
    internal static void Set(JsonObject root, DefinitionPath path, JsonElement? value)
    {
        JsonObject parent = root;
        foreach (string segment in path.Segments.SkipLast(1))
        {
            if (parent[segment] is not JsonObject child)
            {
                if (!value.HasValue) return;
                if (parent.ContainsKey(segment)) throw new InvalidOperationException("document-container-conflict");
                parent[segment] = child = new JsonObject();
            }
            parent = child;
        }
        if (value.HasValue) parent[path.Segments[^1]] = JsonNode.Parse(value.Value.GetRawText());
        else parent.Remove(path.Segments[^1]);
    }
}
