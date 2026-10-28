namespace Configurator.Core.Artifacts;

using System.Diagnostics;
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
    internal static readonly DiagnosticListener PreparationDiagnostics = new("Configurator.Core.Preparation");

    public static DefinitionArtifactResult Compose(
        SetupDefinition definition, string artifactId, SetupAnswerSnapshot answers,
        CancellationToken cancellationToken = default)
        => ComposeCore(definition, artifactId, answers, false, [], cancellationToken);

    internal static DefinitionArtifactResult ComposeCore(SetupDefinition definition, string artifactId,
        SetupAnswerSnapshot answers, bool draft, List<DefinitionDraftCompletion> completions,
        CancellationToken cancellationToken = default)
        => new ValidatedComposition(definition, answers, draft, cancellationToken).Compose(artifactId, completions);

    /// <summary>Owns validation for exactly one definition, answer snapshot and mode; callers cannot supply validation.</summary>
    internal sealed class ValidatedComposition
    {
        private readonly SetupDefinition _definition;
        private readonly SetupAnswerSnapshot _answers;
        private readonly bool _draft;
        private readonly CancellationToken _cancellationToken;
        private readonly DefinitionValidationResult _validation;

        internal ValidatedComposition(SetupDefinition definition, SetupAnswerSnapshot answers, bool draft,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(answers);
            _definition = definition;
            _answers = answers;
            _draft = draft;
            _cancellationToken = cancellationToken;
            _validation = draft ? DefinitionValidator.ValidateDraft(definition, answers, cancellationToken)
                : DefinitionValidator.Validate(definition, answers, cancellationToken);
        }

        internal DefinitionArtifactResult Compose(string artifactId, List<DefinitionDraftCompletion> completions)
            => ComposeValidated(_definition, artifactId, _answers, _draft, completions, _validation, _cancellationToken);
    }

    private static DefinitionArtifactResult ComposeValidated(SetupDefinition definition, string artifactId,
        SetupAnswerSnapshot answers, bool draft, List<DefinitionDraftCompletion> completions,
        DefinitionValidationResult validation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        SetupArtifactDefinition? artifact = definition.Artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null) return new(null, [new("artifact-unknown", "")]);
        if (!validation.IsReady) return new(null, validation.Diagnostics);
        string unresolvedField = "";
        try
        {
            var budget = new CompositionBudget(SetupCompositionLimits.Default);
            int operations = 0;
            var unknown = validation.DeferredFields.Concat(validation.UnresolvedFields).ToHashSet(StringComparer.Ordinal);
            var activeRoots = validation.Fields.Where(field => field.IsActive).Select(field => field.Definition.Name)
                .Concat(validation.Values.Keys).ToHashSet(StringComparer.Ordinal);
            (bool include, JsonNode? output) = Project(artifact.Data.GetProperty("projection"), null, null, null, "", "", 1);
            if (!include) return new(null, [new("artifact-omitted", "")]);
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(output?.ToJsonString() ?? "null");
            byte[] canonical = DefinitionJson.Canonicalize(document.RootElement, keyPolicy: CompositionKeyPolicy.Exact);
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
                cancellationToken.ThrowIfCancellationRequested();
                if (PreparationDiagnostics.IsEnabled("ArtifactComposed"))
                    PreparationDiagnostics.Write("ArtifactComposed", artifact.Id);
                return new(new(definition, artifact, answers, bytes), []);
            }
            finally
            {
                if (bytes is not null && !ReferenceEquals(bytes, canonical))
                    CryptographicOperations.ZeroMemory(bytes);
                CryptographicOperations.ZeroMemory(canonical);
            }

            (bool Include, JsonNode? Node) Project(JsonElement projection, JsonElement? item, int? index,
                DefinitionNode? itemSchema, string itemPath, string outputPath, int depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++operations > SetupCompositionLimits.DefaultParserEvents)
                    throw new SetupDefinitionException("projection-limit");
                if (projection.TryGetProperty("when", out var condition))
                {
                    bool? matches = DefinitionValidator.EvaluateCondition(condition, validation.Values, draft ? unknown : null, item,
                        deferredLocations: draft ? validation.DeferredLocations : null,
                        itemLocation: item is null ? null : DefinitionPath.Parse(itemPath));
                    if (matches is null)
                    {
                        string? unresolved = DefinitionValidator.References(condition).FirstOrDefault(unknown.Contains);
                        if (unresolved is null && item is not null)
                        {
                            DefinitionPath location = DefinitionPath.Parse(itemPath);
                            unresolved = location.Segments.Count > 0 ? location.Segments[0] : null;
                        }
                        unresolvedField = unresolved is null ? "" : validation.Fields.FirstOrDefault(field => field.Definition.Name == unresolved)?.Id ?? "";
                        throw new SetupDefinitionException("draft-dependency-unresolved");
                    }
                    if (matches == false) return (false, null);
                }
                switch (projection.GetProperty("op").GetString())
                {
                    case "omit": return (false, null);
                    case "literal": return Copy(projection.GetProperty("value"), depth);
                    case "copy":
                        {
                            JsonElement reference = projection.GetProperty("reference");
                            var selected = Select(reference, itemSchema, itemPath);
                            CheckUnknown(selected.Path);
                            bool present = DefinitionValidator.TryResolveReference(reference, validation.Values, item, out JsonElement value);
                            return draft ? CopyField(selected.Schema, present ? value : null, selected.Path, outputPath,
                                depth, selected.SecretAncestor) : present ? Copy(value, depth) : (false, null);
                        }
                    case "index":
                        budget.Node(depth);
                        return (true, JsonValue.Create(index!.Value));
                    case "object":
                        {
                            budget.Node(depth);
                            var mapping = new JsonObject();
                            foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                            {
                                var child = Project(member.Value, item, index, itemSchema, itemPath,
                                    outputPath + "/" + Pointer(member.Name), depth + 1);
                                if (!child.Include) continue;
                                budget.Scalar(member.Name);
                                mapping.Add(member.Name, child.Node);
                            }
                            return (true, mapping);
                        }
                    case "list":
                        {
                            budget.Node(depth);
                            var array = new JsonArray();
                            foreach (JsonElement entry in projection.GetProperty("items").EnumerateArray())
                            {
                                var child = Project(entry, item, index, itemSchema, itemPath,
                                    outputPath + "/" + array.Count.ToString(CultureInfo.InvariantCulture), depth + 1);
                                if (child.Include) array.Add(child.Node);
                            }
                            return (true, array);
                        }
                    case "repeat":
                        {
                            JsonElement reference = projection.GetProperty("reference");
                            var selected = Select(reference, itemSchema, itemPath);
                            CheckUnknown(selected.Path);
                            if (!DefinitionValidator.TryResolveReference(reference, validation.Values, item, out JsonElement source)) return (false, null);
                            if (source.ValueKind != JsonValueKind.Array) throw new SetupDefinitionException("repeat-source");
                            budget.Node(depth);
                            var repeated = new JsonArray();
                            int position = 0;
                            foreach (JsonElement entry in source.EnumerateArray())
                            {
                                int itemIndex = position++;
                                var child = Project(projection.GetProperty("item"), entry, itemIndex, selected.Schema.Items,
                                    selected.Path + "/" + itemIndex.ToString(CultureInfo.InvariantCulture),
                                    outputPath + "/" + repeated.Count.ToString(CultureInfo.InvariantCulture), depth + 1);
                                if (child.Include) repeated.Add(child.Node);
                            }
                            return (true, repeated);
                        }
                    default: throw new SetupDefinitionException("projection-operation");
                }
            }

            (DefinitionNode Schema, string Path, bool SecretAncestor) Select(JsonElement reference,
                DefinitionNode? itemSchema, string itemPath)
            {
                bool relative = reference.GetProperty("scope").GetString() == "item";
                DefinitionNode schema = relative ? itemSchema! : definition.Configuration;
                bool secret = false;
                if (relative)
                {
                    DefinitionNode ancestor = definition.Configuration;
                    foreach (string segment in DefinitionPath.Parse(itemPath).Segments)
                    {
                        secret |= SetupFieldDefinition.SensitivityOf(ancestor) == SetupFieldSensitivity.Secret;
                        ancestor = ancestor.Type == DefinitionNodeType.Mapping ? ancestor.Properties[segment] : ancestor.Items!;
                    }
                }
                DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
                foreach (string segment in path.Segments)
                {
                    secret |= SetupFieldDefinition.SensitivityOf(schema) == SetupFieldSensitivity.Secret;
                    schema = schema.Type == DefinitionNodeType.Mapping ? schema.Properties[segment] : schema.Items!;
                }
                return (schema, (relative ? itemPath : "") + path.ToPointer(), secret);
            }

            void CheckUnknown(string sourcePath)
            {
                if (!draft) return;
                DefinitionPath path = DefinitionPath.Parse(sourcePath);
                string? unresolved = path.Segments.Count == 0 ? validation.UnresolvedFields.FirstOrDefault()
                    : validation.UnresolvedFields.Contains(path.Segments[0]) ? path.Segments[0] : null;
                if (unresolved is null) return;
                unresolvedField = validation.Fields.FirstOrDefault(field => field.Definition.Name == unresolved)?.Id ?? "";
                throw new SetupDefinitionException("draft-dependency-unresolved");
            }

            (bool, JsonNode?) Copy(JsonElement value, int depth)
            {
                Charge(value, depth);
                return (true, JsonNode.Parse(value.GetRawText()));
            }

            (bool, JsonNode?) CopyField(DefinitionNode schema, JsonElement? value, string fieldPath,
                string outputPath, int depth, bool secretAncestor, string? completionFieldPath = null,
                string? completionOutputPath = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DefinitionPath location = DefinitionPath.Parse(fieldPath);
                if (location.Segments.Count > 0 && !activeRoots.Contains(location.Segments[0]))
                    return (false, null);
                bool secret = secretAncestor || SetupFieldDefinition.SensitivityOf(schema) == SetupFieldSensitivity.Secret;
                completionFieldPath ??= fieldPath;
                completionOutputPath ??= outputPath;
                if (value is { ValueKind: not JsonValueKind.Null } variantValue && schema.Variants.Count > 0)
                {
                    DefinitionNode branch = DefinitionValidator.SelectVariant(schema, variantValue)
                        ?? throw new SetupDefinitionException("value-variant");
                    return CopyField(branch, value, fieldPath, outputPath, depth, secret,
                        completionFieldPath, completionOutputPath);
                }
                if (secret && schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence) && !value.HasValue)
                {
                    budget.Node(depth);
                    completions.Add(new(artifact.Id, completionFieldPath, completionOutputPath));
                    return (true, schema.Type == DefinitionNodeType.Text || artifact.Format is "dotenv" or "markdown"
                        ? JsonValue.Create("") : null);
                }
                if (!value.HasValue) return (false, null);
                if (value.Value.ValueKind == JsonValueKind.Null) return Copy(value.Value, depth);
                if (schema.Type == DefinitionNodeType.Mapping)
                {
                    budget.Node(depth);
                    var mapping = new JsonObject();
                    foreach ((string name, DefinitionNode property) in schema.Properties)
                    {
                        bool exists = value.Value.TryGetProperty(name, out JsonElement childValue);
                        var child = CopyField(property, exists ? childValue : null, fieldPath + "/" + Pointer(name),
                            outputPath + "/" + Pointer(name), depth + 1, secret,
                            completionFieldPath + "/" + Pointer(name), completionOutputPath + "/" + Pointer(name));
                        if (!child.Item1) continue;
                        budget.Scalar(name);
                        mapping.Add(name, child.Item2);
                    }
                    if (schema.AdditionalProperties is { } map)
                    {
                        int entry = 0;
                        foreach (JsonProperty property in value.Value.EnumerateObject())
                        {
                            if (schema.Properties.ContainsKey(property.Name)) continue;
                            string ordinal = "/@entry/" + entry.ToString(CultureInfo.InvariantCulture);
                            var child = CopyField(map, property.Value, fieldPath + "/" + Pointer(property.Name),
                                outputPath + "/" + Pointer(property.Name), depth + 1, secret,
                                completionFieldPath + ordinal, completionOutputPath + ordinal);
                            budget.Scalar(property.Name);
                            if (child.Item1) mapping.Add(property.Name, child.Item2);
                            entry++;
                        }
                    }
                    return (true, mapping);
                }
                if (schema.Type == DefinitionNodeType.Sequence)
                {
                    budget.Node(depth);
                    var array = new JsonArray();
                    foreach (JsonElement row in value.Value.EnumerateArray())
                    {
                        string ordinal = "/" + array.Count.ToString(CultureInfo.InvariantCulture);
                        array.Add(CopyField(schema.Items!, row, fieldPath + ordinal, outputPath + ordinal, depth + 1, secret,
                            completionFieldPath + ordinal, completionOutputPath + ordinal).Item2);
                    }
                    return (true, array);
                }
                return Copy(value.Value, depth);
            }
            void Charge(JsonElement value, int depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
