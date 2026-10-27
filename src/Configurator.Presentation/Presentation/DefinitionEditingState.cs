namespace Configurator.Presentation;

/// <summary>Aggregate-owned input lifetime and logical rows, accessed only through the form's mutation gate.</summary>
internal sealed class DefinitionEditingState(
    IReadOnlyList<DefinitionFieldInput> fields, DefinitionSessionPrivacy privacy)
{
    private readonly Dictionary<string, List<long>> _rows = new(StringComparer.Ordinal);
    private long _nextRow;

    internal IReadOnlyList<DefinitionFieldInput> Fields { get; } = fields;
    internal DefinitionSessionPrivacy Privacy { get; set; } = privacy;
    internal long InputRevision { get; private set; }

    internal void AdvanceRevision() => InputRevision = checked(InputRevision + 1);

    internal long RowIdentity(string identity, int count, int index)
    {
        if (!_rows.TryGetValue(identity, out List<long>? rows))
            _rows.Add(identity, rows = []);
        while (rows.Count < count) rows.Add(checked(++_nextRow));
        if (rows.Count > count) rows.RemoveRange(count, rows.Count - count);
        return rows[index];
    }

    internal void ClearRows(string identity)
    {
        foreach (string key in _rows.Keys.Where(key => key == identity ||
            key.StartsWith(identity + "-", StringComparison.Ordinal)).ToArray())
            _rows.Remove(key);
    }

    internal void RemoveRow(string identity, int index, string removedIdentity)
    {
        _rows[identity].RemoveAt(index);
        ClearRows(removedIdentity);
    }

    internal void Clear()
    {
        foreach (DefinitionFieldInput field in Fields) field.Clear();
        _rows.Clear();
    }
}
