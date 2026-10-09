namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text.Json;

public sealed class SetupDefinitionException : Exception
{
    internal SetupDefinitionException(string code) : base(code) => Code = code;
    public string Code { get; }
}

public enum SetupDisplayMode { Guided, Advanced, AllDeclared }
public enum SetupFieldSensitivity { Public, Sensitive, Secret }
public enum SetupFieldDisclosure { Essential, Guided, Advanced }

public sealed class SetupDefinition
{
    private readonly byte[] _canonicalBytes;

    internal SetupDefinition(JsonElement document, byte[] canonicalBytes)
    {
        Document = document.Clone();
        _canonicalBytes = (byte[])canonicalBytes.Clone();
        Digest = ArtifactDigest.Compute(canonicalBytes);
        FamilyId = document.GetProperty("familyId").GetGuid();
        Revision = document.GetProperty("revision").GetInt64();
        Fields = Array.AsReadOnly(document.GetProperty("fields").EnumerateArray()
            .Select(item => new SetupFieldDefinition(item)).ToArray());
        Artifacts = Array.AsReadOnly(document.GetProperty("artifacts").EnumerateArray()
            .Select(item => new SetupArtifactDefinition(item)).ToArray());
        ApplicationVersions = Strings("applicationVersions");
        EntryPoints = Strings("entryPoints");
        Locales = Strings("locales");
    }

    internal JsonElement Document { get; }
    public Guid FamilyId { get; }
    public long Revision { get; }
    public ArtifactDigest Digest { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => new((byte[])_canonicalBytes.Clone());
    public string ApplicationId => Document.GetProperty("applicationId").GetString()!;
    public string Publisher => Document.GetProperty("publisher").GetString()!;
    public string Title => Document.GetProperty("title").GetString()!;
    public IReadOnlyList<string> ApplicationVersions { get; }
    public IReadOnlyList<string> EntryPoints { get; }
    public IReadOnlyList<string> Locales { get; }
    public IReadOnlyList<SetupFieldDefinition> Fields { get; }
    public IReadOnlyList<SetupArtifactDefinition> Artifacts { get; }
    public override string ToString() => $"{nameof(SetupDefinition)}:{FamilyId:D}:{Revision}:{Digest}";

    private ReadOnlyCollection<string> Strings(string name) => Array.AsReadOnly(
        Document.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray());
}

public sealed class SetupFieldDefinition
{
    internal SetupFieldDefinition(JsonElement data)
    {
        Data = data.Clone();
        Properties = Array.AsReadOnly(data.TryGetProperty("properties", out var properties)
            ? properties.EnumerateArray().Select(item => new SetupFieldDefinition(item)).ToArray() : []);
        UniqueBy = Array.AsReadOnly(data.TryGetProperty("uniqueBy", out var path)
            ? path.EnumerateArray().Select(item => item.GetString()!).ToArray() : []);
    }
    internal JsonElement Data { get; }
    public string Id => Data.GetProperty("id").GetString()!;
    public string Type => Data.GetProperty("type").GetString()!;
    public string Title => Data.GetProperty("title").GetString()!;
    public string? Help => Text("help");
    public string? Group => Text("group");
    public string? Format => Text("format");
    public string? Consequence => Text("consequence");
    public bool Required => Data.TryGetProperty("required", out var value) && value.GetBoolean();
    public string? ItemType => Text("itemType");
    public int? MinimumItems => Data.TryGetProperty("minItems", out var value) ? value.GetInt32() : null;
    public int? MaximumItems => Data.TryGetProperty("maxItems", out var value) ? value.GetInt32() : null;
    public IReadOnlyList<SetupFieldDefinition> Properties { get; }
    public IReadOnlyList<string> UniqueBy { get; }
    public SetupFieldSensitivity DeclaredSensitivity => Data.GetProperty("sensitivity").GetString() switch
    {
        "public" => SetupFieldSensitivity.Public,
        "secret" => SetupFieldSensitivity.Secret,
        _ => SetupFieldSensitivity.Sensitive
    };
    public SetupFieldSensitivity Sensitivity => EffectiveSensitivity(Data);
    public SetupFieldDisclosure Disclosure => Text("disclosure") switch
    {
        "essential" => SetupFieldDisclosure.Essential,
        "advanced" => SetupFieldDisclosure.Advanced,
        _ => SetupFieldDisclosure.Guided
    };
    public JsonElement? Default => Data.TryGetProperty("default", out var value) ? value.Clone() : null;
    public JsonElement? Recommended => Data.TryGetProperty("recommended", out var value) ? value.Clone() : null;
    public IReadOnlyList<JsonElement> Choices => Array.AsReadOnly(
        Data.TryGetProperty("choices", out var value) ? value.EnumerateArray().Select(item => item.Clone()).ToArray() : []);
    public JsonElement Constraints => Data.Clone();
    public override string ToString() => $"{nameof(SetupFieldDefinition)}:{Id}:{Type}:{Sensitivity}";
    internal string? Text(string name) => Data.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static SetupFieldSensitivity EffectiveSensitivity(JsonElement field)
    {
        SetupFieldSensitivity sensitivity = field.GetProperty("sensitivity").GetString() switch
        {
            "public" => SetupFieldSensitivity.Public,
            "secret" => SetupFieldSensitivity.Secret,
            _ => SetupFieldSensitivity.Sensitive
        };
        if (field.TryGetProperty("properties", out var properties))
            foreach (JsonElement property in properties.EnumerateArray())
                sensitivity = (SetupFieldSensitivity)Math.Max((int)sensitivity, (int)EffectiveSensitivity(property));
        return sensitivity;
    }
}

public sealed class SetupArtifactDefinition
{
    internal SetupArtifactDefinition(JsonElement data) => Data = data.Clone();
    internal JsonElement Data { get; }
    public string Id => Data.GetProperty("id").GetString()!;
    public string Format => Data.GetProperty("format").GetString()!;
    public string FileName => Data.GetProperty("fileName").GetString()!;
    public string ApplySemantics => Data.GetProperty("applySemantics").GetString()!;
    public string Restart => Data.GetProperty("restart").GetString()!;
    public override string ToString() => $"{nameof(SetupArtifactDefinition)}:{Id}:{Format}";
}

public sealed class SetupAnswerSnapshot
{
    private readonly ReadOnlyDictionary<string, JsonElement> _values;

