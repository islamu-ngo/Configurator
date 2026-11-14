namespace Configurator.Core.Artifacts;

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        private readonly AnswerSnapshot _answers;
        private readonly bool _draft;
        private readonly CancellationToken _cancellationToken;
        private readonly DefinitionValidationResult _validation;

        internal ValidatedComposition(Definition definition, AnswerSnapshot answers, bool draft,
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

        internal bool IsEligible(string artifactId) => _validation.ConfigurationContext is not { } context
            || context.EligibleArtifactIds.Contains(artifactId, StringComparer.Ordinal);

        internal IReadOnlyList<DefinitionDiagnostic> ValidationDiagnostics => _validation.Diagnostics;
    }

    private static DefinitionArtifactResult ComposeValidated(Definition definition, string artifactId,
        AnswerSnapshot answers, bool draft, List<DefinitionDraftCompletion> completions,
        DefinitionValidationResult validation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        ArtifactDefinition? artifact = definition.Artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null) return new(null, [new("artifact-unknown", "")]);
        if (!validation.IsReady) return new(null, validation.Diagnostics);
        if (validation.ConfigurationContext is { } context && !context.EligibleArtifactIds.Contains(artifactId, StringComparer.Ordinal))
            return new(null, [new("artifact-context", "")]);
        string unresolvedField = "";
        try
        {
            var budget = new CompositionBudget(CompositionLimits.Default);
            int operations = 0;
            IReadOnlySet<string> unknown = validation.UndecidedRoots;
            var activeRoots = validation.Fields.Where(field => field.IsActive).Select(field => field.Definition.Name)
                .Concat(validation.Values.Keys).ToHashSet(StringComparer.Ordinal);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("ProjectionStarted");
#endif
            (bool include, JsonNode? output) = Project(artifact.Data.GetProperty("projection"), null, null, null, "", "", 1);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("ProjectionCompleted");
#endif
            if (!include) return new(null, [new("artifact-omitted", "")]);
            cancellationToken.ThrowIfCancellationRequested();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            ObservePreparation("SerializationStarted");
#endif
            using JsonDocument document = JsonDocument.Parse(output?.ToJsonString() ?? "null");
            byte[] canonical = DefinitionJson.Canonicalize(document.RootElement, keyPolicy: CompositionKeyPolicy.Exact);
            byte[]? bytes = null;
            try
            {
                bytes = artifact.Format switch
                {
                    DefinitionArtifactFormat.Json => canonical,
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
                if (++operations > CompositionLimits.DefaultParserEvents)
                    throw new DefinitionException("projection-limit");
                if (projection.TryGetProperty("when", out var condition))
                {
                    bool? matches = DefinitionValidator.EvaluateCondition(condition, validation.Values, draft ? unknown : null, item,
                        deferredLocations: draft ? validation.DeferredLocations : null,
                        unresolvedLocations: draft ? validation.UndecidedLocations : null,
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
                        throw new DefinitionException("draft-dependency-unresolved");
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
                            if (source.ValueKind != JsonValueKind.Array) throw new DefinitionException("repeat-source");
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
                    default: throw new DefinitionException("projection-operation");
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

            void CheckUnknown(string sourcePath)
            {
                if (!draft) return;
                DefinitionPath path = DefinitionPath.Parse(sourcePath);
                string? unresolved = path.Segments.Count == 0 ? validation.UnresolvedFields.FirstOrDefault()
                    : validation.UnresolvedFields.Contains(path.Segments[0]) ? path.Segments[0] : null;
                // A nested undecided value blocks only copies that read it or contain it.
                unresolved ??= validation.UnresolvedLocations.FirstOrDefault(location =>
                    DefinitionImportProjection.IsPrefix(location, path) || DefinitionImportProjection.IsPrefix(path, location))
                    ?.Segments[0];
                if (unresolved is null) return;
                unresolvedField = validation.Fields.FirstOrDefault(field => field.Definition.Name == unresolved)?.Id ?? "";
                throw new DefinitionException("draft-dependency-unresolved");
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
                if (validation.ConfigurationContext is { } context && !context.Includes(location.Segments))
                    return (false, null);
                if (location.Segments.Count > 0 && !activeRoots.Contains(location.Segments[0]))
                    return (false, null);
                bool secret = secretAncestor || FieldDefinition.SensitivityOf(schema) == FieldSensitivity.Secret;
                completionFieldPath ??= fieldPath;
                completionOutputPath ??= outputPath;
                if (value is { ValueKind: not JsonValueKind.Null } variantValue && schema.Variants.Count > 0)
                {
                    DefinitionNode branch = DefinitionValidator.SelectVariant(schema, variantValue)
                        ?? throw new DefinitionException("value-variant");
                    return CopyField(branch, value, fieldPath, outputPath, depth, secret,
                        completionFieldPath, completionOutputPath);
                }
                if (secret && !value.HasValue && validation.DeferredLocations.Contains(location))
                {
                    budget.Node(depth);
                    completions.Add(new(artifact.Id, completionFieldPath, completionOutputPath));
                    return (true, schema.Type == DefinitionNodeType.Text || artifact.Format is DefinitionArtifactFormat.Dotenv or DefinitionArtifactFormat.Markdown
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
        catch (DefinitionException exception) { return new(null, [new(exception.Code, unresolvedField)]); }
        catch (CompositionException) { return new(null, [new("artifact-limit", "")]); }
    }

    private static string Pointer(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

}
