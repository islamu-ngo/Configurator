namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

/// <summary>
/// Bounded (max 32) memory-only undo/redo stack for accepted workspace states.
/// Synchronously retires and purges secret-bearing entries upon consent revocation or disposal.
/// </summary>
public sealed class DefinitionFormUndoHistory
{
    public const int MaxCapacity = 32;

    private readonly List<UndoEntry> _undo = new(MaxCapacity);
    private readonly List<UndoEntry> _redo = new(MaxCapacity);

    internal DefinitionFormUndoHistory() { }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public string? LastUndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? LastRedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    internal void Record(
        string label,
        DefinitionAcceptedEditingState before,
        DefinitionAcceptedEditingState after,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        Func<IReadOnlyList<string>, bool> isSecretOrdinaryOrLiteral)
    {
        bool touchesSecret = EntryTouchesPrivacy(before, after, definition, fields, secretsOnly: true, isSecretOrdinaryOrLiteral);
        foreach (UndoEntry entry in _redo)
            entry.Retire();
        _redo.Clear();

        if (_undo.Count == MaxCapacity)
        {
            _undo[0].Retire();
            _undo.RemoveAt(0);
        }
        _undo.Add(new UndoEntry(before, after, label, touchesSecret));
    }

    internal bool TryPopUndo(out DefinitionAcceptedEditingState? target, out string? label)
    {
        if (_undo.Count == 0)
        {
            target = null;
            label = null;
            return false;
        }
        UndoEntry entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(entry);
        target = entry.Before.Copy();
        label = entry.Label;
        return true;
    }

    internal bool TryPopRedo(out DefinitionAcceptedEditingState? target, out string? label)
    {
        if (_redo.Count == 0)
        {
            target = null;
            label = null;
            return false;
        }
        UndoEntry entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(entry);
        target = entry.After.Copy();
        label = entry.Label;
        return true;
    }

    internal void PurgeSecrets(
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        Func<IReadOnlyList<string>, bool> isSecretOrdinaryOrLiteral) =>
        PurgePrivacy(_undo, _redo, definition, fields, secretsOnly: true, isSecretOrdinaryOrLiteral);

    internal void PurgeProtected(
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        Func<IReadOnlyList<string>, bool> isProtectedOrdinaryOrLiteral) =>
        PurgePrivacy(_undo, _redo, definition, fields, secretsOnly: false, isProtectedOrdinaryOrLiteral);

    internal void Clear()
    {
        foreach (UndoEntry entry in _undo) entry.Retire();
        foreach (UndoEntry entry in _redo) entry.Retire();
        _undo.Clear();
        _redo.Clear();
    }

    private static void PurgePrivacy(
        List<UndoEntry> undo,
        List<UndoEntry> redo,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        bool secretsOnly,
        Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        PurgeStack(undo, definition, fields, secretsOnly, isPrivatePath);
        PurgeStack(redo, definition, fields, secretsOnly, isPrivatePath);
    }

    private static void PurgeStack(
        List<UndoEntry> stack,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        bool secretsOnly,
        Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        for (int index = stack.Count - 1; index >= 0; index--)
        {
            UndoEntry entry = stack[index];
            bool entryTouched = entry.TouchesSecret ||
                EntryTouchesPrivacy(entry.Before, entry.After, definition, fields, secretsOnly, isPrivatePath);
            ScrubSnapshot(entry.Before, definition, fields, secretsOnly, isPrivatePath);
            ScrubSnapshot(entry.After, definition, fields, secretsOnly, isPrivatePath);

            if (entryTouched || SnapshotsEquivalent(entry.Before, entry.After))
            {
                entry.Retire();
                stack.RemoveAt(index);
            }
        }
    }

