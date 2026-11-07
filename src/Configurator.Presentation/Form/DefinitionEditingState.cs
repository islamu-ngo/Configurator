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
    internal void Adopt(DefinitionAcceptedEditingState accepted) => _accepted = accepted;

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
        Inputs = names.Select(name => new DefinitionFieldState(
            () => Root.TryGetValue(name, out JsonElement value) ? value : null,
            value =>
            {
                if (value is { } supplied) Root[name] = supplied;
                else Root.Remove(name);
            })).ToArray();
        RootInput = new(() => JsonSerializer.SerializeToElement(Root), value =>
        {
            Root.Clear();
            if (value is { ValueKind: JsonValueKind.Object } mapping)
                foreach (JsonProperty property in mapping.EnumerateObject()) Root.Add(property.Name, property.Value.Clone());
        });
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

internal sealed class DefinitionFieldState(Func<JsonElement?> readValue, Action<JsonElement?> writeValue)
{
    internal string Text = string.Empty;
    internal JsonElement? Value { get => readValue(); set => writeValue(value); }
    internal List<string[]> InvalidPaths { get; } = [];
    internal List<(string[] Path, string Text)> DraftTexts { get; } = [];
    internal List<(string[] Path, DefinitionAnswerOrigin Origin)> Origins { get; } = [];
    internal DefinitionAnswerOrigin Origin = DefinitionAnswerOrigin.Omitted;
    internal string? InputDiagnosticCode;

    internal void CopyTo(DefinitionFieldState copy)
    {
        copy.Text = Text;
        copy.Origin = Origin;
        copy.InputDiagnosticCode = InputDiagnosticCode;
        copy.InvalidPaths.AddRange(InvalidPaths.Select(path => path.ToArray()));
        copy.DraftTexts.AddRange(DraftTexts.Select(draft => (draft.Path.ToArray(), draft.Text)));
        copy.Origins.AddRange(Origins.Select(origin => (origin.Path.ToArray(), origin.Origin)));
    }

    internal void Retire()
    {
        Text = string.Empty;
        Value = null;
        InvalidPaths.Clear();
        DraftTexts.Clear();
        Origins.Clear();
        Origin = DefinitionAnswerOrigin.Omitted;
        InputDiagnosticCode = null;
    }
}
