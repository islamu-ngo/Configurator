namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public sealed class DotenvReadinessResult
{
    private readonly string[] _missing;
    private readonly string[] _blocked;
    private readonly EnvironmentDiagnostic[] _diagnostics;

    internal DotenvReadinessResult(
        DotenvReadinessState state,
        IEnumerable<string> missing,
        IEnumerable<string> blocked,
        IEnumerable<EnvironmentDiagnostic> diagnostics)
    {
        State = state;
        _missing = missing.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        _blocked = blocked.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        _diagnostics = diagnostics.ToArray();
    }

    public DotenvReadinessState State { get; }
    public IReadOnlyList<string> Missing => Array.AsReadOnly((string[])_missing.Clone());
    public IReadOnlyList<string> Blocked => Array.AsReadOnly((string[])_blocked.Clone());
    public IReadOnlyList<EnvironmentDiagnostic> Diagnostics =>
        Array.AsReadOnly((EnvironmentDiagnostic[])_diagnostics.Clone());
    public override string ToString() =>
        $"{nameof(DotenvReadinessResult)}:State={State}:Missing={_missing.Length}:Blocked={_blocked.Length}";
}
