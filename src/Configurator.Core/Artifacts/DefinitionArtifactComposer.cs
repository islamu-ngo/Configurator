namespace Configurator.Core.Artifacts;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Environment;

public sealed class DefinitionArtifact : IDisposable
{
    private readonly byte[] _bytes;
    private readonly object _sync = new();
    private bool _disposed;
    internal DefinitionArtifact(SetupDefinition definition, SetupArtifactDefinition artifact,
        SetupAnswerSnapshot answers, byte[] bytes)
    {
        _bytes = (byte[])bytes.Clone();
        Digest = ArtifactDigest.Compute(bytes);
        DefinitionDigest = definition.Digest;
        DefinitionRevision = definition.Revision;
        InputRevision = answers.InputRevision;
        ArtifactId = artifact.Id;
        FileName = artifact.FileName;
        ApplySemantics = artifact.ApplySemantics;
        Restart = artifact.Restart;
        MediaType = artifact.Format switch
        {
            "json" => "application/json",
            "yaml" => "application/yaml",
            "markdown" => "text/markdown",
            _ => "text/plain"
        };
    }
    /// <summary>Returns a caller-owned copy. Disposal clears the artifact's retained buffer, not copies already returned.</summary>
    public ReadOnlyMemory<byte> Bytes
    {
        get
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return new((byte[])_bytes.Clone());
            }
        }
    }
    public int ByteLength => _bytes.Length;
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
    public ArtifactDigest Digest { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public long DefinitionRevision { get; }
    public long InputRevision { get; }
    public string ArtifactId { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public string ApplySemantics { get; }
    public string Restart { get; }
    public SetupArtifactSensitivity Sensitivity => SetupArtifactSensitivity.Restricted;
    public override string ToString() => $"{nameof(DefinitionArtifact)}:Restricted:Revision={InputRevision}";
}

public sealed class DefinitionArtifactResult
{
    internal DefinitionArtifactResult(DefinitionArtifact? artifact, IEnumerable<DefinitionDiagnostic> diagnostics)
    {
        Artifact = artifact;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
    public DefinitionArtifact? Artifact { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public bool Succeeded => Artifact is not null && Diagnostics.Count == 0;
    public override string ToString() => $"{nameof(DefinitionArtifactResult)}:Succeeded={Succeeded}";
}

public static class DefinitionArtifactComposer
{
    public static DefinitionArtifactResult Compose(
        SetupDefinition definition, string artifactId, SetupAnswerSnapshot answers)
        => ComposeCore(definition, artifactId, answers, false, []);

    internal static DefinitionArtifactResult ComposeCore(SetupDefinition definition, string artifactId,
        SetupAnswerSnapshot answers, bool draft, List<DefinitionDraftCompletion> completions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(answers);
        SetupArtifactDefinition? artifact = definition.Artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null) return new(null, [new("artifact-unknown", "")]);
        DefinitionValidationResult validation = draft ? DefinitionValidator.ValidateDraft(definition, answers)
            : DefinitionValidator.Validate(definition, answers);
        if (!validation.IsReady) return new(null, validation.Diagnostics);
        string unresolvedField = "";
        try
        {
            var budget = new CompositionBudget(SetupCompositionLimits.Default);
            int operations = 0;
            var unknown = validation.DeferredFields.Concat(validation.UnresolvedFields).ToHashSet(StringComparer.Ordinal);
            (bool include, JsonNode? output) = Project(artifact.Data.GetProperty("projection"), null, null, null, "", "", 1);
            if (!include) return new(null, [new("artifact-omitted", "")]);
            using JsonDocument document = JsonDocument.Parse(output?.ToJsonString() ?? "null");
            byte[] canonical = DefinitionJson.Canonicalize(document.RootElement);
            byte[]? bytes = null;
            try
            {
                bytes = artifact.Format switch
                {
                    "json" => canonical,
                    "yaml" => WriteYaml(document.RootElement),
                    "dotenv" => WriteDotenv(document.RootElement, draft),
                    "markdown" => WriteMarkdown(document.RootElement),
                    _ => throw new SetupDefinitionException("format-invalid")
                };
                if (draft && completions.Count > 0 && artifact.Format == "dotenv")
                {
                    byte[] original = bytes;
                    bytes = Encoding.UTF8.GetBytes("# Draft: fill deferred secret keys before use.\n" + Encoding.UTF8.GetString(original));
                    CryptographicOperations.ZeroMemory(original);
                }
                if (bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
                    throw new SetupDefinitionException("artifact-limit");
                return new(new(definition, artifact, answers, bytes), []);
            }
            finally
            {
                if (bytes is not null && !ReferenceEquals(bytes, canonical))
                    CryptographicOperations.ZeroMemory(bytes);
                CryptographicOperations.ZeroMemory(canonical);
            }

            (bool Include, JsonNode? Node) Project(JsonElement projection, JsonElement? item, int? index,
                SetupFieldDefinition? itemField, string itemPath, string outputPath, int depth)
            {
                if (++operations > SetupCompositionLimits.DefaultParserEvents)
                    throw new SetupDefinitionException("projection-limit");
                if (projection.TryGetProperty("when", out var condition))
                {
                    bool? matches = DefinitionValidator.EvaluateCondition(condition, validation.Values, draft ? unknown : null);
                    if (matches is null)
                    {
                        unresolvedField = DefinitionValidator.References(condition).First(unknown.Contains);
                        throw new SetupDefinitionException("draft-dependency-unresolved");
                    }
                    if (matches == false) return (false, null);
                }
                switch (projection.GetProperty("op").GetString())
                {
                    case "omit": return (false, null);
                    case "literal":
                        return Copy(projection.GetProperty("value"), depth);
                    case "copy":
                        string field = projection.GetProperty("field").GetString()!;
                        if (field == "$item") return item.HasValue
                            ? draft ? CopyField(itemField!, item, itemPath, outputPath, depth, false, true) : Copy(item.Value, depth)
                            : (false, null);
                        if (draft && validation.UnresolvedFields.Contains(field))
                        {
                            unresolvedField = field;
                            throw new SetupDefinitionException("draft-dependency-unresolved");
                        }
                        bool present = validation.Values.TryGetValue(field, out var value);
                        return draft ? CopyField(definition.Fields.Single(entry => entry.Id == field), present ? value : null,
                            field, outputPath, depth, false, false) : present ? Copy(value, depth) : (false, null);
                    case "index":
                        budget.Node(depth);
                        return (true, JsonValue.Create(index!.Value));
                    case "object":
                        budget.Node(depth);
                        var mapping = new JsonObject();
                        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                        {
                            var child = Project(member.Value, item, index, itemField, itemPath, outputPath + "/" + Pointer(member.Name), depth + 1);
                            if (!child.Include) continue;
                            budget.Scalar(member.Name);
                            mapping.Add(member.Name, child.Node);
                        }
                        return (true, mapping);
                    case "list":
                        budget.Node(depth);
                        var array = new JsonArray();
                        foreach (JsonElement entry in projection.GetProperty("items").EnumerateArray())
                        {
                            var child = Project(entry, item, index, itemField, itemPath, outputPath + "/" + array.Count.ToString(CultureInfo.InvariantCulture), depth + 1);
                            if (child.Include) array.Add(child.Node);
                        }
                        return (true, array);
                    case "repeat":
                        string listField = projection.GetProperty("field").GetString()!;
                        if (draft && validation.UnresolvedFields.Contains(listField))
                        {
                            unresolvedField = listField;
                            throw new SetupDefinitionException("draft-dependency-unresolved");
                        }
                        if (!validation.Values.TryGetValue(listField, out var source)) return (false, null);
                        if (source.ValueKind != JsonValueKind.Array) throw new SetupDefinitionException("repeat-source");
                        budget.Node(depth);
                        var repeated = new JsonArray();
                        int position = 0;
                        foreach (JsonElement entry in source.EnumerateArray())
                        {
                            int itemIndex = position++;
                            var child = Project(projection.GetProperty("item"), entry, itemIndex,
                                definition.Fields.Single(field => field.Id == listField), listField + "/" + itemIndex.ToString(CultureInfo.InvariantCulture),
                                outputPath + "/" + repeated.Count.ToString(CultureInfo.InvariantCulture), depth + 1);
                            if (child.Include) repeated.Add(child.Node);
                        }
                        return (true, repeated);
                    default: throw new SetupDefinitionException("projection-operation");
                }
            }

            (bool, JsonNode?) Copy(JsonElement value, int depth)
            {
                Charge(value, depth);
                return (true, JsonNode.Parse(value.GetRawText()));
            }

            (bool, JsonNode?) CopyField(SetupFieldDefinition field, JsonElement? value, string fieldPath,
                string outputPath, int depth, bool secretAncestor, bool listItem)
            {
                bool secret = secretAncestor || field.DeclaredSensitivity == SetupFieldSensitivity.Secret;
                string type = listItem ? field.ItemType! : field.Type;
                if (secret && type is not ("object" or "list") && (!value.HasValue
                    || value.Value.ValueKind == JsonValueKind.Null
                    || value.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.Value.GetString())))
                {
                    budget.Node(depth);
                    completions.Add(new(artifact.Id, fieldPath, outputPath));
                    return (true, type is "string" or "enum" || artifact.Format is "dotenv" or "markdown"
                        ? JsonValue.Create("") : null);
                }
                if (!value.HasValue) return (false, null);
                if (type == "object")
                {
                    budget.Node(depth);
                    var mapping = new JsonObject();
                    foreach (SetupFieldDefinition property in field.Properties)
                    {
                        bool exists = value.Value.TryGetProperty(property.Id, out var childValue);
                        var child = CopyField(property, exists ? childValue : null, fieldPath + "/" + Pointer(property.Id),
                            outputPath + "/" + Pointer(property.Id), depth + 1, secret, false);
                        if (!child.Item1) continue;
                        budget.Scalar(property.Id);
                        mapping.Add(property.Id, child.Item2);
                    }
                    return (true, mapping);
                }
                if (type == "list")
                {
                    budget.Node(depth);
                    var array = new JsonArray();
                    foreach (JsonElement row in value.Value.EnumerateArray())
                    {
                        string indexPath = "/" + array.Count.ToString(CultureInfo.InvariantCulture);
                        array.Add(CopyField(field, row, fieldPath + indexPath, outputPath + indexPath, depth + 1, secret, true).Item2);
                    }
                    return (true, array);
                }
                return Copy(value.Value, depth);
            }
            void Charge(JsonElement value, int depth)
            {
                budget.Node(depth);
                switch (value.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (JsonProperty property in value.EnumerateObject())
                        {
                            budget.Scalar(property.Name);
                            Charge(property.Value, depth + 1);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (JsonElement child in value.EnumerateArray()) Charge(child, depth + 1);
                        break;
                    case JsonValueKind.String: budget.Scalar(value.GetString()); break;
                    default: budget.Scalar(value.GetRawText()); break;
                }
            }
        }
        catch (SetupDefinitionException exception) { return new(null, [new(exception.Code, unresolvedField)]); }
        catch (SetupCompositionException) { return new(null, [new("artifact-limit", "")]); }
    }

    private static string Pointer(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static byte[] WriteDotenv(JsonElement value, bool draft = false)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new SetupDefinitionException("dotenv-shape");
        var entries = new List<DotenvEntry>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                throw new SetupDefinitionException("dotenv-shape");
            string text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()! : property.Value.GetRawText();
            bool placeholder = draft && text.Length == 0;
            entries.Add(new(property.Name, placeholder ? null : text,
                placeholder ? DotenvEntryKind.EmptyPlaceholder : DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput));
        }
        DotenvRenderResult rendered = DotenvCodec.Render(new DotenvDocument(entries), true);
        if (!rendered.Succeeded) throw new SetupDefinitionException("dotenv-value");
        return rendered.Bytes.ToArray();
    }

    private static byte[] WriteYaml(JsonElement value)
    {
        var result = new StringBuilder();
        Write(value, 0);
        return Encoding.UTF8.GetBytes(result.ToString());
        void Write(JsonElement node, int indent)
        {
            if (node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Any())
            {
                foreach (JsonProperty property in node.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    result.Append(' ', indent).Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    Child(property.Value, indent);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array && node.GetArrayLength() > 0)
            {
                foreach (JsonElement child in node.EnumerateArray())
                {
                    result.Append(' ', indent).Append('-');
                    Child(child, indent);
                }
            }
            else result.Append(' ', indent).Append(node.GetRawText()).Append('\n');
        }
        void Child(JsonElement child, int indent)
        {
            if (child.ValueKind == JsonValueKind.Object && child.EnumerateObject().Any()
                || child.ValueKind == JsonValueKind.Array && child.GetArrayLength() > 0)
            {
                result.Append('\n');
                Write(child, indent + 2);
            }
            else result.Append(' ').Append(child.GetRawText()).Append('\n');
        }
    }

    private static byte[] WriteMarkdown(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array
            || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            throw new SetupDefinitionException("markdown-shape");
        var output = new StringBuilder();
        foreach (JsonElement item in value.EnumerateArray())
        {
            foreach (char character in item.GetString()!)
            {
                if ("\\`*_{}[]()#+-.!|><&".Contains(character, StringComparison.Ordinal)) output.Append('\\');
                if (character != '\r') output.Append(character);
            }
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }
}
