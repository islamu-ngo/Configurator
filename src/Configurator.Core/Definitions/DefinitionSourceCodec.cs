namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Definitions.Authoring;

/// <summary>Selects one closed content contract after the caller explicitly chooses local syntax.</summary>
public static class DefinitionSourceCodec
{
    public static Definition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static Definition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static Definition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            byte[] canonical = yaml ? DefinitionProfile.CanonicalizeYaml(bytes) : DefinitionProfile.CanonicalizeJson(bytes);
            using JsonDocument parsed = JsonDocument.Parse(canonical);
            JsonElement root = parsed.RootElement;
            if (root.TryGetProperty("$schema", out JsonElement schema))
            {
                if (root.TryGetProperty("schemaVersion", out _) || schema.ValueKind != JsonValueKind.String
                    || schema.GetString() != DefinitionAuthoringCodec.SchemaId)
                    throw new DefinitionException("source-discriminator");
                return DefinitionAuthoringCompiler.Compile(yaml ? DefinitionAuthoringCodec.ParseYaml(bytes) : DefinitionAuthoringCodec.ParseJson(bytes));
            }
            if (!root.TryGetProperty("schemaVersion", out _)) throw new DefinitionException("source-discriminator");
            return yaml ? DefinitionCodec.ParseYaml(bytes) : DefinitionCodec.ParseJson(bytes);
        }
        catch (JsonException) { throw new DefinitionException("document-invalid"); }
        catch (InvalidOperationException) { throw new DefinitionException("document-invalid"); }
    }
}
