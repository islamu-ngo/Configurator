namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;

public sealed class SetupDefinitionException : Exception
{
    internal SetupDefinitionException(string code) : base(code) => Code = code;
    public string Code { get; }
    public string? SchemaPath { get; private init; }
    public DefinitionSourceSpan? SourceSpan { get; private init; }

    internal SetupDefinitionException AtSchemaPath(string knownSchemaPath) => SchemaPath is not null ? this
        : new SetupDefinitionException(Code) { SchemaPath = knownSchemaPath };

    internal SetupDefinitionException WithLocation(DefinitionSourceMap map, string knownSchemaPath)
    {
        string path = SchemaPath ?? knownSchemaPath;
        while (true)
        {
            if (map.TryGetSpan(path, out DefinitionSourceSpan span))
                return new SetupDefinitionException(Code) { SchemaPath = path, SourceSpan = span };
            int separator = path.LastIndexOf('/');
            if (separator < 0) return new SetupDefinitionException(Code);
            path = path[..separator];
        }
    }
}

public enum SetupFieldSensitivity { Public, Sensitive, Secret }
public enum SetupFieldDisclosure { Essential, Guided, Advanced }

public sealed class SetupDefinition
{
    private readonly byte[] _canonicalBytes;

    internal SetupDefinition(JsonElement document, byte[] canonicalBytes, DefinitionNode configuration,
        DefinitionSourceMap? sourceMap = null)
    {
        Document = document.Clone();
        _canonicalBytes = (byte[])canonicalBytes.Clone();
        Digest = ArtifactDigest.Compute(canonicalBytes);
        FamilyId = document.GetProperty("familyId").GetGuid();
        Revision = document.GetProperty("revision").GetInt64();
        Configuration = configuration;
        SourceMap = sourceMap ?? DefinitionSourceMap.Empty;
        SetupFieldSensitivity inherited = SetupFieldDefinition.SensitivityOf(configuration);
        Fields = Array.AsReadOnly(configuration.Properties.Select(item => new SetupFieldDefinition(
            item.Key, item.Value, configuration.Required.Contains(item.Key, StringComparer.Ordinal), inherited)).ToArray());
        Artifacts = Array.AsReadOnly(document.GetProperty("artifacts").EnumerateArray()
            .Select(item => new SetupArtifactDefinition(item)).ToArray());
        ApplicationVersions = Strings("applicationVersions");
        EntryPoints = Strings("entryPoints");
        Locales = Strings("locales");
        RequiredCapabilities = Strings("requiredCapabilities");
        Presentation = document.TryGetProperty("presentation", out var presentation)
            ? new DefinitionPresentation(presentation) : null;
    }

