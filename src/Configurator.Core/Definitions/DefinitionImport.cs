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
            var compiled = DefinitionImportWork.CompleteSynchronously(
                DefinitionImportProjection.CompileAsync(definition, artifactId, new(CancellationToken.None)));
            return new(compiled.Projection is not null, compiled.Code);
        }
        catch (DefinitionImportLimitException) { return new(false, "inverse-limit"); }
    }

    public static bool CanImport(SetupDefinition definition, string artifactId) =>
        GetCapability(definition, artifactId).StrictAutofillSupported;

    /// <summary>Inspects one ordinary artifact without initialization or workspace mutation.</summary>
    public static DefinitionImportAssessment Inspect(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(InspectCoreAsync(definition, artifactId, bytes, null, cancellationToken));

    /// <summary>Yields before inspection and in bounded traversal, normalization and materialization slices; completed callbacks keep execution synchronous.</summary>
    public static ValueTask<DefinitionImportAssessment> InspectAsync(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, Func<CancellationToken, ValueTask> cooperativeYield,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativeYield);
        return InspectCoreAsync(definition, artifactId, bytes, cooperativeYield, cancellationToken);
    }

    private static async ValueTask<DefinitionImportAssessment> InspectCoreAsync(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, Func<CancellationToken, ValueTask>? cooperativeYield, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();
        if (cooperativeYield is not null)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        var work = new DefinitionImportWork(cancellationToken, cooperativeYield);
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
            var (projection, code) = await DefinitionImportProjection.CompileAsync(definition, artifactId, work);
            if (projection is null)
                return Terminal(code, DefinitionImportCompatibility.Unsupported);
            JsonElement input = await ParseAsync(projection!.Artifact.Format, bytes, work, cancellationToken);
            DefinitionImportEvidence recovered = await projection.RecoverAsync(definition, input, work, Add);
            evidence = recovered;
            DefinitionValidationResult validation = await DefinitionValidator.ValidateSuppliedAsync(definition, evidence, work,
                diagnostic => RecordDiagnostic(recovered, diagnostic), cancellationToken);
            foreach (string name in validation.UnresolvedFields)
            {
                await work.VisitAsync();
                SetupFieldDefinition? field = definition.Fields.FirstOrDefault(item => item.Name == name);
                if (field is null || findings.Any(item => item.Kind == DefinitionImportFindingKind.Unassessed && item.Schema?.FieldId == field.Id)) continue;
                coverage = false;
                await Add(new(DefinitionImportFindingKind.Unassessed, "condition-unobserved",
                    evidence.Source(DefinitionPath.FromSegments([name])), new(field.Id, [])));
            }
            foreach (DefinitionPath path in evidence.Assignments)
            {
                await work.VisitAsync();
                if (!evidence.TryValue(path, out JsonElement supplied)) continue;
                if (validation.TryGetEffectiveValue(path, out JsonElement effective) && await RetainsMembersAsync(supplied, effective, work)) continue;
                // Unresolved activity cannot silently discard represented input.
                coverage = false;
                await Add(new(DefinitionImportFindingKind.Unassessed, "mapping-unresolved",
                    evidence.Source(path), DefinitionImportProjection.SchemaLocation(definition, path)));
            }
            refusal = StructuralRefusal();
            if (refusal is null)
                autofill = (await MergeAsync(definition, evidence, new(0, new Dictionary<string, JsonElement>()), null,
                    cancellationToken, work)).Succeeded;
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

        async ValueTask Add(DefinitionImportFinding finding)
        {
            await work.VisitAsync();
            if (findings.Count == SetupCompositionLimits.DefaultSequenceEntries) throw new DefinitionImportLimitException();
            findings.Add(finding);
        }
        async ValueTask RecordDiagnostic(DefinitionImportEvidence observed, DefinitionDiagnostic diagnostic)
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
            await Add(new(kind, diagnostic.Code,
                await observed.SourceAsync(DefinitionPath.FromSegments(field is null ? [] : [field.Name]),
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

    internal static async ValueTask<DefinitionImportResult> MergeAsync(SetupDefinition definition, DefinitionImportEvidence evidence,
        SetupAnswerSnapshot current, IReadOnlySet<string>? replaceableDefaults, CancellationToken cancellationToken,
        DefinitionImportWork? work = null, Func<CancellationToken, ValueTask>? cooperativeYield = null)
    {
        bool ownsWork = work is null;
        work ??= new(cancellationToken, cooperativeYield);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var values = current.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            var imported = new HashSet<string>(StringComparer.Ordinal);
            var assignments = new Dictionary<DefinitionPath, JsonElement>();
            foreach (DefinitionPath path in evidence.Assignments)
            {
                await work.VisitAsync();
                if (!evidence.TryValue(path, out JsonElement value)) continue;
                if (path.Segments.Count == 0)
                    foreach (JsonProperty member in value.EnumerateObject())
                        assignments.Add(DefinitionPath.FromSegments([member.Name]), member.Value);
                else assignments.Add(path, value);
            }
            foreach ((DefinitionPath path, JsonElement answer) in assignments.OrderBy(item => item.Key.ToPointer(), StringComparer.Ordinal))
            {
                await work.VisitAsync();
                string field = path.Segments[0];
                imported.Add(field);
                SetupFieldDefinition? descriptor = definition.Fields.FirstOrDefault(item => item.Name == field);
                DefinitionDiagnostic? conflict = descriptor is null ? null : new("answer-conflict", descriptor.Id);
                foreach (DefinitionPath omitted in current.Omitted)
                {
                    await work.VisitAsync();
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
            SetupAnswerSnapshot candidate = work.CooperativeYield is { } yield
                ? await SetupAnswerSnapshot.CreateAsync(checked(current.InputRevision + 1), values, current.Omitted,
                    yield, cancellationToken)
                : new(checked(current.InputRevision + 1), values, current.Omitted);
            DefinitionValidationResult validation = await DefinitionValidator.ValidateImportAsync(definition, candidate, work, cancellationToken);
            var rejected = validation.Fields.FirstOrDefault(field => imported.Contains(field.Definition.Name) && !field.IsActive);
            if (rejected is not null) return Failed("import-field-invalid", new("import-field-invalid", rejected.Id));
            if (validation.Diagnostics.FirstOrDefault(item => item.Code != "value-required") is { } diagnostic)
                return Failed("import-field-invalid", definition.Fields.Any(field => field.Id == diagnostic.FieldId) ? diagnostic : null);
            foreach ((DefinitionPath path, JsonElement value) in assignments)
            {
                if (!validation.TryGetEffectiveValue(path, out JsonElement effective) || !await RetainsMembersAsync(value, effective, work))
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

    private static async ValueTask<bool> RetainsMembersAsync(JsonElement supplied, JsonElement effective, DefinitionImportWork work)
    {
        await work.VisitAsync();
        if (supplied.ValueKind == JsonValueKind.Object)
        {
            if (effective.ValueKind != JsonValueKind.Object) return false;
            foreach (JsonProperty member in supplied.EnumerateObject())
                if (!effective.TryGetProperty(member.Name, out JsonElement child) ||
                    !await RetainsMembersAsync(member.Value, child, work)) return false;
            return true;
        }
        if (supplied.ValueKind == JsonValueKind.Array)
        {
            if (effective.ValueKind != JsonValueKind.Array || supplied.GetArrayLength() != effective.GetArrayLength()) return false;
            foreach (var pair in supplied.EnumerateArray().Zip(effective.EnumerateArray()))
                if (!await RetainsMembersAsync(pair.First, pair.Second, work)) return false;
            return true;
        }
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

    private static async ValueTask<JsonElement> ParseAsync(string format, ReadOnlyMemory<byte> bytes,
        DefinitionImportWork work, CancellationToken cancellationToken)
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
            normalized = work.CooperativeYield is { } cooperativeYield
                ? await DefinitionJson.CanonicalizeAsync(parsed.RootElement, cooperativeYield,
                    keyPolicy: CompositionKeyPolicy.Exact, cancellationToken: cancellationToken)
                : DefinitionJson.Canonicalize(parsed.RootElement, keyPolicy: CompositionKeyPolicy.Exact);
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
