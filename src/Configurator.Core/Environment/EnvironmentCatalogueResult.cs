namespace Configurator.Core.Environment;

public sealed record EnvironmentCatalogueResult
{
    private readonly EnvironmentDiagnostic[] _diagnostics;

    internal EnvironmentCatalogueResult(
        EnvironmentCatalogue? catalogue,
        IEnumerable<EnvironmentDiagnostic> diagnostics)
    {
        Catalogue = catalogue;
        _diagnostics = diagnostics.ToArray();
    }

    public EnvironmentCatalogue? Catalogue { get; }
    public IReadOnlyList<EnvironmentDiagnostic> Diagnostics =>
        Array.AsReadOnly((EnvironmentDiagnostic[])_diagnostics.Clone());
}
