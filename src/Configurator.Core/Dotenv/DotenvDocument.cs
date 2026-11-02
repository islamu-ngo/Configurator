namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public sealed class DotenvDocument
{
    private readonly DotenvEntry[] _entries;

    public DotenvDocument(IEnumerable<DotenvEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries.ToArray();
    }

    public IReadOnlyList<DotenvEntry> Entries =>
        Array.AsReadOnly((DotenvEntry[])_entries.Clone());

    public override string ToString() => $"{nameof(DotenvDocument)}:Count={_entries.Length}";
}
