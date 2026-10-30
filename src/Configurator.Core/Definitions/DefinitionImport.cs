namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Environment;

public sealed record DefinitionImportResult(bool Succeeded, SetupAnswerSnapshot? Answers, string Code)
{
    public IReadOnlyList<string> ProvidedFields { get; init; } = [];
    public DefinitionDiagnostic? Diagnostic { get; init; }
    public DefinitionImportSchemaLocation? Schema { get; init; }
    public override string ToString() => $"{nameof(DefinitionImportResult)}:{Code}";
}

public static class DefinitionImport
{
    public static DefinitionImportCapability GetCapability(SetupDefinition definition, string artifactId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        try
        {
            bool supported = DefinitionImportProjection.TryCompile(definition, artifactId, new(CancellationToken.None),
                out _, out string code);
            return new(supported, code);
        }
        catch (DefinitionImportLimitException) { return new(false, "inverse-limit"); }
    }

    public static bool CanImport(SetupDefinition definition, string artifactId) =>
        GetCapability(definition, artifactId).StrictAutofillSupported;

    /// <summary>Inspects one ordinary artifact without initialization or workspace mutation.</summary>
    public static DefinitionImportAssessment Inspect(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();
        var work = new DefinitionImportWork(cancellationToken);
        var findings = new List<DefinitionImportFinding>();
        var reportedDiagnostics = new HashSet<DefinitionDiagnostic>();
        var fieldsById = definition.Fields.ToDictionary(field => field.Id, StringComparer.Ordinal);
        string publicArtifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId)?.Id ?? "";
        DefinitionImportEvidence? evidence = null;
        bool inventory = true;
        bool coverage = true;
        bool autofill = false;
        string? refusal = null;
        try
        {
            if (!DefinitionImportProjection.TryCompile(definition, artifactId, work, out var projection, out string code))
                return Terminal(code, DefinitionImportCompatibility.Unsupported);
            JsonElement input = Parse(projection!.Artifact.Format, bytes, cancellationToken);
            DefinitionImportEvidence recovered = projection.Recover(definition, input, work, Add);
            evidence = recovered;
            DefinitionValidationResult validation = DefinitionValidator.ValidateSupplied(definition, evidence, work,
                diagnostic => RecordDiagnostic(recovered, diagnostic), cancellationToken);
            foreach (string name in validation.UnresolvedFields)
            {
                work.Visit();
                SetupFieldDefinition? field = definition.Fields.FirstOrDefault(item => item.Name == name);
                if (field is null || findings.Any(item => item.Kind == DefinitionImportFindingKind.Unassessed && item.Schema?.FieldId == field.Id)) continue;
                coverage = false;
                Add(new(DefinitionImportFindingKind.Unassessed, "condition-unobserved",
                    evidence.Source(DefinitionPath.FromSegments([name])), new(field.Id, [])));
            }
            foreach (DefinitionPath path in evidence.Assignments)
            {
                work.Visit();
                if (!evidence.TryValue(path, out JsonElement supplied)) continue;
                if (validation.TryGetEffectiveValue(path, out JsonElement effective) && RetainsMembers(supplied, effective, work)) continue;
                // Unresolved activity cannot silently discard represented input.
                coverage = false;
                Add(new(DefinitionImportFindingKind.Unassessed, "mapping-unresolved",
                    evidence.Source(path), DefinitionImportProjection.SchemaLocation(definition, path)));
            }
            refusal = StructuralRefusal();
            if (refusal is null)
                autofill = Merge(definition, evidence, new(0, new Dictionary<string, JsonElement>()), null,
                    cancellationToken, work).Succeeded;
        }
        catch (DefinitionImportLimitException)
        {
            inventory = false;
            coverage = false;
            refusal = "assessment-limit";
        }
        catch (Exception exception) when (exception is SetupCompositionException or JsonException or SetupDefinitionException
            or OverflowException or InvalidOperationException or DecoderFallbackException)
        {
            return Terminal("input-invalid", DefinitionImportCompatibility.InvalidInput);
        }
        cancellationToken.ThrowIfCancellationRequested();
        bool changes = findings.Any(item => item.Kind is DefinitionImportFindingKind.MissingRequired
            or DefinitionImportFindingKind.InvalidSuppliedValue or DefinitionImportFindingKind.UnrecognizedInput
            or DefinitionImportFindingKind.StructuralOutputChange);
        DefinitionImportCompatibility compatibility = changes ? DefinitionImportCompatibility.ChangesRequired
            : !coverage || !inventory ? DefinitionImportCompatibility.Incomplete : DefinitionImportCompatibility.Compatible;
        string resultCode = compatibility switch
        {
            DefinitionImportCompatibility.ChangesRequired => "changes-required",
            DefinitionImportCompatibility.Incomplete => "assessment-incomplete",
            _ => "compatible"
        };
        return new(new(definition.Digest.ToString(), publicArtifact, resultCode, compatibility, true,
            coverage && inventory, inventory, autofill && inventory, findings, work.Units, work.InverseNodes,
            work.ValidationNodes), definition, evidence, refusal);

