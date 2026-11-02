namespace Configurator.Core.Composition;

using System.Text.Json;

public sealed record CompositionYamlSource : CompositionSource
{
    private readonly byte[] _bytes;

    public CompositionYamlSource(ReadOnlyMemory<byte> bytes)
        : base(CompositionSourceKind.Yaml) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}
