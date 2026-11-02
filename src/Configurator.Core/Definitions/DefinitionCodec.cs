namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;

public static class DefinitionCodec
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = CompositionLimits.DefaultNestingDepth,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static Definition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static Definition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static Definition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        DefinitionSourceMap sourceMap = DefinitionSourceMap.Empty;
        try
        {
            byte[] canonical = yaml ? DefinitionProfile.CanonicalizeYaml(bytes, out sourceMap)
                : DefinitionProfile.CanonicalizeJson(bytes, out sourceMap);
            using JsonDocument parsed = JsonDocument.Parse(canonical);
            var capabilities = new SortedSet<string>(StringComparer.Ordinal);
            DefinitionNode configuration = DefinitionAdmission.Admit(parsed.RootElement, capabilities);
            JsonElement declared = parsed.RootElement.GetProperty("requiredCapabilities");
            if (declared.ValueKind != JsonValueKind.Array
                || !declared.EnumerateArray().Select(item => item.GetString()).SequenceEqual(capabilities, StringComparer.Ordinal))
                throw new DefinitionException("capabilities-invalid");
            return new Definition(parsed.RootElement, canonical, configuration, sourceMap);
        }
        catch (DefinitionException exception) { throw exception.WithLocation(sourceMap, ""); }
        catch (CompositionException) { throw new DefinitionException("definition-limit"); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            throw new DefinitionException("definition-invalid");
        }
    }

    /// <summary>Fills computed profile/capabilities only, then uses public runtime admission.</summary>
    internal static Definition AdmitCompiled(JsonObject runtime)
    {
        runtime["profile"] = DefinitionProfile.Id;
        runtime["requiredCapabilities"] = new JsonArray();
        byte[] bytes = DefinitionProfile.CanonicalizeJson(JsonSerializer.SerializeToUtf8Bytes(runtime));
        using JsonDocument document = JsonDocument.Parse(bytes);
        var capabilities = new SortedSet<string>(StringComparer.Ordinal);
        _ = DefinitionAdmission.Admit(document.RootElement, capabilities);
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        return ParseJson(JsonSerializer.SerializeToUtf8Bytes(runtime));
    }

}
