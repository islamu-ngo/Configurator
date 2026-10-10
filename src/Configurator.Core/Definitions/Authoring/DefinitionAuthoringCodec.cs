namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;
using Configurator.Core.Composition;

public static class DefinitionAuthoringCodec
{
    public const string SchemaId = "urn:islamu:configurator:authoring:v1";

    public static DefinitionAuthoringDocument ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static DefinitionAuthoringDocument ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static DefinitionAuthoringDocument Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            CompositionMap tree = yaml
                ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None)
                : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None);
            using JsonDocument parsed = JsonDocument.Parse(SetupCompositionNormalizer.WriteJson(tree, CancellationToken.None));
            byte[] canonical = DefinitionJson.Canonicalize(parsed.RootElement);
            using JsonDocument snapshot = JsonDocument.Parse(canonical);
            if (!snapshot.RootElement.TryGetProperty("$schema", out JsonElement schema)
                || schema.ValueKind != JsonValueKind.String || schema.GetString() != SchemaId)
                throw new SetupDefinitionException("authoring-schema");
            return new DefinitionAuthoringDocument(snapshot.RootElement, canonical);
        }
        catch (SetupDefinitionException) { throw; }
        catch (SetupCompositionException exception)
        {
            throw new SetupDefinitionException(exception.Code == SetupCompositionFailureCode.LimitExceeded
                ? "document-limit" : "document-invalid");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new SetupDefinitionException("authoring-invalid");
        }
    }
}
