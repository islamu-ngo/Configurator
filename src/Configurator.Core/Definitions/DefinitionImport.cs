namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Environment;

public sealed record DefinitionImportResult(bool Succeeded, SetupAnswerSnapshot? Answers, string Code)
{
    public IReadOnlyList<string> ProvidedFields { get; init; } = [];
    public override string ToString() => $"{nameof(DefinitionImportResult)}:{Code}";
}

public static class DefinitionImport
{
    public static DefinitionImportResult Assess(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, SetupAnswerSnapshot current, IReadOnlySet<string>? replaceableDefaults = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(current);
        if (!TryMappings(definition, artifactId, out SetupArtifactDefinition? artifact,
            out Dictionary<string, string> mappings, out string code)) return Failed(code);
        try
        {
            if (bytes.IsEmpty || bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
                return Failed("input-invalid");
            Dictionary<string, JsonElement>? input = Parse(artifact!.Format, bytes);
            if (input is null || input.Count == 0) return Failed("input-invalid");

            var values = current.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            var imported = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, JsonElement value) in input)
            {
                if (!mappings.TryGetValue(key, out string? field))
                    return Failed("inverse-unsupported");
                if (!imported.Add(field))
                    return Failed("input-ambiguous");
                JsonElement answer = value;
                if (artifact.Format == "dotenv" && !TryDotenvValue(
                    definition.Fields.Single(item => item.Id == field), value.GetString()!, out answer))
                    return Failed("inverse-unsupported");
                if (values.TryGetValue(field, out JsonElement previous)
                    && !JsonElement.DeepEquals(previous, answer) && replaceableDefaults?.Contains(field) != true)
                    return Failed("answer-conflict");
                values[field] = answer;
            }

            var candidate = new SetupAnswerSnapshot(checked(current.InputRevision + 1), values);
            DefinitionValidationResult validation = DefinitionValidator.Validate(definition, candidate);
            if (validation.Fields.Any(field => imported.Contains(field.Id) && (!field.IsActive || field.HasError))
                || validation.Diagnostics.Any(item => item.Code != "value-required"))
                return Failed("import-field-invalid");
            return new(true, candidate, "import-ready") { ProvidedFields = Array.AsReadOnly(imported.ToArray()) };
        }
        catch (Exception exception) when (exception is SetupCompositionException or JsonException
            or SetupDefinitionException or OverflowException or InvalidOperationException)
        {
            return Failed("input-invalid");
        }
    }

    public static bool CanImport(SetupDefinition definition, string artifactId) =>
        TryMappings(definition, artifactId, out _, out _, out _);

    private static bool TryMappings(SetupDefinition definition, string artifactId,
        out SetupArtifactDefinition? artifact, out Dictionary<string, string> mappings, out string code)
    {
        artifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId);
        mappings = new(StringComparer.Ordinal);
        code = "import-unsupported";
        if (artifact is null || artifact.Format is not ("dotenv" or "json" or "yaml"))
            return false;

        code = "inverse-unsupported";
        JsonElement projection = artifact.Data.GetProperty("projection");
        if (projection.GetProperty("op").GetString() != "object"
            || projection.TryGetProperty("when", out _))
            return false;
        JsonElement members = projection.GetProperty("members");
        var mappedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty member in members.EnumerateObject())
        {
            if (member.Value.GetProperty("op").GetString() != "copy"
                || member.Value.TryGetProperty("when", out _))
                return false;
            string fieldId = member.Value.GetProperty("field").GetString()!;
            if (artifact.Format == "dotenv" && definition.Fields.Single(item => item.Id == fieldId).Type
                is not ("string" or "enum" or "boolean" or "integer" or "number"))
                return false;
            if (!mappedFields.Add(fieldId)) { code = "inverse-ambiguous"; return false; }
            mappings.Add(member.Name, fieldId);
        }
        return true;
    }

    private static bool TryDotenvValue(SetupFieldDefinition field, string text, out JsonElement value)
    {
        value = JsonSerializer.SerializeToElement(text);
        if (field.Type is "string" or "enum") return true;
        if (field.Type == "boolean" && text is "true" or "false")
        {
            value = JsonSerializer.SerializeToElement(text == "true");
            return true;
        }
        if (field.Type == "integer" && long.TryParse(text, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out long integer))
        {
            value = JsonSerializer.SerializeToElement(integer);
            return value.GetRawText() == text;
        }
        if (field.Type == "number" && decimal.TryParse(text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal number))
        {
            value = JsonSerializer.SerializeToElement(number);
            return value.GetRawText() == text;
        }
        return false;
    }

    private static Dictionary<string, JsonElement>? Parse(string format, ReadOnlyMemory<byte> bytes)
    {
        if (format == "dotenv")
        {
            DotenvParseResult parsed = DotenvCodec.Parse(bytes);
            if (!parsed.Succeeded) return null;
            var entries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (DotenvEntry entry in parsed.Document!.Entries)
                if (entry.Value is null || !entries.TryAdd(entry.Key, JsonSerializer.SerializeToElement(entry.Value)))
                    return null;
            return entries;
        }
        CompositionMap normalized = format == "yaml"
            ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None)
            : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None);
        byte[] normalizedBytes = SetupCompositionNormalizer.WriteJson(normalized, CancellationToken.None);
        try
        {
            using JsonDocument document = JsonDocument.Parse(normalizedBytes);
            return document.RootElement.EnumerateObject().ToDictionary(
                item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        }
        finally { CryptographicOperations.ZeroMemory(normalizedBytes); }
    }

    private static DefinitionImportResult Failed(string code) => new(false, null, code);
}
