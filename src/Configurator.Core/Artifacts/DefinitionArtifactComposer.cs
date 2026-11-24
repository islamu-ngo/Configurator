namespace Configurator.Core.Artifacts;

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

public static class DefinitionArtifactComposer
{
    private static readonly ActivitySource PreparationDiagnostics = new("Configurator.Core.Preparation");

    /// <summary>
    /// Emits a synchronous point observation through ActivityStarted, with only admitted metadata.
    /// The activity's Parent is the caller's ambient activity; disposal restores it before preparation continues.
    /// Activity duration measures notification overhead, not the named preparation operation.
    /// </summary>
    internal static void ObservePreparation(string name, bool? draft = null, string? artifactId = null)
    {
        using Activity? activity = PreparationDiagnostics.CreateActivity(name, ActivityKind.Internal);
        if (activity is null) return;
        if (draft is { } draftMode) activity.SetTag("draft", draftMode);
        if (artifactId is not null) activity.SetTag("artifact.id", artifactId);
        activity.Start();
    }

    public static DefinitionArtifactResult Compose(
        Definition definition, string artifactId, AnswerSnapshot answers,
        CancellationToken cancellationToken = default)
        => ComposeCore(definition, artifactId, answers, false, [], cancellationToken);

    internal static DefinitionArtifactResult ComposeCore(Definition definition, string artifactId,
        AnswerSnapshot answers, bool draft, List<DefinitionDraftCompletion> completions,
        CancellationToken cancellationToken = default)
        => new ValidatedComposition(definition, answers, draft, cancellationToken).Compose(artifactId, completions);

    /// <summary>Owns validation for exactly one definition, answer snapshot and mode; callers cannot supply validation.</summary>
    internal sealed class ValidatedComposition
    {
        private readonly Definition _definition;
        private readonly long _inputRevision;
        private readonly bool _draft;
        private readonly CancellationToken _cancellationToken;
        private readonly DefinitionValidationResult _validation;
        private readonly CooperativeYieldGate? _yieldGate;

        internal ValidatedComposition(Definition definition, AnswerSnapshot answers, bool draft,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(answers);
            _definition = definition;
            _inputRevision = answers.InputRevision;
            _draft = draft;
            _cancellationToken = cancellationToken;
            _validation = draft ? DefinitionValidator.ValidateDraft(definition, answers, cancellationToken)
                : DefinitionValidator.Validate(definition, answers, cancellationToken);
        }

        internal ValidatedComposition(Definition definition, DefinitionEvaluationInput answers,
            CancellationToken cancellationToken)
        {
            _definition = definition;
            _inputRevision = answers.InputRevision;
            _cancellationToken = cancellationToken;
            _validation = DefinitionValidator.Validate(definition, answers, cancellationToken);
        }

        private ValidatedComposition(Definition definition, long inputRevision, bool draft,
            DefinitionValidationResult validation, CooperativeYieldGate? yieldGate, CancellationToken cancellationToken)
        {
            _definition = definition;
            _inputRevision = inputRevision;
            _draft = draft;
            _validation = validation;
            _yieldGate = yieldGate;
            _cancellationToken = cancellationToken;
        }

        internal static async ValueTask<ValidatedComposition> CreateAsync(Definition definition, AnswerSnapshot answers,
            bool draft, CooperativeYieldGate? yieldGate, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(answers);
            cancellationToken.ThrowIfCancellationRequested();
            DefinitionValidationResult validation = await DefinitionValidator.ValidateCooperativeAsync(
                definition, answers, draft, yieldGate, cancellationToken);
            return new ValidatedComposition(definition, answers.InputRevision, draft, validation, yieldGate, cancellationToken);
        }

        internal DefinitionValidationResult Validation => _validation;

        internal DefinitionArtifactResult Compose(string artifactId, List<DefinitionDraftCompletion> completions)
            => DefinitionImportWork.CompleteSynchronously(
                ComposeValidatedAsync(_definition, artifactId, _inputRevision, _draft, completions, _validation, null, _cancellationToken));

