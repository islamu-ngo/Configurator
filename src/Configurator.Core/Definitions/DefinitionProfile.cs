namespace Configurator.Core.Definitions;

using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Configurator.Core.Composition;

/// <summary>Bounded exact-key syntax primitives; schema admission remains the codec's responsibility.</summary>
public static class DefinitionProfile
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public const string Id = "bounded-v2";
    public const string AuthoringSchemaId = "urn:islamu:configurator:authoring:v2";
    public const int RuntimeSchemaVersion = 2;

    /// <summary>Closed Phase 3 semantic capability registry. Required subsets are computed by admission.</summary>
    public static IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(new[]
    {
        "predicate.root", "projection.copy", "projection.index", "projection.list", "projection.literal", "projection.object",
        "projection.omit", "projection.repeat", "relationship.presence", "schema.array", "schema.boolean", "schema.bounds", "schema.const",
        "schema.enum", "schema.integer", "schema.notBlank", "schema.nullable", "schema.number", "schema.object",
        "schema.string", "schema.uniqueBy", "schema.map", "schema.variant", "schema.localRef",
        "schema.multipleOf", "schema.uniqueItems", "schema.assertFormat", "values.initial", "values.suggested"
    });

    public static byte[] CanonicalizeJson(ReadOnlyMemory<byte> bytes) => Canonicalize(bytes, yaml: false);
    public static byte[] CanonicalizeYaml(ReadOnlyMemory<byte> bytes) => Canonicalize(bytes, yaml: true);

    /// <summary>Separates bounded metadata identity from exact target names; the generated prefix is reserved.</summary>
    public static string FieldId(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        ValidateScalar(name);
        if (name.Length is > 0 and <= 128 && char.IsAsciiLetter(name[0])
            && !name.StartsWith("f-", StringComparison.Ordinal)
            && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
            return name;
        string jsonPointer = DefinitionPath.FromSegments([name]).ToPointer();
        return "f-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("field:" + jsonPointer)));
    }

    internal static void ValidateScalar(string text)
    {
        if (text.Length > SetupCompositionLimits.DefaultScalarCharacters)
            throw new SetupDefinitionException("scalar-limit");
        try { _ = StrictUtf8.GetByteCount(text); }
        catch (EncoderFallbackException) { throw new SetupDefinitionException("scalar-invalid"); }
    }

    private static byte[] Canonicalize(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        if (bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
            throw new SetupDefinitionException("document-limit");
        try
        {
            _ = StrictUtf8.GetCharCount(bytes.Span);
            CompositionMap tree = yaml
                ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None,
                    keyPolicy: CompositionKeyPolicy.Exact)
                : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None,
                    keyPolicy: CompositionKeyPolicy.Exact);
            using JsonDocument document = JsonDocument.Parse(SetupCompositionNormalizer.WriteJson(tree, CancellationToken.None));
            return DefinitionJson.Canonicalize(document.RootElement, keyPolicy: CompositionKeyPolicy.Exact);
        }
        catch (SetupDefinitionException) { throw; }
        catch (SetupCompositionException exception)
        {
            throw new SetupDefinitionException(exception.Code == SetupCompositionFailureCode.LimitExceeded
                ? "document-limit" : "document-invalid");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new SetupDefinitionException("document-invalid");
        }
    }
}