    internal JsonElement Document { get; }
    public Guid FamilyId { get; }
    public long Revision { get; }
    public ArtifactDigest Digest { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => new((byte[])_canonicalBytes.Clone());
    public string ApplicationId => Document.GetProperty("project").GetProperty("id").GetString()!;
    public string Publisher => Document.GetProperty("project").GetProperty("publisher").GetString()!;
    public string Title => Document.GetProperty("project").GetProperty("name").GetString()!;
    public DefinitionNode Configuration { get; }
    public DefinitionSourceMap SourceMap { get; }
    public IReadOnlyList<string> ApplicationVersions { get; }
    public IReadOnlyList<string> EntryPoints { get; }
    public IReadOnlyList<string> Locales { get; }
    public IReadOnlyList<string> RequiredCapabilities { get; }
    public IReadOnlyList<SetupFieldDefinition> Fields { get; }
    public IReadOnlyList<SetupArtifactDefinition> Artifacts { get; }
    public DefinitionPresentation? Presentation { get; }
    public override string ToString() => $"{nameof(SetupDefinition)}:{FamilyId:D}:{Revision}:{Digest}";

    private ReadOnlyCollection<string> Strings(string name) => Array.AsReadOnly(
        Document.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray());
}

public sealed class SetupFieldDefinition
{
    internal SetupFieldDefinition(string name, DefinitionNode node, bool required,
        SetupFieldSensitivity inherited = SetupFieldSensitivity.Public)
    {
        Node = node;
        Name = name;
        Id = DefinitionProfile.FieldId(name);
        Type = node.JsonType;
        string inferredTitle = string.IsNullOrWhiteSpace(name) ? "Setting" : name;
        if (inferredTitle.Any(char.IsControl)) inferredTitle = JsonSerializer.Serialize(inferredTitle);
        Title = Text("title") ?? (inferredTitle.Length <= 256 ? inferredTitle : inferredTitle[..256]);
        Required = required;
        Default = Data.TryGetProperty("default", out var descriptive) ? descriptive.Clone() : null;
        InitialValue = ExtensionValue("initialValue");
        SuggestedValue = ExtensionValue("suggestedValue");
        Choices = Array.AsReadOnly(Data.TryGetProperty("enum", out var choices)
            ? choices.EnumerateArray().Select(item => item.Clone()).ToArray() : []);
        SetupFieldSensitivity own = (SetupFieldSensitivity)Math.Max((int)inherited, (int)SensitivityOf(node));
        Sensitivity = EffectiveSensitivity(node, inherited);
        Disclosure = ExtensionValue("detail")?.GetString() switch
        {
            "basic" => SetupFieldDisclosure.Essential,
            "advanced" => SetupFieldDisclosure.Advanced,
            _ => SetupFieldDisclosure.Guided
        };
        ActiveWhen = ExtensionValue("applicableWhen");
        Dependencies = Array.AsReadOnly(ActiveWhen is { } condition
            ? DefinitionValidator.References(condition).ToArray() : []);
        Properties = Array.AsReadOnly(node.Properties.Select(item => new SetupFieldDefinition(item.Key, item.Value,
            node.Required.Contains(item.Key, StringComparer.Ordinal), own)).ToArray());
        Items = node.Items is { } itemSchema ? new SetupFieldDefinition("item", itemSchema, false, own) : null;
        UniqueBy = ExtensionValue("uniqueBy") is { } path
            ? DefinitionPath.Parse(path.GetString()!).Segments : Array.Empty<string>();
        Guidance = ExtensionValue("guidance") is { } guidance ? new DefinitionFieldGuidance(guidance) : null;
        InputPurpose = ExtensionValue("credential") is { } credential ? new DefinitionInputPurpose(
            credential.GetProperty("kind").GetString()!, credential.GetProperty("acquisition").GetString()!) : null;
    }
    public DefinitionNode Node { get; }
    internal JsonElement Data => Node.Keywords;
    internal IReadOnlyList<string> Dependencies { get; }
    internal JsonElement? ActiveWhen { get; }
    public string Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string Title { get; }
    public string? Help => Text("description");
    public string? Format => Text("format");
    public DefinitionFieldGuidance? Guidance { get; }
    public DefinitionInputPurpose? InputPurpose { get; }
    public bool Required { get; }
    public string? ItemType => Node.Items?.JsonType;
    public SetupFieldDefinition? Items { get; }
    public int? MinimumItems => Data.TryGetProperty("minItems", out var value) ? value.GetInt32() : null;
    public int? MaximumItems => Data.TryGetProperty("maxItems", out var value) ? value.GetInt32() : null;
    public IReadOnlyList<SetupFieldDefinition> Properties { get; }
    public IReadOnlyList<string> UniqueBy { get; }
    public SetupFieldSensitivity DeclaredSensitivity => SensitivityOf(Node);
    public SetupFieldSensitivity Sensitivity { get; }
    public SetupFieldDisclosure Disclosure { get; }
    public JsonElement? Default { get; }
    public JsonElement? InitialValue { get; }
    public JsonElement? SuggestedValue { get; }
    public IReadOnlyList<JsonElement> Choices { get; }
    public JsonElement Constraints => Data.Clone();
    public override string ToString() => $"{nameof(SetupFieldDefinition)}:{Id}:{Type}:{Sensitivity}";
    internal string? Text(string name) => Data.TryGetProperty(name, out var value) ? value.GetString() : null;

    private JsonElement? ExtensionValue(string name) =>
        Data.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty(name, out var value)
            ? value.Clone() : null;

    internal static SetupFieldSensitivity SensitivityOf(DefinitionNode node)
    {
        JsonElement keywords = node.Keywords;
        if (keywords.TryGetProperty("writeOnly", out var writeOnly) && writeOnly.GetBoolean())
            return SetupFieldSensitivity.Secret;
        return keywords.TryGetProperty("x-configurator", out var metadata)
            && metadata.TryGetProperty("sensitivity", out var sensitivity) ? sensitivity.GetString() switch
        {
            "public" => SetupFieldSensitivity.Public,
            "secret" => SetupFieldSensitivity.Secret,
            _ => SetupFieldSensitivity.Sensitive
        } : SetupFieldSensitivity.Sensitive;
    }

    internal static SetupFieldSensitivity EffectiveSensitivity(DefinitionNode node,
        SetupFieldSensitivity inherited = SetupFieldSensitivity.Public)
    {
        var sensitivity = (SetupFieldSensitivity)Math.Max((int)inherited, (int)SensitivityOf(node));
        foreach (DefinitionNode child in node.Properties.Values.Concat(node.Variants)
            .Concat(node.Items is null ? [] : new[] { node.Items })
            .Concat(node.AdditionalProperties is null ? [] : new[] { node.AdditionalProperties }))
            sensitivity = (SetupFieldSensitivity)Math.Max((int)sensitivity, (int)EffectiveSensitivity(child));
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
    public string ApplySemantics => Data.GetProperty("targetUpdateMode").GetString()!;
    public string Restart => Data.GetProperty("restartRequirement").GetString()!;
    public JsonElement Projection => Data.GetProperty("projection").Clone();
    public override string ToString() => $"{nameof(SetupArtifactDefinition)}:{Id}:{Format}";
}

public sealed class SetupAnswerSnapshot
{
    private readonly ReadOnlyDictionary<string, JsonElement> _values;
    private readonly FrozenSet<DefinitionPath> _omitted;