        internal ValueTask<DefinitionArtifactResult> ComposeAsync(string artifactId, List<DefinitionDraftCompletion> completions)
            => ComposeValidatedAsync(_definition, artifactId, _inputRevision, _draft, completions, _validation, _yieldGate, _cancellationToken);

        internal bool IsEligible(string artifactId) => _validation.ConfigurationContext is not { } context
            || context.EligibleArtifactIds.Contains(artifactId, StringComparer.Ordinal);

        internal IReadOnlyList<DefinitionDiagnostic> ValidationDiagnostics => _validation.Diagnostics;
    }

    private static async ValueTask<DefinitionArtifactResult> ComposeValidatedAsync(Definition definition, string artifactId,
        long inputRevision, bool draft, List<DefinitionDraftCompletion> completions,
        DefinitionValidationResult validation, CooperativeYieldGate? yieldGate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        ArtifactDefinition? artifact = definition.Artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null) return new(null, [new("artifact-unknown", "")]);
        if (!validation.IsReady) return new(null, validation.Diagnostics);
        if (validation.ConfigurationContext is { } context && !context.EligibleArtifactIds.Contains(artifactId, StringComparer.Ordinal))
            return new(null, [new("artifact-context", "")]);
        Projection? projection = null;
        try
        {
            projection = new Projection(definition, artifact, draft, completions, validation,
                CompositionLimits.Default, yieldGate, cancellationToken);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("ProjectionStarted");
#endif
            using var buffer = new ZeroingByteBufferWriter();
            bool include;
            using (var writer = new Utf8JsonWriter(buffer))
            {
                include = await projection.ProjectAsync(artifact.Data.GetProperty("projection"),
                    null, null, null, "", "", 1, writer, pendingPropertyName: null);
                if (!include) return new(null, [new("artifact-omitted", "")]);
                writer.Flush();
            }
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("ProjectionCompleted");
#endif
            cancellationToken.ThrowIfCancellationRequested();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("SerializationStarted");
#endif
            if (buffer.WrittenCount > CompositionLimits.DefaultAggregateSourceBytes)
                throw new DefinitionException("artifact-limit");

            if (artifact.Format == DefinitionArtifactFormat.Json)
            {
                cancellationToken.ThrowIfCancellationRequested();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                ObservePreparation("SerializationCompleted");
#endif
                ObservePreparation("ArtifactComposed", artifactId: artifact.Id);
                return new(new(definition, artifact, inputRevision, buffer.WrittenSpan), []);
            }

            using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
            byte[]? bytes = null;
            try
            {
                bytes = artifact.Format switch
                {
                    DefinitionArtifactFormat.Yaml => DefinitionArtifactSerializer.WriteYaml(document.RootElement),
                    DefinitionArtifactFormat.Dotenv => DefinitionArtifactSerializer.WriteDotenv(document.RootElement,
                        artifact.EnvironmentDialect!.Value, draft),
                    DefinitionArtifactFormat.Markdown => DefinitionArtifactSerializer.WriteMarkdown(document.RootElement),
                    _ => throw new DefinitionException("format-invalid")
                };
                if (draft && completions.Count > 0 && artifact.Format == DefinitionArtifactFormat.Dotenv)
                {
                    byte[] original = bytes;
                    bytes = Encoding.UTF8.GetBytes("# Draft: fill deferred secret keys before use.\n" + Encoding.UTF8.GetString(original));
                    CryptographicOperations.ZeroMemory(original);
                }
                if (bytes.Length > CompositionLimits.DefaultAggregateSourceBytes)
                    throw new DefinitionException("artifact-limit");
                cancellationToken.ThrowIfCancellationRequested();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                ObservePreparation("SerializationCompleted");
#endif
                ObservePreparation("ArtifactComposed", artifactId: artifact.Id);
                return new(new(definition, artifact, inputRevision, bytes), []);
            }
            finally
            {
                if (bytes is not null)
                    CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (DefinitionException exception) { return new(null, [new(exception.Code, projection?.UnresolvedField ?? "")]); }
        catch (CompositionException) { return new(null, [new("artifact-limit", "")]); }
    }

    /// <summary>One projection streams directly into canonical UTF-8 JSON bytes with bounded yielding and zero intermediate JsonNode allocations.</summary>
    private sealed class Projection
    {
        private readonly Definition _definition;
        private readonly ArtifactDefinition _artifact;
        private readonly bool _draft;
        private readonly List<DefinitionDraftCompletion> _completions;
        private readonly DefinitionValidationResult _validation;
        private readonly CompositionLimits _limits;
        private readonly CooperativeYieldGate? _yieldGate;
        private readonly CancellationToken _cancellationToken;
        private readonly CompositionBudget _budget;
        private readonly IReadOnlySet<string> _unknown;
        private readonly HashSet<string> _activeRoots;
        private int _operations;

        internal Projection(Definition definition, ArtifactDefinition artifact, bool draft,
            List<DefinitionDraftCompletion> completions, DefinitionValidationResult validation,
            CompositionLimits limits, CooperativeYieldGate? yieldGate, CancellationToken cancellationToken)
        {
            _definition = definition;
            _artifact = artifact;
            _draft = draft;
            _completions = completions;
            _validation = validation;
            _limits = limits;
            _yieldGate = yieldGate;
            _cancellationToken = cancellationToken;
            _budget = new CompositionBudget(limits);
            _unknown = validation.UndecidedRoots;
            _activeRoots = validation.Fields.Where(field => field.IsActive).Select(field => field.Definition.Name)
                .Concat(validation.Values.Keys).ToHashSet(StringComparer.Ordinal);
        }

        internal string UnresolvedField { get; private set; } = "";

        internal async ValueTask<bool> ProjectAsync(JsonElement projection, JsonElement? item, int? index,
            DefinitionNode? itemSchema, string itemPath, string outputPath, int depth,
            Utf8JsonWriter writer, string? pendingPropertyName)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (++_operations > _limits.ParserEvents)
                throw new DefinitionException("projection-limit");
            if (_yieldGate is not null) await _yieldGate.StepAsync();
            if (projection.TryGetProperty("when", out JsonElement condition))
            {
                bool? matches = DefinitionValidator.EvaluateCondition(condition, _validation.Values, _draft ? _unknown : null, item,
                    deferredLocations: _draft ? _validation.DeferredLocations : null,
                    unresolvedLocations: _draft ? _validation.UndecidedLocations : null,
                    itemLocation: item is null ? null : DefinitionPath.Parse(itemPath));
                if (matches is null)
                {
                    string? unresolved = DefinitionValidator.References(condition).FirstOrDefault(_unknown.Contains);
                    if (unresolved is null && item is not null)
                    {
                        DefinitionPath location = DefinitionPath.Parse(itemPath);
                        unresolved = location.Segments.Count > 0 ? location.Segments[0] : null;
                    }
                    UnresolvedField = unresolved is null ? "" : _validation.Fields.FirstOrDefault(field => field.Definition.Name == unresolved)?.Id ?? "";
                    throw new DefinitionException("draft-dependency-unresolved");
                }
                if (matches == false) return false;
            }

            switch (projection.GetProperty("op").GetString())
            {
                case "omit":
                    return false;
                case "literal":
                    WritePendingPropertyName(writer, pendingPropertyName);
                    await WriteCanonicalElementAsync(writer, projection.GetProperty("value"), depth);
                    return true;
                case "copy":
                    {
                        JsonElement reference = projection.GetProperty("reference");
                        (DefinitionNode Schema, string Path, bool SecretAncestor) selected = Select(reference, itemSchema, itemPath);
                        CheckUnknown(selected.Path);
                        bool present = DefinitionValidator.TryResolveReference(reference, _validation.Values, item, out JsonElement value);
                        if (_draft)
                        {
                            return await CopyFieldAsync(selected.Schema, present ? value : null, selected.Path, outputPath,
                                depth, selected.SecretAncestor, writer, pendingPropertyName, _completions);
                        }
                        if (!present) return false;
                        WritePendingPropertyName(writer, pendingPropertyName);
                        await WriteCanonicalElementAsync(writer, value, depth);
                        return true;
                    }
                case "index":
                    WritePendingPropertyName(writer, pendingPropertyName);
                    _budget.Node(depth);
                    writer.WriteNumberValue(index!.Value);
                    return true;
                case "object":
                    {
                        WritePendingPropertyName(writer, pendingPropertyName);
                        _budget.Node(depth);
                        JsonElement members = projection.GetProperty("members");
                        writer.WriteStartObject();
                        int includedMembers = 0;
                        if (DefinitionJson.IsStrictlyOrdinalSorted(members, out _))
                        {
                            foreach (JsonProperty member in members.EnumerateObject())
                            {
                                bool included = await ProjectAsync(member.Value, item, index, itemSchema, itemPath,
                                    outputPath + "/" + Pointer(member.Name), depth + 1, writer, member.Name);
                                if (included && ++includedMembers > CompositionLimits.DefaultMappingEntries)
                                    throw new DefinitionException("document-limit");
                            }
                        }
                        else
                        {
                            JsonProperty[] sorted = members.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                            string? previous = null;
                            foreach (JsonProperty member in sorted)
                            {
                                if (previous is not null && string.Equals(previous, member.Name, StringComparison.Ordinal))
                                    throw new DefinitionException("duplicate-member");
                                previous = member.Name;
                                bool included = await ProjectAsync(member.Value, item, index, itemSchema, itemPath,
                                    outputPath + "/" + Pointer(member.Name), depth + 1, writer, member.Name);
                                if (included && ++includedMembers > CompositionLimits.DefaultMappingEntries)
                                    throw new DefinitionException("document-limit");
                            }
                        }
                        writer.WriteEndObject();
                        return true;
                    }
                case "list":
                    {
                        WritePendingPropertyName(writer, pendingPropertyName);
                        _budget.Node(depth);
                        writer.WriteStartArray();
                        int count = 0;
                        foreach (JsonElement entry in projection.GetProperty("items").EnumerateArray())
                        {
                            bool included = await ProjectAsync(entry, item, index, itemSchema, itemPath,
                                outputPath + "/" + count.ToString(CultureInfo.InvariantCulture), depth + 1, writer, null);
                            if (included && ++count > CompositionLimits.DefaultSequenceEntries)
                                throw new DefinitionException("document-limit");
                        }
                        writer.WriteEndArray();
                        return true;
                    }
                case "repeat":
                    {
                        JsonElement reference = projection.GetProperty("reference");
                        (DefinitionNode Schema, string Path, bool SecretAncestor) selected = Select(reference, itemSchema, itemPath);
                        CheckUnknown(selected.Path);
                        if (!DefinitionValidator.TryResolveReference(reference, _validation.Values, item, out JsonElement source))
                            return false;
                        if (source.ValueKind != JsonValueKind.Array) throw new DefinitionException("repeat-source");
                        WritePendingPropertyName(writer, pendingPropertyName);
                        _budget.Node(depth);
                        writer.WriteStartArray();
                        int position = 0;
                        int count = 0;
                        JsonElement itemProjection = projection.GetProperty("item");
                        foreach (JsonElement entry in source.EnumerateArray())
                        {
                            int itemIndex = position++;
                            bool included = await ProjectAsync(itemProjection, entry, itemIndex, selected.Schema.Items,
                                selected.Path + "/" + itemIndex.ToString(CultureInfo.InvariantCulture),
                                outputPath + "/" + count.ToString(CultureInfo.InvariantCulture), depth + 1, writer, null);
                            if (included && ++count > CompositionLimits.DefaultSequenceEntries)
                                throw new DefinitionException("document-limit");
                        }
                        writer.WriteEndArray();
                        return true;
                    }
                default:
                    throw new DefinitionException("projection-operation");
            }
        }

        private void WritePendingPropertyName(Utf8JsonWriter writer, string? propertyName)
        {
            if (propertyName is null) return;
            _budget.Scalar(propertyName);
            DefinitionJson.ValidateUtf8Scalar(propertyName);
            writer.WritePropertyName(propertyName);
        }

        private async ValueTask WriteCanonicalElementAsync(Utf8JsonWriter writer, JsonElement value, int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_yieldGate is not null) await _yieldGate.StepAsync();
            _budget.Node(depth);
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    if (DefinitionJson.IsStrictlyOrdinalSorted(value, out int propertyCount))
                    {
                        if (propertyCount > CompositionLimits.DefaultMappingEntries)
                            throw new DefinitionException("document-limit");
                        writer.WriteStartObject();
                        foreach (JsonProperty property in value.EnumerateObject())
                        {
                            _budget.Scalar(property.Name);
                            DefinitionJson.ValidateUtf8Scalar(property.Name);
                            writer.WritePropertyName(property.Name);
                            await WriteCanonicalElementAsync(writer, property.Value, depth + 1);
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        JsonProperty[] sorted = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                        if (sorted.Length > CompositionLimits.DefaultMappingEntries)
                            throw new DefinitionException("document-limit");
                        writer.WriteStartObject();
                        string? previous = null;
                        foreach (JsonProperty property in sorted)
                        {
                            if (previous is not null && string.Equals(previous, property.Name, StringComparison.Ordinal))
                                throw new DefinitionException("duplicate-member");
                            previous = property.Name;
                            _budget.Scalar(property.Name);
                            DefinitionJson.ValidateUtf8Scalar(property.Name);
                            writer.WritePropertyName(property.Name);
                            await WriteCanonicalElementAsync(writer, property.Value, depth + 1);
                        }
                        writer.WriteEndObject();
                    }
                    break;
                case JsonValueKind.Array:
                    if (value.GetArrayLength() > CompositionLimits.DefaultSequenceEntries)
                        throw new DefinitionException("document-limit");
                    writer.WriteStartArray();
                    foreach (JsonElement child in value.EnumerateArray())
                        await WriteCanonicalElementAsync(writer, child, depth + 1);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    string text = value.GetString()!;
                    _budget.Scalar(text);
                    DefinitionJson.ValidateUtf8Scalar(text);
                    writer.WriteStringValue(text);
                    break;
                case JsonValueKind.Number:
                    string raw = value.GetRawText();
                    _budget.Scalar(raw);
                    writer.WriteRawValue(DefinitionJson.CanonicalizeNumber(value, CompositionKeyPolicy.Exact));
                    break;
                case JsonValueKind.True:
                    _budget.Scalar("true");
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    _budget.Scalar("false");
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    _budget.Scalar("null");
                    writer.WriteNullValue();
                    break;
                default:
                    throw new DefinitionException("value-invalid");
            }
        }

