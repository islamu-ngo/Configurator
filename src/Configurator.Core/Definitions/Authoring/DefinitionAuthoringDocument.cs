namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;

/// <summary>A bounded, normalized source snapshot; compilation performs semantic admission.</summary>
public sealed class DefinitionAuthoringDocument
{
    private readonly byte[] _canonicalBytes;

    internal DefinitionAuthoringDocument(JsonElement root, byte[] canonicalBytes, DefinitionSourceMap? sourceMap = null)
    {
        Root = root.Clone();
        _canonicalBytes = canonicalBytes.ToArray();
        SourceMap = sourceMap ?? DefinitionSourceMap.Empty;
    }

    public JsonElement Root { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => _canonicalBytes.ToArray();
    public DefinitionSourceMap SourceMap { get; }
}
