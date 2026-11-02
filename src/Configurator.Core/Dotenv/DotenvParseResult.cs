namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

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
