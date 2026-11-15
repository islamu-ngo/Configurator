namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Definitions;

/// <summary>Aggregate-owned input lifetime and logical rows, accessed only through the form's mutation gate.</summary>
internal sealed class DefinitionEditingState
{
    private DefinitionAcceptedEditingState _accepted;

    internal DefinitionEditingState(IReadOnlyList<FieldDefinition> definitions,
        DefinitionSessionPrivacy privacy,
        Func<FieldDefinition, Func<DefinitionFieldState>, DefinitionFieldInput> createField)
    {
        _accepted = new(definitions.Select(definition => definition.Name).ToArray());
        Fields = Array.AsReadOnly(definitions.Select((definition, index) =>
            createField(definition, () => _accepted.Inputs[index])).ToArray());
        Privacy = privacy;
    }

    internal IReadOnlyList<DefinitionFieldInput> Fields { get; }
    internal DefinitionSessionPrivacy Privacy { get; set; }
    internal long InputRevision => _accepted.InputRevision;
    internal HashSet<DefinitionPath> Omitted => _accepted.Omitted;
    internal DefinitionFieldState Input(int index) => _accepted.Inputs[index];
    internal Dictionary<string, JsonElement> Root => _accepted.Root;
    internal DefinitionFieldState RootInput => _accepted.RootInput;
    internal long BranchIdentity(string identity)
    {
        if (!_accepted.Branches.TryGetValue(identity, out long token))
            _accepted.Branches.Add(identity, token = checked(++_accepted.NextRow));
        return token;
    }
    internal void RetireBranch(string identity)
    {
        ClearRows(identity);
        foreach (string child in _accepted.Branches.Keys.Where(key =>
            key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
            _accepted.Branches.Remove(child);
        _accepted.Branches[identity] = checked(++_accepted.NextRow);
    }

    internal void AdvanceRevision() => _accepted.InputRevision = checked(InputRevision + 1);

    internal DefinitionAcceptedEditingState Capture() => _accepted.Copy();
    internal void Adopt(DefinitionAcceptedEditingState accepted)
    {
        for (int index = 0; index < accepted.Inputs.Length; index++) accepted.Inputs[index].Touch();
        accepted.RootInput.Touch();
        _accepted = accepted;
    }

    internal void CopyRowsTo(DefinitionAcceptedEditingState replacement, IReadOnlySet<string> retired)
    {
        replacement.Rows.Clear();
        foreach ((string identity, List<long> rows) in _accepted.Rows)
            if (!retired.Any(prefix => identity == prefix || identity.StartsWith(prefix + "-", StringComparison.Ordinal)))
                replacement.Rows.Add(identity, [.. rows]);
        replacement.NextRow = _accepted.NextRow;
        replacement.Branches.Clear();
        foreach ((string identity, long token) in _accepted.Branches)
            replacement.Branches.Add(identity, token);
    }

    internal long RowIdentity(string identity, int count, int index)
    {
        if (!_accepted.Rows.TryGetValue(identity, out List<long>? rows))
            _accepted.Rows.Add(identity, rows = []);
        while (rows.Count < count) rows.Add(checked(++_accepted.NextRow));
        if (rows.Count > count) rows.RemoveRange(count, rows.Count - count);
        return rows[index];
    }

    internal void ClearRows(string identity)
    {
        foreach (string key in _accepted.Rows.Keys.Where(key => key == identity ||
            key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
            _accepted.Rows.Remove(key);
    }

    internal void RemoveRow(string identity, int index, string removedIdentity)
    {
        _accepted.Rows[identity].RemoveAt(index);
        ClearRows(removedIdentity);
    }

    internal void Clear()
    {
        foreach (DefinitionFieldInput field in Fields) field.Clear();
        _accepted.RootInput.Retire();
        _accepted.Root.Clear();
        _accepted.Rows.Clear();
    }
}

/// <summary>
/// The sole accepted payload behind stable field facades. Detached copies are
/// provisional and never escape through a public value accessor.
/// </summary>
internal sealed class DefinitionAcceptedEditingState
{
    private readonly string[] _names;

    internal DefinitionAcceptedEditingState(string[] names)
    {
        _names = names;
        var inputs = new DefinitionFieldState[names.Length];
        for (int index = 0; index < names.Length; index++)
            inputs[index] = new DefinitionFieldState(Root, names[index]);
        Inputs = inputs;
        RootInput = new(Root, null);
    }

    internal Dictionary<string, JsonElement> Root { get; } = new(StringComparer.Ordinal);
    internal DefinitionFieldState[] Inputs { get; }
    internal DefinitionFieldState RootInput { get; }
    internal HashSet<DefinitionPath> Omitted { get; } = [];
    internal Dictionary<string, List<long>> Rows { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, long> Branches { get; } = new(StringComparer.Ordinal);
    internal long NextRow;
    internal long InputRevision;

    internal DefinitionAcceptedEditingState Copy()
    {
        var copy = new DefinitionAcceptedEditingState(_names)
        {
            NextRow = NextRow,
            InputRevision = InputRevision
        };
        foreach ((string name, JsonElement value) in Root) copy.Root.Add(name, value);
        for (int index = 0; index < Inputs.Length; index++) Inputs[index].CopyTo(copy.Inputs[index]);
        RootInput.CopyTo(copy.RootInput);
        copy.Omitted.UnionWith(Omitted);
        foreach ((string key, List<long> rows) in Rows) copy.Rows.Add(key, [.. rows]);
        foreach ((string key, long token) in Branches) copy.Branches.Add(key, token);
        return copy;
    }

    internal void Retire()
    {
        foreach (DefinitionFieldState input in Inputs) input.Retire();
        Root.Clear();
        RootInput.Retire();
        Omitted.Clear();
        Rows.Clear();
        Branches.Clear();
    }
}

internal sealed class DefinitionFieldState
{
    private static long s_nextVersion;
    private readonly Dictionary<string, JsonElement> _root;
    private readonly string? _name;
    private List<string[]>? _invalidPaths;
    private List<(string[] Path, string Text)>? _draftTexts;
    private List<(string[] Path, DefinitionAnswerOrigin Origin)>? _origins;

    internal DefinitionFieldState(Dictionary<string, JsonElement> root, string? name)
    {
        _root = root;
        _name = name;
    }

    internal string Text = string.Empty;
    internal long Version { get; private set; }
    internal JsonElement? Value
    {
        get
        {
            if (_name is null) return JsonSerializer.SerializeToElement(_root);
            return _root.TryGetValue(_name, out JsonElement value) ? value : null;
        }
        set
        {
            Touch();
            if (_name is null)
            {
                _root.Clear();
                if (value is { ValueKind: JsonValueKind.Object } mapping)
                    foreach (JsonProperty property in mapping.EnumerateObject())
                        _root.Add(property.Name, property.Value.Clone());
            }
            else if (value is { } supplied)
            {
                _root[_name] = supplied;
            }
            else
            {
                _root.Remove(_name);
            }
        }
    }

    internal void Touch() => Version = System.Threading.Interlocked.Increment(ref s_nextVersion);
    internal bool HasInvalidPaths => _invalidPaths is { Count: > 0 };
    internal bool HasDraftTexts => _draftTexts is { Count: > 0 };
    internal bool HasOrigins => _origins is { Count: > 0 };
    internal List<string[]> InvalidPaths => _invalidPaths ??= [];
    internal List<(string[] Path, string Text)> DraftTexts => _draftTexts ??= [];
    internal List<(string[] Path, DefinitionAnswerOrigin Origin)> Origins => _origins ??= [];
    internal DefinitionAnswerOrigin Origin = DefinitionAnswerOrigin.Omitted;
    internal string? InputDiagnosticCode;

    internal void CopyTo(DefinitionFieldState copy)
    {
        copy.Text = Text;
        copy.Origin = Origin;
        copy.InputDiagnosticCode = InputDiagnosticCode;
        if (_invalidPaths is { Count: > 0 })
            foreach (string[] path in _invalidPaths) copy.InvalidPaths.Add(path.ToArray());
        if (_draftTexts is { Count: > 0 })
            foreach ((string[] path, string text) in _draftTexts) copy.DraftTexts.Add((path.ToArray(), text));
        if (_origins is { Count: > 0 })
            foreach ((string[] path, DefinitionAnswerOrigin origin) in _origins) copy.Origins.Add((path.ToArray(), origin));
        copy.Version = Version;
    }

    internal void Retire()
    {
        Text = string.Empty;
        Value = null;
        _invalidPaths?.Clear();
        _draftTexts?.Clear();
        _origins?.Clear();
        Origin = DefinitionAnswerOrigin.Omitted;
        InputDiagnosticCode = null;
        Touch();
    }

    internal void ScrubPrivate(Func<IReadOnlyList<string>, bool> isPrivatePath)
    {
        _draftTexts?.RemoveAll(draft => isPrivatePath(draft.Path));
        _invalidPaths?.RemoveAll(path => isPrivatePath(path));
        _origins?.RemoveAll(origin => isPrivatePath(origin.Path));
    }

    internal static bool EqualCollections(DefinitionFieldState left, DefinitionFieldState right)
    {
        int leftDrafts = left._draftTexts?.Count ?? 0;
        int rightDrafts = right._draftTexts?.Count ?? 0;
        if (leftDrafts != rightDrafts) return false;
        int leftInvalid = left._invalidPaths?.Count ?? 0;
        int rightInvalid = right._invalidPaths?.Count ?? 0;
        if (leftInvalid != rightInvalid) return false;
        for (int index = 0; index < leftDrafts; index++)
        {
            (string[] path, string text) a = left._draftTexts![index];
            (string[] path, string text) b = right._draftTexts![index];
            if (!string.Equals(a.text, b.text, StringComparison.Ordinal) || !a.path.SequenceEqual(b.path))
                return false;
        }
        for (int index = 0; index < leftInvalid; index++)
        {
            if (!left._invalidPaths![index].SequenceEqual(right._invalidPaths![index]))
                return false;
        }
        return true;
    }
}