        private (DefinitionNode Schema, string Path, bool SecretAncestor) Select(JsonElement reference,
            DefinitionNode? itemSchema, string itemPath)
        {
            bool relative = reference.GetProperty("scope").GetString() == "item";
            DefinitionNode schema = relative ? itemSchema! : _definition.Configuration;
            bool secret = false;
            if (relative)
            {
                DefinitionNode ancestor = _definition.Configuration;
                foreach (string segment in DefinitionPath.Parse(itemPath).Segments)
                {
                    secret |= FieldDefinition.SensitivityOf(ancestor) == FieldSensitivity.Secret;
                    ancestor = ancestor.Type == DefinitionNodeType.Mapping ? ancestor.Properties[segment] : ancestor.Items!;
                }
            }
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            foreach (string segment in path.Segments)
            {
                secret |= FieldDefinition.SensitivityOf(schema) == FieldSensitivity.Secret;
                schema = schema.Type == DefinitionNodeType.Mapping ? schema.Properties[segment] : schema.Items!;
            }
            return (schema, (relative ? itemPath : "") + path.ToPointer(), secret);
        }

        private void CheckUnknown(string sourcePath)
        {
            if (!_draft) return;
            DefinitionPath path = DefinitionPath.Parse(sourcePath);
            string? unresolved = path.Segments.Count == 0 ? _validation.UnresolvedFields.FirstOrDefault()
                : _validation.UnresolvedFields.Contains(path.Segments[0]) ? path.Segments[0] : null;
            // A nested undecided value blocks only copies that read it or contain it.
            unresolved ??= _validation.UnresolvedLocations.FirstOrDefault(location =>
                DefinitionImportProjection.IsPrefix(location, path) || DefinitionImportProjection.IsPrefix(path, location))
                ?.Segments[0];
            if (unresolved is null) return;
            UnresolvedField = _validation.Fields.FirstOrDefault(field => field.Definition.Name == unresolved)?.Id ?? "";
            throw new DefinitionException("draft-dependency-unresolved");
        }

