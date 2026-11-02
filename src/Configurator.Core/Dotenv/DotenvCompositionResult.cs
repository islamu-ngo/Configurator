namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

public sealed class DotenvCompositionResult
{
    private readonly EnvironmentDiagnostic[] _diagnostics;

    internal DotenvCompositionResult(
        DotenvDocument document,
        DotenvReadinessResult readiness,
        IEnumerable<EnvironmentDiagnostic> diagnostics)
    {
        Document = document;
        Readiness = readiness;
        _diagnostics = diagnostics.ToArray();
    }

    public DotenvDocument Document { get; }
    public DotenvReadinessResult Readiness { get; }
    public IReadOnlyList<EnvironmentDiagnostic> Diagnostics =>
        Array.AsReadOnly((EnvironmentDiagnostic[])_diagnostics.Clone());
    public override string ToString() =>
        $"{nameof(DotenvCompositionResult)}:State={Readiness.State}:Diagnostics={_diagnostics.Length}";
}