    public SetupAnswerSnapshot(long inputRevision, IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(inputRevision);
        InputRevision = inputRevision;
        if (values.Count > 1024) throw new SetupDefinitionException("answer-limit");
        var snapshot = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var budget = new Composition.CompositionBudget(Composition.SetupCompositionLimits.Default);
        int bytes = 0;
        foreach ((string key, JsonElement value) in values)
        {
            if (key.Length > 128 || value.ValueKind == JsonValueKind.Undefined)
                throw new SetupDefinitionException("answer-invalid");
            byte[] canonical = DefinitionJson.Canonicalize(value, budget);
            bytes = checked(bytes + canonical.Length);
            if (bytes > Composition.SetupCompositionLimits.DefaultAggregateSourceBytes)
                throw new SetupDefinitionException("answer-limit");
            using JsonDocument parsed = JsonDocument.Parse(canonical);
            snapshot.Add(key, parsed.RootElement.Clone());
        }
        _values = new(snapshot);
    }

    public long InputRevision { get; }
    public IReadOnlyDictionary<string, JsonElement> Values => _values;
    public override string ToString() => $"{nameof(SetupAnswerSnapshot)}:Revision={InputRevision}:Count={_values.Count}";
}

public sealed record DefinitionDiagnostic(string Code, string FieldId)
{
    public override string ToString() => $"{Code}:{FieldId}";
}
