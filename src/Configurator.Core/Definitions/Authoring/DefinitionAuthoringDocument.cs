namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;

/// <summary>A bounded, normalized source snapshot; compilation performs semantic admission.</summary>
public sealed class DefinitionAuthoringDocument
{
    private readonly byte[] _canonicalBytes;

    internal DefinitionAuthoringDocument(JsonElement root, byte[] canonicalBytes)
    {
        Root = root.Clone();
        _canonicalBytes = canonicalBytes.ToArray();
    }

    public JsonElement Root { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => _canonicalBytes.ToArray();
}
