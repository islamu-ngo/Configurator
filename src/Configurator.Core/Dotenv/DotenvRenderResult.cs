namespace Configurator.Core.Dotenv;

using Configurator.Core.Environment;

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