        private async ValueTask<bool> CopyFieldAsync(DefinitionNode schema, JsonElement? value, string fieldPath,
            string outputPath, int depth, bool secretAncestor, Utf8JsonWriter writer, string? pendingPropertyName,
            List<DefinitionDraftCompletion> completions, string? completionFieldPath = null,
            string? completionOutputPath = null)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_yieldGate is not null) await _yieldGate.StepAsync();
            DefinitionPath location = DefinitionPath.Parse(fieldPath);
            if (_validation.ConfigurationContext is { } context && !context.Includes(location.Segments))
                return false;
            if (location.Segments.Count > 0 && !_activeRoots.Contains(location.Segments[0]))
                return false;
            bool secret = secretAncestor || FieldDefinition.SensitivityOf(schema) == FieldSensitivity.Secret;
            completionFieldPath ??= fieldPath;
            completionOutputPath ??= outputPath;
            if (value is { ValueKind: not JsonValueKind.Null } variantValue && schema.Variants.Count > 0)
            {
                DefinitionNode branch = DefinitionValidator.SelectVariant(schema, variantValue)
                    ?? throw new DefinitionException("value-variant");
                return await CopyFieldAsync(branch, value, fieldPath, outputPath, depth, secret,
                    writer, pendingPropertyName, completions, completionFieldPath, completionOutputPath);
            }
            if (secret && !value.HasValue && _validation.DeferredLocations.Contains(location))
            {
                WritePendingPropertyName(writer, pendingPropertyName);
                _budget.Node(depth);
                completions.Add(new(_artifact.Id, completionFieldPath, completionOutputPath));
                if (schema.Type == DefinitionNodeType.Text || _artifact.Format is DefinitionArtifactFormat.Dotenv or DefinitionArtifactFormat.Markdown)
                    writer.WriteStringValue("");
                else
                    writer.WriteNullValue();
                return true;
            }
            if (!value.HasValue) return false;
            if (value.Value.ValueKind == JsonValueKind.Null)
            {
                WritePendingPropertyName(writer, pendingPropertyName);
                await WriteCanonicalElementAsync(writer, value.Value, depth);
                return true;
            }
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                WritePendingPropertyName(writer, pendingPropertyName);
                _budget.Node(depth);
                writer.WriteStartObject();
                int writtenEntries = 0;
                if (schema.AdditionalProperties is not { } map)
                {
                    foreach ((string name, DefinitionNode property) in schema.Properties)
                    {
                        bool exists = value.Value.TryGetProperty(name, out JsonElement childValue);
                        string escaped = "/" + Pointer(name);
                        bool included = await CopyFieldAsync(property, exists ? childValue : null,
                            fieldPath + escaped, outputPath + escaped, depth + 1, secret,
                            writer, name, completions, completionFieldPath + escaped, completionOutputPath + escaped);
                        if (included && ++writtenEntries > CompositionLimits.DefaultMappingEntries)
                            throw new DefinitionException("document-limit");
                    }
                }
                else if (schema.Properties.Count == 0 && DefinitionJson.IsStrictlyOrdinalSorted(value.Value, out _))
                {
                    int entry = 0;
                    foreach (JsonProperty property in value.Value.EnumerateObject())
                    {
                        string escaped = "/" + Pointer(property.Name);
                        string ordinal = "/@entry/" + entry.ToString(CultureInfo.InvariantCulture);
                        bool included = await CopyFieldAsync(map, property.Value,
                            fieldPath + escaped, outputPath + escaped, depth + 1, secret,
                            writer, property.Name, completions, completionFieldPath + ordinal, completionOutputPath + ordinal);
                        if (!included) _budget.Scalar(property.Name);
                        else if (++writtenEntries > CompositionLimits.DefaultMappingEntries)
                            throw new DefinitionException("document-limit");
                        entry++;
                    }
                }
                else
                {
                    var mapEntries = new List<(string Name, JsonElement Value, int EntryIndex)>();
                    int entry = 0;
                    bool mapSorted = true;
                    string? previousMap = null;
                    foreach (JsonProperty property in value.Value.EnumerateObject())
                    {
                        if (schema.Properties.ContainsKey(property.Name)) continue;
                        if (previousMap is not null && string.CompareOrdinal(previousMap, property.Name) >= 0)
                            mapSorted = false;
                        previousMap = property.Name;
                        mapEntries.Add((property.Name, property.Value, entry++));
                    }
                    if (!mapSorted)
                        mapEntries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

                    List<DefinitionDraftCompletion>? mapCompletions = schema.Properties.Count > 0 ? [] : null;
                    using IEnumerator<KeyValuePair<string, DefinitionNode>> declaredEnumerator = schema.Properties.GetEnumerator();
                    bool hasDeclared = declaredEnumerator.MoveNext();
                    int mapIndex = 0;
                    while (hasDeclared || mapIndex < mapEntries.Count)
                    {
                        bool takeDeclared = hasDeclared && (mapIndex >= mapEntries.Count
                            || StringComparer.Ordinal.Compare(declaredEnumerator.Current.Key, mapEntries[mapIndex].Name) < 0);
                        if (takeDeclared)
                        {
                            string name = declaredEnumerator.Current.Key;
                            DefinitionNode property = declaredEnumerator.Current.Value;
                            hasDeclared = declaredEnumerator.MoveNext();
                            bool exists = value.Value.TryGetProperty(name, out JsonElement childValue);
                            string escaped = "/" + Pointer(name);
                            bool included = await CopyFieldAsync(property, exists ? childValue : null,
                                fieldPath + escaped, outputPath + escaped, depth + 1, secret,
                                writer, name, completions, completionFieldPath + escaped, completionOutputPath + escaped);
                            if (included && ++writtenEntries > CompositionLimits.DefaultMappingEntries)
                                throw new DefinitionException("document-limit");
                        }
                        else
                        {
                            (string name, JsonElement entryValue, int entryOrdinal) = mapEntries[mapIndex++];
                            string escaped = "/" + Pointer(name);
                            string ordinal = "/@entry/" + entryOrdinal.ToString(CultureInfo.InvariantCulture);
                            bool included = await CopyFieldAsync(map, entryValue,
                                fieldPath + escaped, outputPath + escaped, depth + 1, secret,
                                writer, name, mapCompletions ?? completions, completionFieldPath + ordinal, completionOutputPath + ordinal);
                            if (!included) _budget.Scalar(name);
                            else if (++writtenEntries > CompositionLimits.DefaultMappingEntries)
                                throw new DefinitionException("document-limit");
                        }
                    }
                    if (mapCompletions is { Count: > 0 })
                        completions.AddRange(mapCompletions);
                }
                writer.WriteEndObject();
                return true;
            }
            if (schema.Type == DefinitionNodeType.Sequence)
            {
                WritePendingPropertyName(writer, pendingPropertyName);
                _budget.Node(depth);
                writer.WriteStartArray();
                int count = 0;
                foreach (JsonElement row in value.Value.EnumerateArray())
                {
                    string ordinal = "/" + count.ToString(CultureInfo.InvariantCulture);
                    bool included = await CopyFieldAsync(schema.Items!, row, fieldPath + ordinal, outputPath + ordinal,
                        depth + 1, secret, writer, null, completions, completionFieldPath + ordinal, completionOutputPath + ordinal);
                    if (!included) writer.WriteNullValue();
                    if (++count > CompositionLimits.DefaultSequenceEntries)
                        throw new DefinitionException("document-limit");
                }
                writer.WriteEndArray();
                return true;
            }
            WritePendingPropertyName(writer, pendingPropertyName);
            await WriteCanonicalElementAsync(writer, value.Value, depth);
            return true;
        }
    }

    private static string Pointer(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
