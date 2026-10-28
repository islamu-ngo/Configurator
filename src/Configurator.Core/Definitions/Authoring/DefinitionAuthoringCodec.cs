namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;

public static class DefinitionAuthoringCodec
{
    public const string SchemaId = DefinitionProfile.AuthoringSchemaId;

    public static DefinitionAuthoringDocument ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static DefinitionAuthoringDocument ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static DefinitionAuthoringDocument Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            DefinitionSourceMap sourceMap;
            byte[] canonical = yaml ? DefinitionProfile.CanonicalizeYaml(bytes, out sourceMap)
                : DefinitionProfile.CanonicalizeJson(bytes, out sourceMap);
            using JsonDocument snapshot = JsonDocument.Parse(canonical);
            if (!snapshot.RootElement.TryGetProperty("$schema", out JsonElement schema)
                || schema.ValueKind != JsonValueKind.String || schema.GetString() != SchemaId)
                throw new SetupDefinitionException("authoring-schema");
            return new DefinitionAuthoringDocument(snapshot.RootElement, canonical, sourceMap);
        }
        catch (SetupDefinitionException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new SetupDefinitionException("authoring-invalid");
        }
    }
}
