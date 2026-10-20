namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;

public static class SetupDefinitionCodec
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = SetupCompositionLimits.DefaultNestingDepth,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static SetupDefinition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, yaml: false);
    public static SetupDefinition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, yaml: true);

    private static SetupDefinition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            CompositionMap normalized = yaml
                ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None)
                : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None);
            using JsonDocument parsed = JsonDocument.Parse(
                SetupCompositionNormalizer.WriteJson(normalized, CancellationToken.None));
            byte[] canonical = DefinitionJson.Canonicalize(parsed.RootElement);
            using JsonDocument canonicalDocument = JsonDocument.Parse(canonical);
            JsonElement document = canonicalDocument.RootElement;
            _ = JsonSerializer.Deserialize<DefinitionDocument>(canonical, Options)
                ?? throw new SetupDefinitionException("definition-invalid");
            Validate(document);
            return new SetupDefinition(document, canonical);
        }
        catch (SetupDefinitionException) { throw; }
        catch (SetupCompositionException exception)
        {
            throw new SetupDefinitionException(exception.Code == SetupCompositionFailureCode.LimitExceeded
                ? "document-limit" : "document-invalid");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            throw new SetupDefinitionException("definition-invalid");
        }
    }

    private static void Validate(JsonElement document)
    {
        if (document.GetProperty("schemaVersion").GetInt32() != 1
            || !Guid.TryParseExact(document.GetProperty("familyId").GetString(), "D", out Guid family)
            || family == Guid.Empty || document.GetProperty("revision").GetInt64() < 1)
            throw new SetupDefinitionException("identity-invalid");
        foreach (string name in new[] { "applicationId", "publisher", "title" })
            Text(document.GetProperty(name), 256);
        foreach (string name in new[] { "applicationVersions", "entryPoints", "locales" })
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            JsonElement array = document.GetProperty(name);
            if (array.GetArrayLength() is < 1 or > 128)
                throw new SetupDefinitionException("metadata-limit");
            foreach (JsonElement item in array.EnumerateArray())
                if (!unique.Add(Text(item, 128))) throw new SetupDefinitionException("metadata-duplicate");
        }
        JsonElement fields = document.GetProperty("fields");
        JsonElement artifacts = document.GetProperty("artifacts");
        if (fields.GetArrayLength() > 1024 || artifacts.GetArrayLength() is < 1 or > 64)
            throw new SetupDefinitionException("definition-limit");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement field in fields.EnumerateArray())
        {
            string id = Identifier(field.GetProperty("id"));
            if (!ids.Add(id) || !folded.Add(id)) throw new SetupDefinitionException("field-duplicate");
            ValidateField(field, nested: false);
        }
        if (document.TryGetProperty("presentation", out var presentation)) ValidatePresentation(presentation, ids);
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (JsonElement field in fields.EnumerateArray())
        {
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (string member in new[] { "activeWhen", "requiredWhen" })
                if (field.TryGetProperty(member, out var condition)) ValidateCondition(condition, ids, references);
            dependencies.Add(field.GetProperty("id").GetString()!, references);
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids) Visit(id, new HashSet<string>(StringComparer.Ordinal));
        void Visit(string id, HashSet<string> path)
        {
            if (visited.Contains(id)) return;
            if (!path.Add(id)) throw new SetupDefinitionException("activation-cycle");
            foreach (string reference in dependencies[id]) Visit(reference, path);
            path.Remove(id);
            visited.Add(id);
        }

        var artifactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listIds = fields.EnumerateArray().Where(field => field.GetProperty("type").GetString() == "list")
            .Select(field => field.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            if (!artifactIds.Add(Identifier(artifact.GetProperty("id"))))
                throw new SetupDefinitionException("artifact-duplicate");
            Choice(artifact, "format", DefinitionVocabulary.Formats);
            Choice(artifact, "applySemantics", DefinitionVocabulary.ApplySemantics);
            Choice(artifact, "restart", DefinitionVocabulary.Restarts);
            string fileName = Text(artifact.GetProperty("fileName"), 128);
            string stem = fileName.Split('.')[0];
            if (fileName is "." or ".." || fileName[^1] is '.' or ' '
                || !fileName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL"
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsAsciiDigit(stem[3])
                || !fileNames.Add(fileName))
                throw new SetupDefinitionException("filename-invalid");
            ValidateProjection(artifact.GetProperty("projection"), ids, listIds, inRepeat: false);
        }
    }

    private static void ValidateField(JsonElement field, bool nested, bool publicAncestors = true, bool secretAncestors = false)
    {
        Identifier(field.GetProperty("id"));
        Text(field.GetProperty("title"), 256);
        Choice(field, "type", DefinitionVocabulary.FieldTypes);
        Choice(field, "sensitivity", DefinitionVocabulary.Sensitivities);
        bool publicContext = publicAncestors && field.GetProperty("sensitivity").GetString() == "public";
        Choice(field, "disclosure", DefinitionVocabulary.Disclosures);
        foreach (string name in new[] { "help", "group", "format", "consequence" })
            if (field.TryGetProperty(name, out var text)) Text(text, name == "help" ? 4096 : 256);
        if (field.TryGetProperty("validator", out _))
            Choice(field, "validator", DefinitionVocabulary.Validators);
        foreach ((string low, string high) in new[]
            { ("minLength", "maxLength"), ("minItems", "maxItems"), ("minimum", "maximum") })
        {
            decimal? min = field.TryGetProperty(low, out var a) ? a.GetDecimal() : null;
            decimal? max = field.TryGetProperty(high, out var b) ? b.GetDecimal() : null;
            if (min > max || low != "minimum" && (min < 0 || max < 0
                || min != decimal.Truncate(min ?? 0) && min.HasValue
                || max != decimal.Truncate(max ?? 0) && max.HasValue))
                throw new SetupDefinitionException("constraint-invalid");
        }
        string type = field.GetProperty("type").GetString()!;
        bool secretContext = secretAncestors || field.GetProperty("sensitivity").GetString() == "secret";
        if (field.TryGetProperty("guidance", out var guidance)) ValidateGuidance(guidance);
        if (field.TryGetProperty("credential", out var credential))
        {
            Choice(credential, "kind", "password", "opaque-token", "symmetric-key", "private-key", "connection-string");
            Choice(credential, "acquisition", "existing", "create", "issuer");
            string kind = credential.GetProperty("kind").GetString()!;
            string acquisition = credential.GetProperty("acquisition").GetString()!;
            if (type != "string" || !secretContext
                || acquisition == "create" && kind is not ("password" or "symmetric-key")
                || acquisition == "issuer" && kind == "symmetric-key")
                throw new SetupDefinitionException("credential-invalid");
        }
        foreach ((string name, bool allowed) in new[]
        {
            ("minLength", type is "string" or "enum"), ("maxLength", type is "string" or "enum"),
            ("minimum", type is "integer" or "number"), ("maximum", type is "integer" or "number"),
            ("minItems", type == "list"), ("maxItems", type == "list")
        })
            if (!allowed && field.TryGetProperty(name, out _)) throw new SetupDefinitionException("constraint-invalid");
        if (type == "enum")
        {
            if (!field.TryGetProperty("choices", out var choices) || choices.GetArrayLength() is < 1 or > 256
                || choices.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)
                || choices.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count()
                    != choices.GetArrayLength())
                throw new SetupDefinitionException("choices-invalid");
        }
        else if (field.TryGetProperty("choices", out _)) throw new SetupDefinitionException("constraint-invalid");
        if (type == "list") Choice(field, "itemType", DefinitionVocabulary.ItemTypes);
        else if (field.TryGetProperty("itemType", out _)) throw new SetupDefinitionException("constraint-invalid");
        bool objectList = type == "list" && field.GetProperty("itemType").GetString() == "object";
        if (type == "object" || objectList)
        {
            if (!field.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Array)
                throw new SetupDefinitionException("properties-invalid");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement property in properties.EnumerateArray())
            {
                if (!ids.Add(Identifier(property.GetProperty("id"))))
                    throw new SetupDefinitionException("field-duplicate");
                ValidateField(property, nested: true, publicAncestors: publicContext, secretAncestors: secretContext);
            }
        }
        else if (field.TryGetProperty("properties", out _)) throw new SetupDefinitionException("constraint-invalid");
        if (field.TryGetProperty("uniqueBy", out var uniqueBy))
        {
            if (!objectList || uniqueBy.GetArrayLength() is < 1 or > SetupCompositionLimits.DefaultNestingDepth)
                throw new SetupDefinitionException("unique-key-invalid");
            JsonElement current = field;
            int position = 0;
            foreach (JsonElement segment in uniqueBy.EnumerateArray())
            {
                string name = Identifier(segment);
                if (!current.TryGetProperty("properties", out var properties))
                    throw new SetupDefinitionException("unique-key-invalid");
                JsonElement child = properties.EnumerateArray().FirstOrDefault(property => property.GetProperty("id").GetString() == name);
                if (child.ValueKind == JsonValueKind.Undefined || !child.TryGetProperty("required", out var required) || !required.GetBoolean())
                    throw new SetupDefinitionException("unique-key-invalid");
                current = child;
                if (++position < uniqueBy.GetArrayLength() && current.GetProperty("type").GetString() != "object")
                    throw new SetupDefinitionException("unique-key-invalid");
            }
            if (current.GetProperty("type").GetString() != "string")
                throw new SetupDefinitionException("unique-key-invalid");
        }
        if (nested && (field.TryGetProperty("activeWhen", out _) || field.TryGetProperty("requiredWhen", out _)))
            throw new SetupDefinitionException("nested-condition");
        foreach (string name in new[] { "default", "recommended" })
        {
            if (!field.TryGetProperty(name, out var value)) continue;
            if (!publicContext
                || !DefinitionValidator.ValueValid(field, value)
                || HasProtectedProperty(field))
                throw new SetupDefinitionException("default-invalid");
        }
    }

    private static void ValidatePresentation(JsonElement presentation, HashSet<string> roots)
    {
        JsonElement sections = presentation.GetProperty("sections");
        JsonElement experiences = presentation.GetProperty("experiences");
        if (sections.GetArrayLength() is < 1 or > 64 || experiences.GetArrayLength() is < 1 or > 16)
            throw new SetupDefinitionException("presentation-limit");
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement section in sections.EnumerateArray())
        {
            if (!sectionIds.Add(PresentationItem(section))) throw new SetupDefinitionException("presentation-duplicate");
            if (section.TryGetProperty("disclosure", out _)) Choice(section, "disclosure", DefinitionVocabulary.Disclosures);
            JsonElement fields = section.GetProperty("fields");
            if (fields.GetArrayLength() is < 1 or > 1024) throw new SetupDefinitionException("presentation-reference");
            foreach (JsonElement field in fields.EnumerateArray())
                if (!roots.Contains(field.GetString()!) || !covered.Add(field.GetString()!))
                    throw new SetupDefinitionException("presentation-reference");
        }
        if (!covered.SetEquals(roots)) throw new SetupDefinitionException("presentation-coverage");
        var experienceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement experience in experiences.EnumerateArray())
        {
            if (!experienceIds.Add(PresentationItem(experience))) throw new SetupDefinitionException("presentation-duplicate");
            if (experience.TryGetProperty("initialDisclosure", out _))
                Choice(experience, "initialDisclosure", DefinitionVocabulary.Disclosures);
            if (experience.TryGetProperty("navigation", out _)) Choice(experience, "navigation", "guided", "sections");
            JsonElement references = experience.GetProperty("sections");
            var listed = new HashSet<string>(StringComparer.Ordinal);
            if (references.GetArrayLength() != sectionIds.Count) throw new SetupDefinitionException("presentation-coverage");
            foreach (JsonElement reference in references.EnumerateArray())
                if (!sectionIds.Contains(reference.GetString()!) || !listed.Add(reference.GetString()!))
                    throw new SetupDefinitionException("presentation-reference");
        }
        if (!experienceIds.Contains(presentation.GetProperty("defaultExperience").GetString()!))
            throw new SetupDefinitionException("presentation-default");
    }

    private static string PresentationItem(JsonElement item)
    {
        string id = Text(item.GetProperty("id"), 64);
        if (id[0] is < 'a' or > 'z' || id[^1] == '-' || id.Contains("--", StringComparison.Ordinal)
            || !id.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new SetupDefinitionException("presentation-identifier");
        Text(item.GetProperty("title"), 128);
        if (item.TryGetProperty("description", out var description)) Text(description, 2048);
        return id;
    }

    private static void ValidateGuidance(JsonElement guidance)
    {
        if (guidance.TryGetProperty("instructions", out var instructions))
        {
            string text = Text(instructions, 4096);
            if (text.Contains('`', StringComparison.Ordinal) || text.Contains("](", StringComparison.Ordinal)
                || text.Contains("${", StringComparison.Ordinal) || text.Contains("{{", StringComparison.Ordinal)
                || text.Contains("}}", StringComparison.Ordinal))
                throw new SetupDefinitionException("guidance-invalid");
        }
        if (guidance.TryGetProperty("links", out var links))
        {
            if (links.GetArrayLength() > 4) throw new SetupDefinitionException("guidance-limit");
            foreach (JsonElement link in links.EnumerateArray())
            {
                Text(link.GetProperty("title"), 128);
                string url = Text(link.GetProperty("url"), 2048);
                if (url.Any(char.IsWhiteSpace) || url.Contains('?', StringComparison.Ordinal)
                    || url.Contains('#', StringComparison.Ordinal)
                    || url.Contains("${", StringComparison.Ordinal) || url.Contains("{{", StringComparison.Ordinal)
                    || url.Contains("}}", StringComparison.Ordinal)
                    || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
                    || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                    throw new SetupDefinitionException("guidance-link");
            }
        }
        if (guidance.TryGetProperty("searchTerms", out var terms))
        {
            if (terms.GetArrayLength() > 16) throw new SetupDefinitionException("guidance-limit");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement term in terms.EnumerateArray())
                if (!unique.Add(Text(term, 64))) throw new SetupDefinitionException("guidance-duplicate");
        }
    }

    private static bool HasProtectedProperty(JsonElement field) =>
        field.TryGetProperty("properties", out var properties) && properties.EnumerateArray().Any(property =>
            property.GetProperty("sensitivity").GetString() != "public" || HasProtectedProperty(property));

    internal static void ValidateCondition(JsonElement condition, HashSet<string> ids, HashSet<string> references)
    {
        string op = condition.GetProperty("op").GetString()!;
        bool compound = op is "all" or "any" or "not";
        if (compound)
        {
            Only(condition, "op", "operands");
            JsonElement operands = condition.GetProperty("operands");
            if (operands.GetArrayLength() < (op == "not" ? 1 : 2)
                || op == "not" && operands.GetArrayLength() != 1)
                throw new SetupDefinitionException("condition-arity");
            foreach (JsonElement child in operands.EnumerateArray()) ValidateCondition(child, ids, references);
        }
        else
        {
            if (op is not ("present" or "equals" or "notEquals" or "in"))
                throw new SetupDefinitionException("condition-operation");
            Only(condition, op == "present" ? ["op", "field"] : ["op", "field", "value"]);
            string id = condition.GetProperty("field").GetString()!;
            if (!ids.Contains(id)) throw new SetupDefinitionException("field-unknown");
            references.Add(id);
            if (op != "present" && (!condition.TryGetProperty("value", out var value)
                || op == "in" && value.ValueKind != JsonValueKind.Array))
                throw new SetupDefinitionException("condition-value");
        }
    }

    private static void ValidateProjection(
        JsonElement projection, HashSet<string> ids, HashSet<string> listIds, bool inRepeat)
    {
        string op = projection.GetProperty("op").GetString()!;
        string[] members = op switch
        {
            "literal" => ["op", "value", "when"],
            "copy" => ["op", "field", "when"],
            "omit" or "index" => ["op", "when"],
            "object" => ["op", "members", "when"],
            "list" => ["op", "items", "when"],
            "repeat" => ["op", "field", "item", "when"],
            _ => throw new SetupDefinitionException("projection-operation")
        };
        Only(projection, members);
        if (projection.TryGetProperty("when", out var condition)) ValidateCondition(condition, ids, []);
        switch (op)
        {
            case "literal":
                _ = projection.GetProperty("value");
                break;
            case "copy":
            case "repeat":
                string id = projection.GetProperty("field").GetString()!;
                if (id == "$item" ? !inRepeat || op == "repeat" : !ids.Contains(id))
                    throw new SetupDefinitionException("projection-field");
                if (op == "repeat")
                {
                    if (inRepeat) throw new SetupDefinitionException("repeat-nesting");
                    if (!listIds.Contains(id)) throw new SetupDefinitionException("repeat-source");
                    ValidateProjection(projection.GetProperty("item"), ids, listIds, inRepeat: true);
                }
                break;
            case "index":
                if (!inRepeat) throw new SetupDefinitionException("index-context");
                break;
            case "object":
                foreach (JsonProperty item in projection.GetProperty("members").EnumerateObject())
                    ValidateProjection(item.Value, ids, listIds, inRepeat);
                break;
            case "list":
                foreach (JsonElement item in projection.GetProperty("items").EnumerateArray())
                    ValidateProjection(item, ids, listIds, inRepeat);
                break;
        }
    }

    internal static string Text(JsonElement value, int maximum)
    {
        string text = value.GetString() ?? throw new SetupDefinitionException("text-invalid");
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)
            || text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal))
            throw new SetupDefinitionException("text-invalid");
        return text;
    }

    private static string Identifier(JsonElement value)
    {
        string text = Text(value, 128);
        if (!char.IsAsciiLetter(text[0])
            || !text.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
            throw new SetupDefinitionException("identifier-invalid");
        return text;
    }

    private static void Choice(JsonElement value, string name, params string[] allowed)
    {
        if (!allowed.Contains(value.GetProperty(name).GetString(), StringComparer.Ordinal))
            throw new SetupDefinitionException("vocabulary-invalid");
    }

    private static void Only(JsonElement element, params string[] allowed)
    {
        if (element.EnumerateObject().Any(item => !allowed.Contains(item.Name, StringComparer.Ordinal)))
            throw new SetupDefinitionException("member-invalid");
    }
}

