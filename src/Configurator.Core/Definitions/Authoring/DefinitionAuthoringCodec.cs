namespace Configurator.Core.Definitions.Authoring;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

public static class DefinitionAuthoringCodec
{
    public const string SchemaId = DefinitionProfile.AuthoringSchemaId;

    public static DefinitionAuthoringDocument ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static DefinitionAuthoringDocument ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    public static bool TryParseJson(
        ReadOnlyMemory<byte> bytes,
        [NotNullWhen(true)] out DefinitionAuthoringDocument? document,
        [NotNullWhen(false)] out DefinitionAuthoringDiagnostic? diagnostic) =>
        TryParse(bytes, yaml: false, out document, out diagnostic);

    public static bool TryParseYaml(
        ReadOnlyMemory<byte> bytes,
        [NotNullWhen(true)] out DefinitionAuthoringDocument? document,
        [NotNullWhen(false)] out DefinitionAuthoringDiagnostic? diagnostic) =>
        TryParse(bytes, yaml: true, out document, out diagnostic);

    private static bool TryParse(
        ReadOnlyMemory<byte> bytes,
        bool yaml,
        [NotNullWhen(true)] out DefinitionAuthoringDocument? document,
        [NotNullWhen(false)] out DefinitionAuthoringDiagnostic? diagnostic)
    {
        try
        {
            document = Parse(bytes, yaml);
            diagnostic = null;
            return true;
        }
        catch (DefinitionException exception)
        {
            document = null;
            diagnostic = exception.ToAuthoringDiagnostic();
            return false;
        }
    }

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
                throw new DefinitionException("authoring-schema").WithLocation(sourceMap, "/$schema");
            return new DefinitionAuthoringDocument(snapshot.RootElement, canonical, sourceMap);
        }
        catch (DefinitionException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new DefinitionException("authoring-invalid");
        }
    }
}