    private static bool EntryTouchesPrivacy(
        DefinitionAcceptedEditingState before,
        DefinitionAcceptedEditingState after,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        bool secretsOnly,
        Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        if (SnapshotContainsPrivateData(before, definition, fields, secretsOnly, isPrivatePath) !=
            SnapshotContainsPrivateData(after, definition, fields, secretsOnly, isPrivatePath))
            return true;

        bool configurationLocked = secretsOnly
            ? DefinitionSecretPolicy.IsSecret(definition.Configuration)
            : DefinitionSecretPolicy.IsProtected(definition.Configuration);

        for (int index = 0; index < fields.Count; index++)
        {
            FieldDefinition field = fields[index].Definition;
            bool fieldPrivate = configurationLocked || (secretsOnly
                ? DefinitionSecretPolicy.IsSecret(field.Node) || ContainsSecretDescendants(field.Node)
                : DefinitionSecretPolicy.IsProtected(field.Node));
            if (!fieldPrivate) continue;

            if (!FieldStateEquivalent(before.Inputs[index], after.Inputs[index]))
                return true;
            string fieldId = field.Id;
            if (!OmissionsMatch(before.Omitted, after.Omitted, path => path.Segments.Count > 0 && path.Segments[0] == fieldId))
                return true;
        }

        // Check dynamic or unlisted root properties / drafts / omissions at private coordinates.
        foreach (string name in before.Root.Keys.Concat(after.Root.Keys).Distinct(StringComparer.Ordinal))
        {
            if (fields.Any(field => field.Definition.Name == name)) continue;
            bool beforeHas = before.Root.TryGetValue(name, out JsonElement bv);
            bool afterHas = after.Root.TryGetValue(name, out JsonElement av);
            if (beforeHas == afterHas && (!beforeHas || JsonElement.DeepEquals(bv, av))) continue;
            if (configurationLocked || isPrivatePath([name]) ||
                (definition.Configuration.Properties.TryGetValue(name, out DefinitionNode? prop) &&
                 (secretsOnly ? ContainsSecretDescendants(prop) : DefinitionSecretPolicy.IsProtected(prop))))
                return true;
        }

        if (before.RootInput.DraftTexts.Any(d => isPrivatePath(d.Path)) ||
            after.RootInput.DraftTexts.Any(d => isPrivatePath(d.Path)))
        {
            var beforePrivate = before.RootInput.DraftTexts.Where(d => isPrivatePath(d.Path)).Select(d => (string.Join('\0', d.Path), d.Text));
            var afterPrivate = after.RootInput.DraftTexts.Where(d => isPrivatePath(d.Path)).Select(d => (string.Join('\0', d.Path), d.Text));
            if (!beforePrivate.SequenceEqual(afterPrivate)) return true;
        }

        return !OmissionsMatch(before.Omitted, after.Omitted, path => isPrivatePath(path.Segments));
    }

    private static string? SnapshotContainsPrivateData(
        DefinitionAcceptedEditingState state,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        bool secretsOnly,
        Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        // Produces a deterministic structural digest of private portions only, avoiding keeping secrets intact.
        var parts = new List<string>();
        foreach ((string name, JsonElement value) in state.Root.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (secretsOnly ? DefinitionSecretPolicy.IsSecret(definition.Configuration) : DefinitionSecretPolicy.IsProtected(definition.Configuration))
            {
                parts.Add(name + ":" + value.GetRawText());
                continue;
            }
            if (definition.Configuration.Properties.TryGetValue(name, out DefinitionNode? schema))
            {
                JsonNode? node = JsonNode.Parse(value.GetRawText());
                JsonNode? scrubbed = secretsOnly
                    ? DefinitionSecretPolicy.RemoveSecrets(schema, node?.DeepClone())
                    : (DefinitionSecretPolicy.RemoveProtected(schema, node?.DeepClone()) ? null : node?.DeepClone());
                if (node?.ToJsonString() != scrubbed?.ToJsonString())
                    parts.Add(name + ":" + value.GetRawText());
            }
            else if (isPrivatePath([name]))
            {
                parts.Add(name + ":" + value.GetRawText());
            }
        }
        return parts.Count == 0 ? null : string.Join("|", parts);
    }