// These closed wire records are shared with the definition schema exporter.
internal sealed record DefinitionDocument
{
    public required int SchemaVersion { get; init; }
    public required string FamilyId { get; init; }
    public required long Revision { get; init; }
    public required string ApplicationId { get; init; }
    public required IReadOnlyList<string> ApplicationVersions { get; init; }
    public required string Publisher { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> EntryPoints { get; init; }
    public required IReadOnlyList<string> Locales { get; init; }
    public required IReadOnlyList<FieldDocument> Fields { get; init; }
    public required IReadOnlyList<ArtifactDocument> Artifacts { get; init; }
    public PresentationDocument? Presentation { get; init; }
}

internal sealed record FieldDocument
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Sensitivity { get; init; }
    public required string Disclosure { get; init; }
    public bool Required { get; init; }
    public string? Help { get; init; }
    public string? Group { get; init; }
    public string? Format { get; init; }
    public string? Consequence { get; init; }
    public GuidanceDocument? Guidance { get; init; }
    public CredentialDocument? Credential { get; init; }
    public string? Validator { get; init; }
    public JsonElement? Default { get; init; }
    public JsonElement? Recommended { get; init; }
    public IReadOnlyList<string>? Choices { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public int? MinItems { get; init; }
    public int? MaxItems { get; init; }
    public string? ItemType { get; init; }
    public IReadOnlyList<string>? UniqueBy { get; init; }
    public IReadOnlyList<FieldDocument>? Properties { get; init; }
    public ConditionDocument? ActiveWhen { get; init; }
    public ConditionDocument? RequiredWhen { get; init; }
}

internal sealed record PresentationDocument
{
    public required string DefaultExperience { get; init; }
    public required IReadOnlyList<SectionDocument> Sections { get; init; }
    public required IReadOnlyList<ExperienceDocument> Experiences { get; init; }
}

internal sealed record SectionDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Disclosure { get; init; }
    public required IReadOnlyList<string> Fields { get; init; }
}

