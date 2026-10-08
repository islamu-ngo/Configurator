namespace Configurator.Core.Artifacts;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Environment;

public sealed class DefinitionArtifact
{
    private readonly byte[] _bytes;
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
    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
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
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(answers);
        SetupArtifactDefinition? artifact = definition.Artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null) return new(null, [new("artifact-unknown", "")]);
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers);
        if (!validation.IsReady) return new(null, validation.Diagnostics);
        try
        {
            var budget = new CompositionBudget(SetupCompositionLimits.Default);
            int operations = 0;
            (bool include, JsonNode? output) = Project(artifact.Data.GetProperty("projection"), null, null, 1);
            if (!include) return new(null, [new("artifact-omitted", "")]);
            using JsonDocument document = JsonDocument.Parse(output?.ToJsonString() ?? "null");
            byte[] canonical = DefinitionJson.Canonicalize(document.RootElement);
            byte[] bytes = artifact.Format switch
            {
                "json" => canonical,
                "yaml" => WriteYaml(document.RootElement),
                "dotenv" => WriteDotenv(document.RootElement),
                "markdown" => WriteMarkdown(document.RootElement),
                _ => throw new SetupDefinitionException("format-invalid")
            };
            if (bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
                throw new SetupDefinitionException("artifact-limit");
            return new(new(definition, artifact, answers, bytes), []);

            (bool Include, JsonNode? Node) Project(JsonElement projection, JsonElement? item, int? index, int depth)
            {
                if (++operations > SetupCompositionLimits.DefaultParserEvents)
                    throw new SetupDefinitionException("projection-limit");
                if (projection.TryGetProperty("when", out var condition)
                    && !DefinitionValidator.ConditionMatches(condition, validation.Values)) return (false, null);
                switch (projection.GetProperty("op").GetString())
                {
                    case "omit": return (false, null);
                    case "literal":
                        return Copy(projection.GetProperty("value"), depth);
                    case "copy":
                        string field = projection.GetProperty("field").GetString()!;
                        if (field == "$item") return item.HasValue ? Copy(item.Value, depth) : (false, null);
                        return validation.Values.TryGetValue(field, out var value) ? Copy(value, depth) : (false, null);
                    case "index":
                        budget.Node(depth);
                        return (true, JsonValue.Create(index!.Value));
                    case "object":
                        budget.Node(depth);
                        var mapping = new JsonObject();
                        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                        {
                            var child = Project(member.Value, item, index, depth + 1);
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
                            var child = Project(entry, item, index, depth + 1);
                            if (child.Include) array.Add(child.Node);
                        }
                        return (true, array);
                    case "repeat":
                        string listField = projection.GetProperty("field").GetString()!;
                        if (!validation.Values.TryGetValue(listField, out var source)) return (false, null);
                        if (source.ValueKind != JsonValueKind.Array) throw new SetupDefinitionException("repeat-source");
                        budget.Node(depth);
                        var repeated = new JsonArray();
                        int position = 0;
                        foreach (JsonElement entry in source.EnumerateArray())
                        {
                            var child = Project(projection.GetProperty("item"), entry, position++, depth + 1);
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
        catch (SetupDefinitionException exception) { return new(null, [new(exception.Code, "")]); }
        catch (SetupCompositionException) { return new(null, [new("artifact-limit", "")]); }
    }

    private static byte[] WriteDotenv(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new SetupDefinitionException("dotenv-shape");
        var entries = new List<DotenvEntry>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                throw new SetupDefinitionException("dotenv-shape");
            string text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()! : property.Value.GetRawText();
            entries.Add(new(property.Name, text, DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput));
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
