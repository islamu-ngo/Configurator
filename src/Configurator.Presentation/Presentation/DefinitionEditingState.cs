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
        _accepted = new(definitions.Count);
        Fields = Array.AsReadOnly(definitions.Select((definition, index) =>
            createField(definition, () => _accepted.Inputs[index])).ToArray());
        Privacy = privacy;
    }

    internal IReadOnlyList<DefinitionFieldInput> Fields { get; }
    internal DefinitionSessionPrivacy Privacy { get; set; }
    internal long InputRevision => _accepted.InputRevision;
    internal HashSet<DefinitionPath> Omitted => _accepted.Omitted;
    internal DefinitionFieldState Input(int index) => _accepted.Inputs[index];

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
        _accepted.Rows.Clear();
    }
}

/// <summary>
/// The sole accepted payload behind stable field facades. Detached copies are
/// provisional and never escape through a public value accessor.
/// </summary>
internal sealed class DefinitionAcceptedEditingState(int count)
{
    internal DefinitionFieldState[] Inputs { get; } =
        Enumerable.Range(0, count).Select(_ => new DefinitionFieldState()).ToArray();
    internal HashSet<DefinitionPath> Omitted { get; } = [];
    internal Dictionary<string, List<long>> Rows { get; } = new(StringComparer.Ordinal);
    internal long NextRow;
    internal long InputRevision;

    internal DefinitionAcceptedEditingState Copy()
    {
        var copy = new DefinitionAcceptedEditingState(Inputs.Length)
        {
            NextRow = NextRow,
            InputRevision = InputRevision
        };
        for (int index = 0; index < Inputs.Length; index++) copy.Inputs[index] = Inputs[index].Copy();
        copy.Omitted.UnionWith(Omitted);
        foreach ((string key, List<long> rows) in Rows) copy.Rows.Add(key, [.. rows]);
        return copy;
    }

    internal void Retire()
    {
        foreach (DefinitionFieldState input in Inputs) input.Retire();
        Omitted.Clear();
        Rows.Clear();
    }
}

internal sealed class DefinitionFieldState
{
    internal string Text = string.Empty;
    internal JsonElement? Value;
    internal List<string[]> InvalidPaths { get; } = [];
    internal List<(string[] Path, string Text)> DraftTexts { get; } = [];
    internal List<(string[] Path, DefinitionAnswerOrigin Origin)> Origins { get; } = [];
    internal DefinitionAnswerOrigin Origin = DefinitionAnswerOrigin.Omitted;
    internal string? InputDiagnosticCode;

    internal DefinitionFieldState Copy()
    {
        var copy = new DefinitionFieldState
        {
            Text = Text,
            Value = Value,
            Origin = Origin,
            InputDiagnosticCode = InputDiagnosticCode
        };
        copy.InvalidPaths.AddRange(InvalidPaths.Select(path => path.ToArray()));
        copy.DraftTexts.AddRange(DraftTexts.Select(draft => (draft.Path.ToArray(), draft.Text)));
        copy.Origins.AddRange(Origins.Select(origin => (origin.Path.ToArray(), origin.Origin)));
        return copy;
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