internal sealed record ExperienceDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? InitialDisclosure { get; init; }
    public string? Navigation { get; init; }
    public required IReadOnlyList<string> Sections { get; init; }
}

internal sealed record GuidanceDocument
{
    public string? Instructions { get; init; }
    public IReadOnlyList<GuidanceLinkDocument>? Links { get; init; }
    public IReadOnlyList<string>? SearchTerms { get; init; }
}

internal sealed record GuidanceLinkDocument
{
    public required string Title { get; init; }
    public required string Url { get; init; }
}

internal sealed record CredentialDocument
{
    public required string Kind { get; init; }
    public required string Acquisition { get; init; }
}

internal sealed record ConditionDocument
{
    public required string Op { get; init; }
    public string? Field { get; init; }
    public JsonElement? Value { get; init; }
    public IReadOnlyList<ConditionDocument>? Operands { get; init; }
}

internal sealed record ArtifactDocument
{
    public required string Id { get; init; }
    public required string Format { get; init; }
    public required string FileName { get; init; }
    public required string ApplySemantics { get; init; }
    public required string Restart { get; init; }
    public required ProjectionDocument Projection { get; init; }
}

internal sealed record ProjectionDocument
{
    public required string Op { get; init; }
    public JsonElement? Value { get; init; }
    public string? Field { get; init; }
    public IReadOnlyDictionary<string, ProjectionDocument>? Members { get; init; }
    public IReadOnlyList<ProjectionDocument>? Items { get; init; }
    public ProjectionDocument? Item { get; init; }
    public ConditionDocument? When { get; init; }
}
