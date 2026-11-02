namespace Configurator.Core.Composition;

using System.Text.Json;

public sealed record CompositionJsonSource : CompositionSource
{
    private readonly byte[] _bytes;

    public CompositionJsonSource(ReadOnlyMemory<byte> bytes)
        : base(CompositionSourceKind.Json) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}