        void Add(DefinitionImportFinding finding)
        {
            work.Visit();
            if (findings.Count == SetupCompositionLimits.DefaultSequenceEntries) throw new DefinitionImportLimitException();
            findings.Add(finding);
        }
        void RecordDiagnostic(DefinitionImportEvidence observed, DefinitionDiagnostic diagnostic)
        {
            if (!reportedDiagnostics.Add(diagnostic)) return;
            DefinitionImportFindingKind kind = diagnostic.Code switch
            {
                "value-required" => DefinitionImportFindingKind.MissingRequired,
                "value-unobserved" or "condition-unobserved" or "value-partially-observed" or "validation-limit" =>
                    DefinitionImportFindingKind.Unassessed,
                _ => DefinitionImportFindingKind.InvalidSuppliedValue
            };
            if (kind == DefinitionImportFindingKind.Unassessed) coverage = false;
            if (diagnostic.Code == "validation-limit") inventory = false;
            fieldsById.TryGetValue(diagnostic.FieldId, out SetupFieldDefinition? field);
            var safe = new List<string>();
            foreach (DefinitionLocationSegment segment in diagnostic.Location?.Segments ?? [])
            {
                if (segment.Kind != DefinitionLocationSegmentKind.Property) break;
                safe.Add(segment.PropertyName!);
            }
            Add(new(kind, diagnostic.Code,
                observed.Source(DefinitionPath.FromSegments(field is null ? [] : [field.Name]),
                    diagnostic.Location?.Segments ?? [], work),
                field is null ? null : new(field.Id, safe)));
        }
        string? StructuralRefusal() => findings.Any(item => item.Kind == DefinitionImportFindingKind.StructuralOutputChange ||
            item.Code == "input-unrecognized") ? "inverse-unsupported" : null;
        DefinitionImportAssessment Terminal(string code, DefinitionImportCompatibility compatibility) =>
            new(new(definition.Digest.ToString(), publicArtifact, code, compatibility, false, false, true,
                false, [], work.Units, work.InverseNodes, work.ValidationNodes), refusal: code);
    }

    public static DefinitionImportResult Assess(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, SetupAnswerSnapshot current, IReadOnlySet<string>? replaceableDefaults = null)
    {
        using DefinitionImportAssessment assessment = Inspect(definition, artifactId, bytes);
        return assessment.AssessAutofill(current, replaceableDefaults);
    }

    internal static DefinitionImportResult Merge(SetupDefinition definition, DefinitionImportEvidence evidence,
        SetupAnswerSnapshot current, IReadOnlySet<string>? replaceableDefaults, CancellationToken cancellationToken,
        DefinitionImportWork? work = null)
    {
        bool ownsWork = work is null;
        work ??= new(cancellationToken);
        try
        {
            var values = current.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            var imported = new HashSet<string>(StringComparer.Ordinal);
            var assignments = new Dictionary<DefinitionPath, JsonElement>();
            foreach (DefinitionPath path in evidence.Assignments)
            {
                work.Visit();
                if (!evidence.TryValue(path, out JsonElement value)) continue;
                if (path.Segments.Count == 0)
                    foreach (JsonProperty member in value.EnumerateObject())
                        assignments.Add(DefinitionPath.FromSegments([member.Name]), member.Value);
                else assignments.Add(path, value);
            }
            foreach ((DefinitionPath path, JsonElement answer) in assignments.OrderBy(item => item.Key.ToPointer(), StringComparer.Ordinal))
            {
                work.Visit();
                string field = path.Segments[0];
                imported.Add(field);
                SetupFieldDefinition? descriptor = definition.Fields.FirstOrDefault(item => item.Name == field);
                DefinitionDiagnostic? conflict = descriptor is null ? null : new("answer-conflict", descriptor.Id);
                foreach (DefinitionPath omitted in current.Omitted)
                {
                    work.Visit();
                    if (DefinitionImportProjection.IsPrefix(omitted, path) ||
                        DefinitionImportProjection.IsPrefix(path, omitted) && TrySelect(answer, omitted, path.Segments.Count, out _))
                        return Failed("answer-conflict", conflict);
                }
                bool replaceable = descriptor is not null && replaceableDefaults?.Contains(descriptor.Id) == true;
                if (path.Segments.Count == 1)
                {
                    if (values.TryGetValue(field, out JsonElement previous) &&
                        !JsonElement.DeepEquals(previous, answer) && !replaceable) return Failed("answer-conflict", conflict);
                    values[field] = answer;
                    continue;
                }
                JsonObject root;
                if (!values.TryGetValue(field, out JsonElement existing)) root = new();
                else if (existing.ValueKind == JsonValueKind.Object) root = JsonNode.Parse(existing.GetRawText())!.AsObject();
                else if (replaceable) root = new();
                else return Failed("answer-conflict", conflict);
                JsonObject container = root;
                for (int index = 1; index < path.Segments.Count - 1; index++)
                {
                    string segment = path.Segments[index];
                    if (container.TryGetPropertyValue(segment, out JsonNode? child))
                    {
                        if (child is JsonObject mapping) { container = mapping; continue; }
                        if (!replaceable) return Failed("answer-conflict", conflict);
                    }
                    var created = new JsonObject();
                    container[segment] = created;
                    container = created;
                }
                string leaf = path.Segments[^1];
                if (container.TryGetPropertyValue(leaf, out JsonNode? prior) &&
                    !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(prior), answer) && !replaceable)
                    return Failed("answer-conflict", conflict);
                container[leaf] = JsonNode.Parse(answer.GetRawText());
                values[field] = JsonSerializer.SerializeToElement(root);
            }
            var candidate = new SetupAnswerSnapshot(checked(current.InputRevision + 1), values, current.Omitted);
            DefinitionValidationResult validation = DefinitionValidator.ValidateImport(definition, candidate, work, cancellationToken);
            var rejected = validation.Fields.FirstOrDefault(field => imported.Contains(field.Definition.Name) && !field.IsActive);
            if (rejected is not null) return Failed("import-field-invalid", new("import-field-invalid", rejected.Id));
            if (validation.Diagnostics.FirstOrDefault(item => item.Code != "value-required") is { } diagnostic)
                return Failed("import-field-invalid", definition.Fields.Any(field => field.Id == diagnostic.FieldId) ? diagnostic : null);
            foreach ((DefinitionPath path, JsonElement value) in assignments)
            {
                if (!validation.TryGetEffectiveValue(path, out JsonElement effective) || !RetainsMembers(value, effective, work))
                {
                    SetupFieldDefinition? descriptor = definition.Fields.FirstOrDefault(field => field.Name == path.Segments[0]);
                    return Failed("import-field-invalid", descriptor is null ? null : new("import-field-invalid", descriptor.Id));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, candidate, "import-ready")
            {
                ProvidedFields = Array.AsReadOnly(definition.Fields.Where(field => imported.Contains(field.Name))
                    .Select(field => field.Id).ToArray())
            };
        }
        catch (DefinitionImportLimitException) when (ownsWork)
        {
            return Failed("assessment-limit");
        }
        catch (Exception exception) when (exception is SetupCompositionException or JsonException or SetupDefinitionException
            or OverflowException or InvalidOperationException)
        {
            return Failed("input-invalid");
        }
    }

    private static bool TrySelect(JsonElement value, DefinitionPath path, int start, out JsonElement selected)
    {
        selected = value;
        for (int index = start; index < path.Segments.Count; index++)
        {
            if (selected.ValueKind == JsonValueKind.Object)
            {
                if (!selected.TryGetProperty(path.Segments[index], out selected)) return false;
            }
            else if (selected.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position) &&
                position < selected.GetArrayLength()) selected = selected[position];
            else return false;
        }
        return true;
    }

    private static bool RetainsMembers(JsonElement supplied, JsonElement effective, DefinitionImportWork work)
    {
        work.Visit();
        if (supplied.ValueKind == JsonValueKind.Object)
            return effective.ValueKind == JsonValueKind.Object && supplied.EnumerateObject().All(member =>
                effective.TryGetProperty(member.Name, out JsonElement child) && RetainsMembers(member.Value, child, work));
        if (supplied.ValueKind == JsonValueKind.Array)
            return effective.ValueKind == JsonValueKind.Array && supplied.GetArrayLength() == effective.GetArrayLength() &&
                supplied.EnumerateArray().Zip(effective.EnumerateArray()).All(pair => RetainsMembers(pair.First, pair.Second, work));
        return JsonElement.DeepEquals(supplied, effective);
    }

    internal static bool TryDotenvValue(DefinitionNode field, string text, out JsonElement value)
    {
        value = JsonSerializer.SerializeToElement(text);
        if (field.Type == DefinitionNodeType.Text) return true;
        if (field.Type == DefinitionNodeType.Boolean && text is "true" or "false")
        {
            value = JsonSerializer.SerializeToElement(text == "true");
            return true;
        }
        if (field.Type == DefinitionNodeType.WholeNumber && long.TryParse(text, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out long integer))
        {
            value = JsonSerializer.SerializeToElement(integer);
            return value.GetRawText() == text;
        }
        if (field.Type == DefinitionNodeType.Number && decimal.TryParse(text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number))
        {
            value = JsonSerializer.SerializeToElement(number);
            return value.GetRawText() == text;
        }
        return false;
    }

    private static JsonElement Parse(string format, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (bytes.IsEmpty || bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
            throw new SetupDefinitionException("input-invalid");
        cancellationToken.ThrowIfCancellationRequested();
        if (format == "dotenv")
        {
            DotenvParseResult parsed = DotenvCodec.Parse(bytes);
            if (!parsed.Succeeded) throw new SetupDefinitionException("input-invalid");
            var entries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (DotenvEntry entry in parsed.Document!.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Value is null || !entries.TryAdd(entry.Key, JsonSerializer.SerializeToElement(entry.Value)))
                    throw new SetupDefinitionException("input-invalid");
            }
            return JsonSerializer.SerializeToElement(entries);
        }
        byte[] normalized;
        if (format == "yaml")
            normalized = SetupCompositionNormalizer.WriteJson(SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default,
                cancellationToken, keyPolicy: CompositionKeyPolicy.Exact), cancellationToken);
        else
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes.Span);
            using JsonDocument parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = SetupCompositionLimits.DefaultNestingDepth });
            normalized = DefinitionJson.Canonicalize(parsed.RootElement, keyPolicy: CompositionKeyPolicy.Exact);
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(normalized);
            return document.RootElement.Clone();
        }
        finally { CryptographicOperations.ZeroMemory(normalized); }
    }

    private static DefinitionImportResult Failed(string code, DefinitionDiagnostic? diagnostic = null) =>
        new(false, null, code)
        {
            Diagnostic = diagnostic is null ? null : new DefinitionDiagnostic(diagnostic.Code, diagnostic.FieldId),
            Schema = diagnostic is null || diagnostic.FieldId.Length == 0 ? null :
                new(diagnostic.FieldId, (diagnostic.Location?.Segments ?? [])
                    .TakeWhile(segment => segment.Kind == DefinitionLocationSegmentKind.Property)
                    .Select(segment => segment.PropertyName!))
        };
}
