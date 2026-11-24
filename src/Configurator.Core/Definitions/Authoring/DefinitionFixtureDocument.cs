namespace Configurator.Core.Definitions.Authoring;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionFixtureCaseKind
{
    Prepare,
    Import
}

public sealed class DefinitionFixtureArtifactExpectation
{
    internal DefinitionFixtureArtifactExpectation(
        string id,
        string? fileName,
        byte[]? expectedBytes,
        string? expectedSha256,
        bool? deferred)
    {
        Id = id;
        FileName = fileName;
        _expectedBytes = expectedBytes;
        ExpectedSha256 = expectedSha256;
        Deferred = deferred;
    }

    private readonly byte[]? _expectedBytes;

    public string Id { get; }
    public string? FileName { get; }
    public ReadOnlyMemory<byte>? ExpectedBytes => _expectedBytes is null ? null : new ReadOnlyMemory<byte>((byte[])_expectedBytes.Clone());
    public string? ExpectedSha256 { get; }
    public bool? Deferred { get; }

    internal bool MatchesBytes(ReadOnlySpan<byte> actual) =>
        _expectedBytes is null || actual.SequenceEqual(_expectedBytes);
}

public sealed class DefinitionFixtureExpectation
{
    internal DefinitionFixtureExpectation(
        bool? ready,
        string? refusal,
        IReadOnlyList<string> diagnosticCodes,
        IReadOnlyList<DefinitionFixtureArtifactExpectation> artifacts,
        DefinitionImportCompatibility? importCompatibility,
        bool? importReady)
    {
        Ready = ready;
        Refusal = refusal;
        DiagnosticCodes = diagnosticCodes;
        Artifacts = artifacts;
        ImportCompatibility = importCompatibility;
        ImportReady = importReady;
    }

    public bool? Ready { get; }
    public string? Refusal { get; }
    public IReadOnlyList<string> DiagnosticCodes { get; }
    public IReadOnlyList<DefinitionFixtureArtifactExpectation> Artifacts { get; }
    public DefinitionImportCompatibility? ImportCompatibility { get; }
    public bool? ImportReady { get; }
}

public sealed class DefinitionFixtureCase
{
    internal DefinitionFixtureCase(
        string id,
        string? description,
        DefinitionFixtureCaseKind kind,
        bool draft,
        string? artifactId,
        AnswerSnapshot? answers,
        AnswerSnapshot? contextAnswers,
        byte[]? importBytes,
        DefinitionFixtureExpectation expect)
    {
        Id = id;
        Description = description;
        Kind = kind;
        Draft = draft;
        ArtifactId = artifactId;
        Answers = answers;
        ContextAnswers = contextAnswers;
        _importBytes = importBytes;
        Expect = expect;
    }

    private readonly byte[]? _importBytes;

    public string Id { get; }
    public string? Description { get; }
    public DefinitionFixtureCaseKind Kind { get; }
    public bool Draft { get; }
    public string? ArtifactId { get; }
    public AnswerSnapshot? Answers { get; }
    public AnswerSnapshot? ContextAnswers { get; }
    public ReadOnlyMemory<byte>? ImportBytes => _importBytes is null ? null : new ReadOnlyMemory<byte>((byte[])_importBytes.Clone());
    public DefinitionFixtureExpectation Expect { get; }
}

/// <summary>
/// Bounded data-only synthetic conformance fixture suite. Rejects executable, network, process,
/// oversized, duplicate-id, and vacuous zero-case inputs fail-closed before execution.
/// </summary>
public sealed class DefinitionFixtureDocument
{
    public const string SchemaId = "urn:islamu:configurator:fixtures:v" + ConfiguratorVersion.Current;
    public const string ContractVersion = ConfiguratorVersion.Current;
    public const int MaximumCases = 64;
    public const int MaximumArtifactExpectationsPerCase = 64;
    public const int MaximumDiagnosticCodesPerCase = 64;
    public const int MaximumCaseIdLength = 128;
    public const int MaximumDescriptionLength = 512;

    private static readonly HashSet<string> RootMembers = new(StringComparer.Ordinal)
    {
        "$schema", "fixtureContractVersion", "cases"
    };

    private static readonly HashSet<string> CaseMembers = new(StringComparer.Ordinal)
    {
        "id", "description", "kind", "draft", "artifactId", "answers", "contextAnswers", "contentUtf8", "contentBase64", "expect"
    };

