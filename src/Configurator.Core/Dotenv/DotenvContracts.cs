namespace Configurator.Core.Environment;

public enum DotenvPurpose
{
    TemplatePlaceholders,
    EditableValues,
}

/// <summary>One-based coordinates in the acquired source, before line-ending normalization.</summary>
public sealed record DotenvSourceLocation(int Line, int Column);

public enum DotenvEntryKind
{
    EmptyPlaceholder,
    SafeDefault,
    LocalHumanValue,
    GeneratedValueReference,
}

public enum DotenvProvenance
{
    CatalogueDefault,
    UserInput,
    Generated,
}

public sealed record DotenvEntry
{
    public DotenvEntry(
        string key,
        string? value,
        DotenvEntryKind kind,
        bool isSecret,
        DotenvProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Value = value;
        Kind = kind;
        IsSecret = isSecret;
        Provenance = provenance;
    }

    public string Key { get; }
    public string? Value { get; }
    public DotenvEntryKind Kind { get; }
    public bool IsSecret { get; }
    public DotenvProvenance Provenance { get; }
    public override string ToString() => $"{nameof(DotenvEntry)}:{Kind}:{Provenance}:Protected={IsSecret}";
}

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

public sealed class DotenvParseResult
{
    private readonly EnvironmentDiagnostic[] _diagnostics;

    internal DotenvParseResult(DotenvDocument? document, IEnumerable<EnvironmentDiagnostic> diagnostics,
        IEnumerable<DotenvSourceLocation>? entryLocations = null, bool diagnosticsTruncated = false)
    {
        Document = document;
        _diagnostics = diagnostics.ToArray();
        EntryLocations = Array.AsReadOnly(entryLocations?.ToArray() ?? []);
        DiagnosticLocations = Array.AsReadOnly(_diagnostics.Select(diagnostic =>
        {
            const string prefix = "$.lines[";
            return diagnostic.Path.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(diagnostic.Path.AsSpan(prefix.Length, diagnostic.Path.Length - prefix.Length - 1),
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int line)
                ? new DotenvSourceLocation(line, 1) : new DotenvSourceLocation(1, 1);
        }).ToArray());
        DiagnosticsTruncated = diagnosticsTruncated;
    }

    public DotenvDocument? Document { get; }
    /// <summary>Locations of assignment values, in document entry order.</summary>
    public IReadOnlyList<DotenvSourceLocation> EntryLocations { get; }
    /// <summary>Locations of rejected lines, in diagnostic order; document errors point to 1:1.</summary>
    public IReadOnlyList<DotenvSourceLocation> DiagnosticLocations { get; }
    public bool DiagnosticsTruncated { get; }
    public IReadOnlyList<EnvironmentDiagnostic> Diagnostics =>
        Array.AsReadOnly((EnvironmentDiagnostic[])_diagnostics.Clone());
    public bool Succeeded => Document is not null && _diagnostics.Length == 0;
    public override string ToString() => $"{nameof(DotenvParseResult)}:Succeeded={Succeeded}:Diagnostics={_diagnostics.Length}";
}

public sealed class DotenvRenderResult
{
    private readonly byte[] _bytes;
    private readonly EnvironmentDiagnostic[] _diagnostics;

    internal DotenvRenderResult(IEnumerable<byte> bytes, IEnumerable<EnvironmentDiagnostic> diagnostics)
    {
        _bytes = bytes.ToArray();
        _diagnostics = diagnostics.ToArray();
    }

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
    public IReadOnlyList<EnvironmentDiagnostic> Diagnostics =>
        Array.AsReadOnly((EnvironmentDiagnostic[])_diagnostics.Clone());
    public bool Succeeded => _diagnostics.Length == 0;
    public override string ToString() => $"{nameof(DotenvRenderResult)}:Succeeded={Succeeded}:Bytes={_bytes.Length}:Diagnostics={_diagnostics.Length}";
}