    private static void ScrubSnapshot(
        DefinitionAcceptedEditingState state,
        Definition definition,
        IReadOnlyList<DefinitionFieldInput> fields,
        bool secretsOnly,
        Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        bool configLocked = secretsOnly
            ? DefinitionSecretPolicy.IsSecret(definition.Configuration)
            : DefinitionSecretPolicy.IsProtected(definition.Configuration);

        if (configLocked)
        {
            state.Retire();
            return;
        }

        JsonNode? rootNode = JsonSerializer.SerializeToNode(state.Root);
        JsonNode? scrubbedRoot = secretsOnly
            ? DefinitionSecretPolicy.RemoveSecrets(definition.Configuration, rootNode)
            : (DefinitionSecretPolicy.RemoveProtected(definition.Configuration, rootNode) ? null : rootNode);

        state.Root.Clear();
        if (scrubbedRoot is JsonObject mapping)
        {
            foreach ((string key, JsonNode? val) in mapping)
                if (val is not null) state.Root.Add(key, JsonSerializer.SerializeToElement(val));
        }

        for (int index = 0; index < fields.Count; index++)
        {
            FieldDefinition field = fields[index].Definition;
            DefinitionFieldState fieldState = state.Inputs[index];
            bool wholePrivate = secretsOnly
                ? DefinitionSecretPolicy.IsSecret(field.Node)
                : DefinitionSecretPolicy.DeclaresProtected(field.Node);
            if (wholePrivate)
            {
                fieldState.Retire();
                continue;
            }

            bool mayHoldSecrets = secretsOnly ? ContainsSecretDescendants(field.Node) : DefinitionSecretPolicy.IsProtected(field.Node);
            if (fieldState.Value is { } supplied)
                fieldState.Text = DisplayValue(supplied);
            else if (mayHoldSecrets || isPrivatePath([field.Id]))
            {
                fieldState.Text = string.Empty;
                fieldState.InputDiagnosticCode = null;
                fieldState.Origin = DefinitionAnswerOrigin.Omitted;
            }

            fieldState.ScrubPrivate(isPrivatePath);
        }

        state.RootInput.ScrubPrivate(isPrivatePath);
        state.Omitted.RemoveWhere(path => isPrivatePath(path.Segments));
    }

    private static string DisplayValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText()
    };

    private static bool ContainsSecretDescendants(DefinitionNode node) =>
        DefinitionSecretPolicy.IsSecret(node) ||
        node.Properties.Values.Any(ContainsSecretDescendants) ||
        node.Items is not null && ContainsSecretDescendants(node.Items) ||
        node.AdditionalProperties is not null && ContainsSecretDescendants(node.AdditionalProperties) ||
        node.Variants.Any(ContainsSecretDescendants);

    private static bool OmissionsMatch(HashSet<DefinitionPath> a, HashSet<DefinitionPath> b, Func<DefinitionPath, bool> filter)
    {
        var first = a.Where(filter).OrderBy(p => p.ToString(), StringComparer.Ordinal);
        var second = b.Where(filter).OrderBy(p => p.ToString(), StringComparer.Ordinal);
        return first.SequenceEqual(second);
    }

    private static bool SnapshotsEquivalent(DefinitionAcceptedEditingState a, DefinitionAcceptedEditingState b)
    {
        if (a.Root.Count != b.Root.Count || !a.Omitted.SetEquals(b.Omitted)) return false;
        foreach ((string key, JsonElement value) in a.Root)
            if (!b.Root.TryGetValue(key, out JsonElement other) || !JsonElement.DeepEquals(value, other)) return false;
        for (int index = 0; index < a.Inputs.Length; index++)
            if (!FieldStateEquivalent(a.Inputs[index], b.Inputs[index])) return false;
        return FieldStateEquivalent(a.RootInput, b.RootInput);
    }

    private static bool FieldStateEquivalent(DefinitionFieldState a, DefinitionFieldState b) =>
        a.Text == b.Text &&
        a.Origin == b.Origin &&
        a.InputDiagnosticCode == b.InputDiagnosticCode &&
        DefinitionFieldState.EqualCollections(a, b);

    private sealed class UndoEntry(
        DefinitionAcceptedEditingState before,
        DefinitionAcceptedEditingState after,
        string label,
        bool touchesSecret)
    {
        internal DefinitionAcceptedEditingState Before { get; } = before;
        internal DefinitionAcceptedEditingState After { get; } = after;
        internal string Label { get; } = label;
        internal bool TouchesSecret { get; } = touchesSecret;

        internal void Retire()
        {
            Before.Retire();
            After.Retire();
        }
    }
}