    private static readonly HashSet<string> ExpectMembers = new(StringComparer.Ordinal)
    {
        "ready", "refusal", "diagnosticCodes", "artifacts", "importCompatibility", "importReady"
    };

    private static readonly HashSet<string> ArtifactExpectMembers = new(StringComparer.Ordinal)
    {
        "id", "fileName", "contentUtf8", "contentBase64", "sha256", "deferred"
    };

    private readonly byte[] _canonicalBytes;

    private DefinitionFixtureDocument(byte[] canonicalBytes, IReadOnlyList<DefinitionFixtureCase> cases)
    {
        _canonicalBytes = canonicalBytes;
        Digest = ArtifactDigest.Compute(canonicalBytes);
        Cases = cases;
    }

    public ArtifactDigest Digest { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => new((byte[])_canonicalBytes.Clone());
    public IReadOnlyList<DefinitionFixtureCase> Cases { get; }

    public static DefinitionFixtureDocument ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, yaml: false);
    public static DefinitionFixtureDocument ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, yaml: true);

    private static DefinitionFixtureDocument Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        byte[] canonical = yaml
            ? DefinitionProfile.CanonicalizeYaml(bytes, out DefinitionSourceMap _)
            : DefinitionProfile.CanonicalizeJson(bytes, out DefinitionSourceMap _);

        using JsonDocument doc = JsonDocument.Parse(canonical);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("fixture-invalid");

        EnsureAllowedMembers(root, RootMembers);

        if (root.TryGetProperty("$schema", out JsonElement schemaElement)
            && (schemaElement.ValueKind != JsonValueKind.String
                || !string.Equals(schemaElement.GetString(), SchemaId, StringComparison.Ordinal)))
            throw new DefinitionException("fixture-schema");

        if (!root.TryGetProperty("fixtureContractVersion", out JsonElement versionElement)
            || versionElement.ValueKind != JsonValueKind.String
            || !string.Equals(versionElement.GetString(), ContractVersion, StringComparison.Ordinal))
            throw new DefinitionException("fixture-version");

        if (!root.TryGetProperty("cases", out JsonElement casesElement) || casesElement.ValueKind != JsonValueKind.Array)
            throw new DefinitionException("fixture-invalid");

        int caseCount = casesElement.GetArrayLength();
        if (caseCount == 0)
            throw new DefinitionException("fixture-empty");
        if (caseCount > MaximumCases)
            throw new DefinitionException("fixture-limit");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var parsedCases = new List<DefinitionFixtureCase>(caseCount);
        int index = 0;
        foreach (JsonElement item in casesElement.EnumerateArray())
        {
            string casePath = "/cases/" + index.ToString(CultureInfo.InvariantCulture);
            DefinitionFixtureCase parsed = ParseCase(item, casePath);
            if (!seenIds.Add(parsed.Id))
                throw new DefinitionException("fixture-duplicate-case").AtSchemaPath(casePath);
            parsedCases.Add(parsed);
            index++;
        }

        return new DefinitionFixtureDocument(canonical, parsedCases.AsReadOnly());
    }

    private static DefinitionFixtureCase ParseCase(JsonElement element, string casePath)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(casePath);

        EnsureAllowedMembers(element, CaseMembers, casePath);

        string id = ReadRequiredString(element, "id", casePath);
        if (!IsValidCaseId(id))
            throw new DefinitionException("fixture-case-id").AtSchemaPath(casePath + "/id");

        string? description = ReadOptionalString(element, "description", MaximumDescriptionLength, casePath + "/description");

        string kindText = ReadRequiredString(element, "kind", casePath);
        DefinitionFixtureCaseKind kind = kindText switch
        {
            "prepare" => DefinitionFixtureCaseKind.Prepare,
            "import" => DefinitionFixtureCaseKind.Import,
            _ => throw new DefinitionException("fixture-kind").AtSchemaPath(casePath + "/kind")
        };

        bool draft = ReadOptionalBool(element, "draft", casePath + "/draft") ?? false;
        string? artifactId = ReadOptionalString(element, "artifactId", MaximumCaseIdLength, casePath + "/artifactId");

        bool hasAnswers = element.TryGetProperty("answers", out JsonElement answersElement);
        bool hasContextAnswers = element.TryGetProperty("contextAnswers", out JsonElement contextElement);
        bool hasContentUtf8 = element.TryGetProperty("contentUtf8", out JsonElement contentUtf8Element);
        bool hasContentBase64 = element.TryGetProperty("contentBase64", out JsonElement contentBase64Element);

        AnswerSnapshot? answers = null;
        AnswerSnapshot? contextAnswers = null;
        byte[]? importBytes = null;

        if (kind == DefinitionFixtureCaseKind.Prepare)
        {
            if (!hasAnswers || hasContentUtf8 || hasContentBase64 || hasContextAnswers)
                throw new DefinitionException("fixture-invalid").AtSchemaPath(casePath);
            answers = ParseInlineAnswers(answersElement, casePath + "/answers");
        }
        else
        {
            if (draft || hasAnswers || string.IsNullOrEmpty(artifactId) || hasContentUtf8 == hasContentBase64)
                throw new DefinitionException("fixture-invalid").AtSchemaPath(casePath);
            importBytes = hasContentUtf8
                ? Encoding.UTF8.GetBytes(RequireString(contentUtf8Element, casePath + "/contentUtf8"))
                : DecodeBase64(contentBase64Element, casePath + "/contentBase64");
            if (hasContextAnswers)
                contextAnswers = ParseInlineAnswers(contextElement, casePath + "/contextAnswers");
        }

        if (!element.TryGetProperty("expect", out JsonElement expectElement))
            throw new DefinitionException("fixture-expectation-empty").AtSchemaPath(casePath + "/expect");

        DefinitionFixtureExpectation expect = ParseExpectation(expectElement, kind, casePath + "/expect");
        return new DefinitionFixtureCase(id, description, kind, draft, artifactId, answers, contextAnswers, importBytes, expect);
    }

    private static DefinitionFixtureExpectation ParseExpectation(
        JsonElement element,
        DefinitionFixtureCaseKind kind,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);

        EnsureAllowedMembers(element, ExpectMembers, path);

        bool? ready = ReadOptionalBool(element, "ready", path + "/ready");
        string? refusal = ReadOptionalString(element, "refusal", MaximumCaseIdLength, path + "/refusal");

        var diagnosticCodes = new List<string>();
        if (element.TryGetProperty("diagnosticCodes", out JsonElement codesElement))
        {
            if (codesElement.ValueKind != JsonValueKind.Array)
                throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/diagnosticCodes");
            if (codesElement.GetArrayLength() > MaximumDiagnosticCodesPerCase)
                throw new DefinitionException("fixture-limit").AtSchemaPath(path + "/diagnosticCodes");
            foreach (JsonElement item in codesElement.EnumerateArray())
            {
                string code = RequireString(item, path + "/diagnosticCodes");
                if (string.IsNullOrWhiteSpace(code) || code.Length > MaximumCaseIdLength)
                    throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/diagnosticCodes");
                diagnosticCodes.Add(code);
            }
        }

        var artifacts = new List<DefinitionFixtureArtifactExpectation>();
        if (element.TryGetProperty("artifacts", out JsonElement artifactsElement))
        {
            if (kind != DefinitionFixtureCaseKind.Prepare || artifactsElement.ValueKind != JsonValueKind.Array)
                throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/artifacts");
            if (artifactsElement.GetArrayLength() > MaximumArtifactExpectationsPerCase)
                throw new DefinitionException("fixture-limit").AtSchemaPath(path + "/artifacts");
            int artIndex = 0;
            foreach (JsonElement item in artifactsElement.EnumerateArray())
            {
                artifacts.Add(ParseArtifactExpectation(item, path + "/artifacts/" + artIndex.ToString(CultureInfo.InvariantCulture)));
                artIndex++;
            }
        }

        DefinitionImportCompatibility? importCompatibility = null;
        if (element.TryGetProperty("importCompatibility", out JsonElement compatElement))
        {
            if (kind != DefinitionFixtureCaseKind.Import || compatElement.ValueKind != JsonValueKind.String)
                throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/importCompatibility");
            importCompatibility = compatElement.GetString() switch
            {
                "compatible" => DefinitionImportCompatibility.Compatible,
                "changes-required" => DefinitionImportCompatibility.ChangesRequired,
                "incomplete" => DefinitionImportCompatibility.Incomplete,
                "unsupported" => DefinitionImportCompatibility.Unsupported,
                "invalid-input" => DefinitionImportCompatibility.InvalidInput,
                _ => throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/importCompatibility")
            };
        }

        bool? importReady = ReadOptionalBool(element, "importReady", path + "/importReady");
        if (importReady.HasValue && kind != DefinitionFixtureCaseKind.Import)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/importReady");

        bool hasAnyAssertion = ready.HasValue
            || refusal is not null
            || diagnosticCodes.Count > 0
            || artifacts.Count > 0
            || importCompatibility.HasValue
            || importReady.HasValue;

        if (!hasAnyAssertion)
            throw new DefinitionException("fixture-expectation-empty").AtSchemaPath(path);

        return new DefinitionFixtureExpectation(
            ready,
            refusal,
            diagnosticCodes.AsReadOnly(),
            artifacts.AsReadOnly(),
            importCompatibility,
            importReady);
    }

    private static DefinitionFixtureArtifactExpectation ParseArtifactExpectation(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);

        EnsureAllowedMembers(element, ArtifactExpectMembers, path);

        string id = ReadRequiredString(element, "id", path);
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaximumCaseIdLength)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/id");

        string? fileName = ReadOptionalString(element, "fileName", MaximumCaseIdLength, path + "/fileName");
        bool hasUtf8 = element.TryGetProperty("contentUtf8", out JsonElement utf8Element);
        bool hasBase64 = element.TryGetProperty("contentBase64", out JsonElement base64Element);
        if (hasUtf8 && hasBase64)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);

        byte[]? expectedBytes = hasUtf8
            ? Encoding.UTF8.GetBytes(RequireString(utf8Element, path + "/contentUtf8"))
            : hasBase64 ? DecodeBase64(base64Element, path + "/contentBase64") : null;

        string? sha256 = ReadOptionalString(element, "sha256", 64, path + "/sha256");
        if (sha256 is not null && !IsLowerHexSha256(sha256))
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path + "/sha256");

        bool? deferred = ReadOptionalBool(element, "deferred", path + "/deferred");

        return new DefinitionFixtureArtifactExpectation(id, fileName, expectedBytes, sha256, deferred);
    }

    private static AnswerSnapshot ParseInlineAnswers(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);
        try
        {
            return AnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(element.GetRawText()));
        }
        catch (DefinitionException exception)
        {
            throw new DefinitionException(exception.Code).AtSchemaPath(path);
        }
    }

    private static void EnsureAllowedMembers(JsonElement element, HashSet<string> allowed, string? path = null)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                DefinitionException exception = new("fixture-member");
                throw path is null ? exception : exception.AtSchemaPath(path);
            }
        }
    }

    private static string ReadRequiredString(JsonElement element, string name, string parentPath)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(parentPath + "/" + name);
        return value.GetString()!;
    }

    private static string? ReadOptionalString(JsonElement element, string name, int maxLength, string path)
    {
        if (!element.TryGetProperty(name, out JsonElement value)) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);
        string text = value.GetString()!;
        if (text.Length == 0 || text.Length > maxLength)
            throw new DefinitionException("fixture-invalid").AtSchemaPath(path);
        return text;
    }

    private static bool? ReadOptionalBool(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new DefinitionException("fixture-invalid").AtSchemaPath(path)
        };
    }

    private static string RequireString(JsonElement element, string path) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw new DefinitionException("fixture-invalid").AtSchemaPath(path);

    private static byte[] DecodeBase64(JsonElement element, string path)
    {
        string encoded = RequireString(element, path);
        try { return Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new DefinitionException("fixture-invalid").AtSchemaPath(path); }
    }

    private static bool IsValidCaseId(string id)
    {
        if (id.Length is 0 or > MaximumCaseIdLength) return false;
        if (!char.IsAsciiLetterLower(id[0]) && !char.IsAsciiDigit(id[0])) return false;
        for (int index = 1; index < id.Length; index++)
        {
            char character = id[index];
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character is not ('.' or '_' or '-'))
                return false;
        }
        return true;
    }

    private static bool IsLowerHexSha256(string text)
    {
        if (text.Length != 64) return false;
        foreach (char character in text)
        {
            if (character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }
        return true;
    }
}