    public SetupAnswerSnapshot(long inputRevision, IReadOnlyDictionary<string, JsonElement> values,
        IEnumerable<DefinitionPath>? omitted = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(inputRevision);
        InputRevision = inputRevision;
        if (values.Count > Composition.SetupCompositionLimits.DefaultMappingEntries)
            throw new SetupDefinitionException("answer-limit");
        var snapshot = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var budget = new Composition.CompositionBudget(Composition.SetupCompositionLimits.Default);
        int bytes = 2;
        try
        {
            budget.Node(1);
            foreach ((string key, JsonElement value) in values)
            {
                DefinitionProfile.ValidateScalar(key);
                budget.Scalar(key);
                if (value.ValueKind == JsonValueKind.Undefined) throw new SetupDefinitionException("answer-invalid");
                byte[] canonical = DefinitionJson.Canonicalize(value, budget,
                    Composition.CompositionKeyPolicy.Exact, initialDepth: 2);
                try
                {
                    bytes += canonical.Length + JsonSerializer.SerializeToUtf8Bytes(key).Length + 2;
                    if (bytes > Composition.SetupCompositionLimits.DefaultAggregateSourceBytes)
                        throw new SetupDefinitionException("answer-limit");
                    using JsonDocument parsed = JsonDocument.Parse(canonical);
                    snapshot.Add(key, parsed.RootElement.Clone());
                }
                finally { CryptographicOperations.ZeroMemory(canonical); }
            }
            var suppressed = new HashSet<DefinitionPath>();
            foreach (DefinitionPath path in omitted ?? [])
            {
                if (suppressed.Count == Composition.SetupCompositionLimits.DefaultSequenceEntries)
                    throw new SetupDefinitionException("answer-limit");
                ArgumentNullException.ThrowIfNull(path);
                budget.Scalar(path.ToPointer());
                bytes += JsonSerializer.SerializeToUtf8Bytes(path.ToPointer()).Length + 1;
                if (bytes > Composition.SetupCompositionLimits.DefaultAggregateSourceBytes)
                    throw new SetupDefinitionException("answer-limit");
                if (!suppressed.Add(path) || HasSuppliedValue(path))
                    throw new SetupDefinitionException("answer-omission");
            }
            foreach (DefinitionPath path in suppressed)
            {
                string reference = path.ToPointer();
                for (int position = 1; position < reference.Length; position++)
                    if (reference[position] == '/' && suppressed.Contains(DefinitionPath.Parse(reference[..position])))
                        throw new SetupDefinitionException("answer-omission");
            }
            _omitted = suppressed.ToFrozenSet();
        }
        catch (Composition.SetupCompositionException)
        {
            throw new SetupDefinitionException("answer-limit");
        }
        _values = new(snapshot);

        bool HasSuppliedValue(DefinitionPath path)
        {
            if (path.Segments.Count == 0) return true;
            if (!snapshot.TryGetValue(path.Segments[0], out JsonElement value)) return false;
            for (int index = 1; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    if (!value.TryGetProperty(path.Segments[index], out value)) return false;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position)
                    && position < value.GetArrayLength()) value = value[position];
                else return false;
            }
            return true;
        }
    }

    public long InputRevision { get; }
    public IReadOnlyDictionary<string, JsonElement> Values => _values;
    public IReadOnlySet<DefinitionPath> Omitted => _omitted;
    public override string ToString() => $"{nameof(SetupAnswerSnapshot)}:Revision={InputRevision}:Count={_values.Count}";
}

public sealed record DefinitionDiagnostic(string Code, string FieldId)
{
    public DefinitionDiagnostic(string code, string fieldId, DefinitionCorrectionLocation location)
        : this(code, fieldId, location.Segments.SelectMany(segment =>
            segment.Kind == DefinitionLocationSegmentKind.MapEntry
                ? new[] { "@entry", segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : new[] { segment.PropertyName
                    ?? segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToArray())
    {
        Location = location;
    }

    public DefinitionDiagnostic(string code, string fieldId, IReadOnlyList<string> relativePath)
        : this(code, fieldId)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }

    public IReadOnlyList<string> RelativePath { get; } = Array.Empty<string>();
    public DefinitionCorrectionLocation? Location { get; }
    public bool Equals(DefinitionDiagnostic? other) => other is not null &&
        Code == other.Code && FieldId == other.FieldId && RelativePath.SequenceEqual(other.RelativePath)
        && Equals(Location, other.Location);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code, StringComparer.Ordinal);
        hash.Add(FieldId, StringComparer.Ordinal);
        foreach (string segment in RelativePath) hash.Add(segment, StringComparer.Ordinal);
        hash.Add(Location);
        return hash.ToHashCode();
    }
    public override string ToString() => $"{Code}:{FieldId}";
}
