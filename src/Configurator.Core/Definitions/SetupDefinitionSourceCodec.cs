namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions.Authoring;

/// <summary>Selects one closed content contract after the caller explicitly chooses local syntax.</summary>
public static class SetupDefinitionSourceCodec
{
    public static SetupDefinition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static SetupDefinition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static SetupDefinition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            CompositionMap tree = yaml
                ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None)
                : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None);
            using JsonDocument parsed = JsonDocument.Parse(SetupCompositionNormalizer.WriteJson(tree, CancellationToken.None));
            JsonElement root = parsed.RootElement;
            if (root.TryGetProperty("$schema", out JsonElement schema))
            {
                if (root.TryGetProperty("schemaVersion", out _) || schema.ValueKind != JsonValueKind.String
                    || schema.GetString() != DefinitionAuthoringCodec.SchemaId)
                    throw new SetupDefinitionException("source-discriminator");
                return DefinitionAuthoringCompiler.Compile(yaml ? DefinitionAuthoringCodec.ParseYaml(bytes) : DefinitionAuthoringCodec.ParseJson(bytes));
            }
            if (!root.TryGetProperty("schemaVersion", out _)) throw new SetupDefinitionException("source-discriminator");
            return yaml ? SetupDefinitionCodec.ParseYaml(bytes) : SetupDefinitionCodec.ParseJson(bytes);
        }
        catch (SetupCompositionException exception)
        {
            throw new SetupDefinitionException(exception.Code == SetupCompositionFailureCode.LimitExceeded ? "document-limit" : "document-invalid");
        }
        catch (JsonException) { throw new SetupDefinitionException("document-invalid"); }
    }
}
