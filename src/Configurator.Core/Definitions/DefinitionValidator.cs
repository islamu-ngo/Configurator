namespace Configurator.Core.Definitions;

using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;

public sealed class SetupFieldState
{
    internal SetupFieldState(SetupFieldDefinition field, bool active, bool required, bool provided, bool error)
    {
        Definition = field;
        IsActive = active;
        IsRequired = required;
        IsProvided = provided;
        HasError = error;
    }

    public SetupFieldDefinition Definition { get; }
    public string Id => Definition.Id;
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool HasError { get; }
}

public sealed class DefinitionValidationResult
{
    internal DefinitionValidationResult(IEnumerable<SetupFieldState> fields,
        IEnumerable<DefinitionDiagnostic> diagnostics, Dictionary<string, JsonElement> values,
        IReadOnlySet<string>? deferred = null, IReadOnlySet<string>? unresolved = null,
        IEnumerable<DefinitionNodeState>? nodes = null, IReadOnlySet<DefinitionPath>? deferredLocations = null)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.OrderBy(item => item.FieldId, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Values = new ReadOnlyDictionary<string, JsonElement>(values);
        DeferredFields = deferred ?? new HashSet<string>(StringComparer.Ordinal);
        UnresolvedFields = unresolved ?? new HashSet<string>(StringComparer.Ordinal);
        Nodes = Array.AsReadOnly(nodes?.ToArray() ?? []);
        DeferredLocations = deferredLocations ?? new HashSet<DefinitionPath>();
    }

    public IReadOnlyList<SetupFieldState> Fields { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DefinitionNodeState> Nodes { get; }
    public bool IsReady => Diagnostics.Count == 0;
    internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    internal IReadOnlySet<string> DeferredFields { get; }
    internal IReadOnlySet<string> UnresolvedFields { get; }
    internal IReadOnlySet<DefinitionPath> DeferredLocations { get; }
    /// <summary>Explicitly reads a resolved private value; serialized validation facts remain value-free.</summary>
    public bool TryGetEffectiveValue(DefinitionPath location, out JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(location);
        JsonElement reference = JsonSerializer.SerializeToElement(new { scope = "root", path = location.ToPointer() });
        if (DefinitionValidator.TryResolveReference(reference, Values, null, out JsonElement resolved))
        {
            value = resolved.Clone();
            return true;
        }
        value = default;
        return false;
    }
    public override string ToString() => $"{nameof(DefinitionValidationResult)}:Ready={IsReady}:Diagnostics={Diagnostics.Count}";
}

/// <summary>Value-free validation facts for one literal editor path, including required-work obligations.</summary>
public sealed class DefinitionNodeState
{
    internal DefinitionNodeState(string fieldId, IReadOnlyList<string> relativePath,
        bool active, bool required, bool provided, bool valid, bool obligation, bool obligationReady, bool missing,
        SetupFieldSensitivity sensitivity, DefinitionCorrectionLocation location)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
        IsActive = active;
        IsRequired = active && required;
        IsProvided = provided;
        IsReady = valid;
        HasError = active && !valid;
        IsObligation = active && obligation;
        IsObligationReady = active && obligationReady;
        IsMissing = missing;
        Sensitivity = sensitivity;
        Location = location;
    }

    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool IsReady { get; }
    public bool HasError { get; }
    public bool IsObligation { get; }
    public bool IsObligationReady { get; }
    public bool IsMissing { get; }
    public SetupFieldSensitivity Sensitivity { get; }
    public DefinitionCorrectionLocation Location { get; }
}

public static class DefinitionValidator
{
    public static DefinitionValidationResult Validate(SetupDefinition definition, SetupAnswerSnapshot answers,
        CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(ValidateCoreAsync(definition, answers, false, cancellationToken));

    /// <summary>Defers missing secret leaves only. Supplied invalid values remain blocking.</summary>
    public static DefinitionValidationResult ValidateDraft(SetupDefinition definition, SetupAnswerSnapshot answers,
        CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(ValidateCoreAsync(definition, answers, true, cancellationToken));

    internal static ValueTask<DefinitionValidationResult> ValidateSuppliedAsync(SetupDefinition definition,
        DefinitionImportEvidence evidence, DefinitionImportWork work, Func<DefinitionDiagnostic, ValueTask> diagnosticObserved,
        CancellationToken cancellationToken) =>
        ValidateCoreAsync(definition, evidence.Answers, false, cancellationToken, evidence, work, diagnosticObserved);

    internal static ValueTask<DefinitionValidationResult> ValidateImportAsync(SetupDefinition definition, SetupAnswerSnapshot answers,
        DefinitionImportWork work, CancellationToken cancellationToken) =>
        ValidateCoreAsync(definition, answers, false, cancellationToken, work: work);

    private static async ValueTask<DefinitionValidationResult> ValidateCoreAsync(SetupDefinition definition, SetupAnswerSnapshot answers,
        bool draft, CancellationToken cancellationToken, DefinitionImportEvidence? observations = null,
        DefinitionImportWork? work = null, Func<DefinitionDiagnostic, ValueTask>? diagnosticObserved = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        DefinitionNode rootSchema = definition.Configuration;
        if (rootSchema.Discriminator is { } discriminator)
        {
            if (!answers.Values.TryGetValue(discriminator, out JsonElement selection) ||
                DiscriminatedBranch(rootSchema, selection) is not { } branch)
            {
                var diagnostic = new DefinitionDiagnostic(observations?.State(DefinitionPath.FromSegments([discriminator])) ==
                    DefinitionImportObservationState.Unobserved ? "value-unobserved" : "value-variant", "");
                if (diagnosticObserved is not null) await diagnosticObserved(diagnostic);
                return new([], [diagnostic], new(StringComparer.Ordinal));
            }
            rootSchema = branch;
        }
        var rootSensitivity = (SetupFieldSensitivity)Math.Max(
            (int)SetupFieldDefinition.SensitivityOf(definition.Configuration), (int)SetupFieldDefinition.SensitivityOf(rootSchema));
        IReadOnlyList<SetupFieldDefinition> rootFields = ReferenceEquals(rootSchema, definition.Configuration) ? definition.Fields
            : Array.AsReadOnly(rootSchema.Properties.Select(property => new SetupFieldDefinition(property.Key, property.Value,
                rootSchema.Required.Contains(property.Key, StringComparer.Ordinal), rootSensitivity)).ToArray());
        var effective = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var diagnostics = new List<DefinitionDiagnostic>();
        var states = new Dictionary<string, SetupFieldState>(StringComparer.Ordinal);
        var nodes = new List<DefinitionNodeState>();
        var deferred = new HashSet<string>(StringComparer.Ordinal);
        var deferredLocations = new HashSet<DefinitionPath>();
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        var invalidControllers = new HashSet<string>(StringComparer.Ordinal);
        var invalidLocations = new HashSet<DefinitionPath>();
        var fields = rootFields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var budget = new CompositionBudget(SetupCompositionLimits.Default);
        var missing = new List<(string Root, string Id, string[] Path, DefinitionLocationSegment[] Correction)>();
        try
        {
            if (rootSchema.AdditionalProperties is null && answers.Values.Keys.Any(name => !fields.ContainsKey(name)))
                await Emit(new("field-unknown", ""));
            foreach (DefinitionPath omission in answers.Omitted)
            {
                DefinitionNode current = rootSchema;
                for (int index = 0; index < omission.Segments.Count; index++)
                {
                    if (current.Type == DefinitionNodeType.Mapping &&
                        current.Properties.TryGetValue(omission.Segments[index], out DefinitionNode? child))
                        current = child;
                    else if (current.Type == DefinitionNodeType.Sequence && omission.TryGetArrayIndex(index, out int row) &&
                        row < SetupCompositionLimits.DefaultSequenceEntries) current = current.Items!;
                    else if (current.Type == DefinitionNodeType.Mapping && current.AdditionalProperties is { } map)
                        current = map;
                    else { await Emit(new("answer-omission", "")); break; }
                }
            }
            foreach (SetupFieldDefinition field in rootFields) await Activate(field);
            if (rootSchema.AdditionalProperties is { } rootMap)
            {
                int entry = 0;
                foreach ((string name, JsonElement supplied) in answers.Values)
                {
                    if (fields.ContainsKey(name)) continue;
                    var resolved = await Resolve(rootMap, JsonNode.Parse(supplied.GetRawText()), true, [name], "",
                        ["@entry", entry.ToString(CultureInfo.InvariantCulture)], true, true, rootSensitivity,
                        correction: [DefinitionLocationSegment.MapEntry(entry)]);
                    if (resolved.Present) effective.Add(name, await ElementAsync(resolved.Value, work));
                    entry++;
                }
            }
            await SettleRequirements();
            bool rootComplete = observations is null || observations.Complete(DefinitionPath.Parse(""));
            if (!rootComplete && (rootSchema.Keywords.TryGetProperty("minProperties", out _) ||
                rootSchema.Keywords.TryGetProperty("maxProperties", out _)))
                await Emit(new("value-partially-observed", ""));
            if (!PropertyCountValid(rootSchema, effective.Count, rootComplete))
                await Emit(new("value-invalid", ""));
            if (DefinitionArtifactComposer.PreparationDiagnostics.IsEnabled("ValidationCompleted"))
                DefinitionArtifactComposer.PreparationDiagnostics.Write("ValidationCompleted", draft);
            return new(rootFields.Select(field => states[field.Name]), diagnostics, effective, deferred, unresolved, nodes,
                deferredLocations);
        }
        catch (SetupCompositionException)
        {
            if (diagnosticObserved is not null) await diagnosticObserved(new("validation-limit", ""));
            return new([], [new("validation-limit", "")], new(StringComparer.Ordinal));
        }
        catch (SetupDefinitionException exception) when (exception.Code == "path-limit")
        {
            if (diagnosticObserved is not null) await diagnosticObserved(new("validation-limit", ""));
            return new([], [new("validation-limit", "")], new(StringComparer.Ordinal));
        }

        async ValueTask Emit(DefinitionDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
            if (diagnosticObserved is not null) await diagnosticObserved(diagnostic);
        }

        async ValueTask Activate(SetupFieldDefinition field)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (work is not null) await work.VisitAsync();
            if (states.ContainsKey(field.Name)) return;
            if (!visiting.Add(field.Name)) throw new SetupDefinitionException("activation-cycle");
            foreach (string dependency in await SchemaReferencesAsync(field.Node, "root", work))
                if (fields.TryGetValue(dependency, out SetupFieldDefinition? controller)) await Activate(controller);
            var unknown = Unknown();
            bool? applies = field.ActiveWhen is { } condition ? await Evaluate(condition, unknown) : true;
            if (applies is null) unresolved.Add(field.Name);
            bool active = applies == true;
            bool supplied = answers.Values.TryGetValue(field.Name, out JsonElement source);
            JsonNode? value = supplied ? JsonNode.Parse(source.GetRawText()) : null;
            int before = diagnostics.Count;
            var resolved = await Resolve(field.Node, value, supplied, [field.Name], field.Id, [], active,
                active && field.Required, rootSensitivity);
            if (active && resolved.Present) effective.Add(field.Name, await ElementAsync(resolved.Value, work));
            bool error = diagnostics.Count != before;
            if (error) invalidControllers.Add(field.Name);
            states.Add(field.Name, new(field, active, active && field.Required, active && resolved.Present, error));
            visiting.Remove(field.Name);
        }

        async ValueTask<(bool Present, JsonNode? Value, bool Valid)> Resolve(DefinitionNode schema,
            JsonNode? value, bool present, string[] location, string fieldId,
            string[] safePath, bool active, bool required, SetupFieldSensitivity inherited,
            JsonObject? row = null, HashSet<string>? rowUnknown = null,
            DefinitionLocationSegment[]? correction = null, DefinitionPath? rowLocation = null)
        {
            correction ??= [];
            cancellationToken.ThrowIfCancellationRequested();
            if (work is not null) await work.ValidationAsync();
            budget.Node(location.Length);
            DefinitionPath observedPath = DefinitionPath.FromSegments(location);
            bool observed = observations is null || observations.State(observedPath) != DefinitionImportObservationState.Unobserved;
            bool complete = observations is null || observations.Complete(observedPath);
            int start = nodes.Count;
            int diagnosticsBefore = diagnostics.Count;
            var sensitivity = (SetupFieldSensitivity)Math.Max((int)inherited, (int)SetupFieldDefinition.SensitivityOf(schema));
            if (!active || answers.Omitted.Contains(DefinitionPath.FromSegments(location))) present = false;
            else if (observations is null && !present && schema.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
                && metadata.TryGetProperty("initialValue", out JsonElement initial))
            {
                value = JsonNode.Parse(initial.GetRawText());
                present = true;
            }
            if (active && present && value is not null && schema.Variants.Count > 0)
            {
                DefinitionNode? branch = SelectVariant(schema, await ElementAsync(value, work));
                if (branch is null)
                {
                    invalidLocations.Add(observedPath);
                    await Emit(new("value-variant", fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
                    nodes.Add(new(fieldId, safePath, true, required, true, false, required, false, false,
                        SetupFieldDefinition.EffectiveSensitivity(schema, inherited),
                        new DefinitionCorrectionLocation(answers.InputRevision, correction)));
                    return (true, value, false);
                }
                var resolvedBranch = await Resolve(branch, value, true, location, fieldId, safePath, true, required, sensitivity,
                    row, rowUnknown, correction, rowLocation);
                if (resolvedBranch.Valid && !await LocalValueValidAsync(schema,
                    await ElementAsync(resolvedBranch.Value, work), complete, work))
                {
                    await Emit(new("value-invalid", fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
                    return (resolvedBranch.Present, resolvedBranch.Value, false);
                }
                return resolvedBranch;
            }
            bool missingSecret = active && !present && draft && sensitivity == SetupFieldSensitivity.Secret
                && schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
            if (missingSecret)
            {
                deferred.Add(location[0]);
                deferredLocations.Add(DefinitionPath.FromSegments(location));
            }
            bool valid = !active || !present;
            if (active && required && !observed)
            {
                await Emit(new("value-unobserved", fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            }
            if (active && !present && required && !missingSecret && observed)
            {
                missing.Add((location[0], fieldId, safePath, correction));
                if (observations is not null && diagnosticObserved is not null)
                    await diagnosticObserved(new("value-required", fieldId,
                        new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            }
            JsonElement resolvedElement = default;
            bool containerPresent = active && present && value is JsonObject;
            if (active && present && !complete && schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)
                await Emit(new("value-partially-observed", fieldId,
                    new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                var mapping = value as JsonObject;
                bool childrenValid = true;
                if (containerPresent && schema.AdditionalProperties is null && mapping!.Any(member => !schema.Properties.ContainsKey(member.Key)))
                {
                    await Emit(new("value-invalid", fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
                    childrenValid = false;
                }
                var resolvedNames = new HashSet<string>(schema.Properties.Count, StringComparer.Ordinal);
                var localUnknown = ReferenceEquals(row, mapping) && rowUnknown is not null
                    ? rowUnknown : new HashSet<string>(StringComparer.Ordinal);
                if (observations is not null)
                    foreach (string name in schema.Properties.Keys)
                        if (observations.State(DefinitionPath.FromSegments(location.Append(name))) == DefinitionImportObservationState.Unobserved)
                            localUnknown.Add(name);
                foreach (string name in schema.Properties.Keys) await ResolveProperty(name);

                async ValueTask ResolveProperty(string name)
                {
                    if (!resolvedNames.Add(name)) return;
                    DefinitionNode childSchema = schema.Properties[name];
                    JsonElement? condition = childSchema.Keywords.TryGetProperty("x-configurator", out var extension)
                        && extension.TryGetProperty("applicableWhen", out var predicate) ? predicate : null;
                    foreach (string dependency in await SchemaReferencesAsync(childSchema, row is null ? "root" : "item", work))
                        if (schema.Properties.ContainsKey(dependency) && dependency != name) await ResolveProperty(dependency);
                    bool? applicable = condition is { } test ? await Evaluate(test, Unknown(),
                        row is null ? null : await ElementAsync(row, work), rowUnknown, rowLocation) : true;
                    if (applicable is null)
                    {
                        localUnknown.Add(name);
                        if (observations is null) unresolved.Add(location[0]);
                        else await Emit(new("condition-unobserved", fieldId, new DefinitionCorrectionLocation(
                            answers.InputRevision, [.. correction, DefinitionLocationSegment.Property(name)])));
                    }
                    bool childPresent = containerPresent && mapping!.ContainsKey(name);
                    JsonNode? childValue = childPresent ? mapping![name] : null;
                    bool childRequired = containerPresent && schema.Required.Contains(name, StringComparer.Ordinal);
                    var child = await Resolve(childSchema, childValue, childPresent, [.. location, name], fieldId,
                        [.. safePath, name], containerPresent && applicable == true, childRequired, sensitivity,
                        row, rowUnknown, [.. correction, DefinitionLocationSegment.Property(name)], rowLocation);
                    childrenValid &= child.Valid;
                    if (applicable == true && (!child.Valid || childRequired && !child.Present || draft && !child.Present &&
                        SetupFieldDefinition.EffectiveSensitivity(childSchema, sensitivity) == SetupFieldSensitivity.Secret))
                        localUnknown.Add(name);
                    if (containerPresent)
                    {
                        if (!child.Present) mapping!.Remove(name);
                        else if (!ReferenceEquals(mapping![name], child.Value)) mapping![name] = child.Value;
                    }
                }
                if (containerPresent)
                {
                    var requiredNames = new HashSet<string>(StringComparer.Ordinal);
                    JsonElement keywords = schema.Keywords;
                    if (keywords.TryGetProperty("if", out var guard))
                    {
                        bool? holds = await EvaluateGuardAsync(guard, mapping!, localUnknown, observations, work, observedPath, invalidLocations);
                        if (holds == true) RequireLocal(keywords.GetProperty("then").GetProperty("required"));
                        else if (holds == false && keywords.TryGetProperty("else", out var alternative))
                            RequireLocal(alternative.GetProperty("required"));
                        else if (holds is null)
                        {
                            if (observations is null) unresolved.Add(location[0]);
                            else await Emit(new("condition-unobserved", fieldId,
                                new DefinitionCorrectionLocation(answers.InputRevision, correction)));
                        }
                    }
                    if (keywords.TryGetProperty("dependentRequired", out var relationships))
                        foreach (JsonProperty dependency in relationships.EnumerateObject())
                            if (localUnknown.Contains(dependency.Name)) unresolved.Add(location[0]);
                            else if (mapping!.ContainsKey(dependency.Name)) RequireLocal(dependency.Value);
                    foreach (string name in requiredNames)
                    {
                        string[] requiredPath = [.. safePath, name];
                        int nodeIndex = -1;
                        for (int candidate = start; candidate < nodes.Count; candidate++)
                        {
                            if (work is not null) await work.VisitAsync();
                            DefinitionNodeState node = nodes[candidate];
                            if (node.FieldId != fieldId ||
                                !node.RelativePath.SequenceEqual(requiredPath, StringComparer.Ordinal)) continue;
                            nodeIndex = candidate;
                            break;
                        }
                        if (nodeIndex >= 0)
                        {
                            DefinitionNodeState node = nodes[nodeIndex];
                            if (!node.IsActive) continue;
                            bool hasChildObligation = false;
                            foreach (DefinitionNodeState descendant in nodes.Skip(start))
                            {
                                if (work is not null) await work.VisitAsync();
                                if (descendant.FieldId == fieldId && descendant.IsObligation &&
                                    descendant.RelativePath.Count > requiredPath.Length &&
                                    descendant.RelativePath.Take(requiredPath.Length).SequenceEqual(requiredPath, StringComparer.Ordinal))
                                {
                                    hasChildObligation = true;
                                    break;
                                }
                            }
                            nodes[nodeIndex] = new(node.FieldId, node.RelativePath, node.IsActive, true, node.IsProvided,
                                node.IsReady, !hasChildObligation, node.IsProvided && node.IsReady, node.IsMissing, node.Sensitivity, node.Location);
                        }
                        if (localUnknown.Contains(name))
                        {
                            if (observations is not null) await Emit(new("value-unobserved", fieldId,
                                new DefinitionCorrectionLocation(answers.InputRevision, [.. correction, DefinitionLocationSegment.Property(name)])));
                            continue;
                        }
                        if (mapping!.ContainsKey(name)) continue;
                        DefinitionNode child = schema.Properties[name];
                        if (draft && SetupFieldDefinition.EffectiveSensitivity(child, sensitivity) == SetupFieldSensitivity.Secret)
                            deferred.Add(location[0]);
                        else missing.Add((location[0], fieldId, [.. safePath, name],
                            [.. correction, DefinitionLocationSegment.Property(name)]));
                    }
                    void RequireLocal(JsonElement names)
                    {
                        foreach (JsonElement name in names.EnumerateArray()) requiredNames.Add(name.GetString()!);
                    }
                }
                if (containerPresent && schema.AdditionalProperties is { } mapSchema)
                {
                    int entry = 0;
                    foreach ((string name, JsonNode? childValue) in mapping!.ToArray())
                    {
                        if (schema.Properties.ContainsKey(name)) continue;
                        budget.Scalar(name);
                        var child = await Resolve(mapSchema, childValue, true, [.. location, name], fieldId,
                            [.. safePath, "@entry", entry.ToString(CultureInfo.InvariantCulture)], true, true, sensitivity,
                            row, rowUnknown, [.. correction, DefinitionLocationSegment.MapEntry(entry)], rowLocation);
                        childrenValid &= child.Valid;
                        if (!ReferenceEquals(mapping![name], child.Value)) mapping[name] = child.Value;
                        entry++;
                    }
                }
                if (active && present)
                {
                    resolvedElement = await ElementAsync(value, work);
                    valid = childrenValid && await LocalValueValidAsync(schema, resolvedElement, complete, work);
                }
            }
            else if (schema.Type == DefinitionNodeType.Sequence && active && present
                && value is JsonArray sequence)
            {
                valid = true;
                for (int index = 0; index < sequence.Count; index++)
                {
                    string ordinal = index.ToString(CultureInfo.InvariantCulture);
                    var child = await Resolve(schema.Items!, sequence[index], true, [.. location, ordinal], fieldId,
                        [.. safePath, ordinal], true, true, sensitivity, sequence[index] as JsonObject,
                        new(StringComparer.Ordinal), [.. correction, DefinitionLocationSegment.ArrayRow(index)],
                        DefinitionPath.FromSegments(location.Append(ordinal)));
                    valid &= child.Valid;
                    if (!ReferenceEquals(sequence[index], child.Value)) sequence[index] = child.Value;
                }
                resolvedElement = await ElementAsync(value, work);
                valid &= await LocalValueValidAsync(schema, resolvedElement, complete, work);
                int duplicate = await DuplicateIndexAsync(schema, resolvedElement, work);
                if (duplicate >= 0)
                {
                    JsonElement uniqueBy = schema.Keywords.GetProperty("x-configurator").GetProperty("uniqueBy");
                    await Emit(new("value-duplicate", fieldId, new DefinitionCorrectionLocation(answers.InputRevision,
                        [.. correction, DefinitionLocationSegment.ArrayRow(duplicate),
                         .. DefinitionPath.Parse(uniqueBy.GetString()!).Segments.Select(DefinitionLocationSegment.Property)])));
                }
            }
            else if (active && present)
            {
                resolvedElement = await ElementAsync(value, work);
                valid = await LocalValueValidAsync(schema, resolvedElement, complete, work);
            }
            if (active && present && !valid && !await LocalValueValidAsync(schema, resolvedElement, complete, work))
                invalidLocations.Add(observedPath);
            if (active && present && !valid && diagnostics.Count == diagnosticsBefore)
                await Emit(new("value-invalid", fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            bool childObligation = false;
            foreach (DefinitionNodeState node in nodes.Skip(start))
            {
                if (work is not null) await work.VisitAsync();
                if (!node.IsObligation) continue;
                childObligation = true;
                break;
            }
            bool obligation = active && required && (schema.Type != DefinitionNodeType.Mapping || !childObligation);
            bool obligationReady = present && (schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence
                ? await LocalValueValidAsync(schema, resolvedElement, complete, work) : valid);
            nodes.Insert(start, new(fieldId, safePath, active, required, active && present, valid,
                obligation, obligationReady, !present, SetupFieldDefinition.EffectiveSensitivity(schema, inherited),
                new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            return (active && present, value, valid);

        }

        async ValueTask SettleRequirements()
        {
            var unknown = Unknown();
            var relationshipRequired = new HashSet<string>(StringComparer.Ordinal);
            var witnesses = new HashSet<string>(StringComparer.Ordinal);
            await CloseUncertainty();
            foreach (var absent in missing)
            {
                if (work is not null) await work.VisitAsync();
                if (absent.Path.Length > 0 || !unknown.Contains(absent.Root))
                {
                    witnesses.Add(absent.Root);
                    await Emit(new("value-required", absent.Id,
                        new DefinitionCorrectionLocation(answers.InputRevision, absent.Correction)));
                }
            }
            foreach (string name in relationshipRequired)
            {
                if (work is not null) await work.VisitAsync();
                if (observations?.State(DefinitionPath.FromSegments([name])) == DefinitionImportObservationState.Unobserved)
                {
                    await Emit(new("value-unobserved", fields[name].Id));
                    continue;
                }
                if (!effective.ContainsKey(name) && !unknown.Contains(name) && states[name].IsActive)
                {
                    if (draft && SetupFieldDefinition.EffectiveSensitivity(fields[name].Node) == SetupFieldSensitivity.Secret)
                        deferred.Add(name);
                    else
                    {
                        witnesses.Add(name);
                        await Emit(new("value-required", fields[name].Id,
                            new DefinitionCorrectionLocation(answers.InputRevision, [])));
                    }
                }
            }
            unknown.UnionWith(witnesses);
            unknown.UnionWith(deferred);
            await CloseUncertainty();
            var diagnosticFields = new HashSet<string>(StringComparer.Ordinal);
            var diagnosticPaths = new HashSet<(string FieldId, DefinitionPath Path)>();
            foreach (DefinitionDiagnostic diagnostic in diagnostics)
            {
                if (work is not null) await work.VisitAsync();
                diagnosticFields.Add(diagnostic.FieldId);
                diagnosticPaths.Add((diagnostic.FieldId, DefinitionPath.FromSegments(diagnostic.RelativePath)));
            }
            var childObligations = new HashSet<string>(StringComparer.Ordinal);
            foreach (DefinitionNodeState node in nodes)
            {
                if (work is not null) await work.VisitAsync();
                if (node.RelativePath.Count > 0 && node.IsObligation) childObligations.Add(node.FieldId);
            }
            var fieldsById = rootFields.ToDictionary(field => field.Id, StringComparer.Ordinal);
            foreach (SetupFieldDefinition field in rootFields)
            {
                SetupFieldState previous = states[field.Name];
                if (work is not null) await work.VisitAsync();
                bool? applicable = field.ActiveWhen is { } condition ? await Evaluate(condition, unknown) : true;
                bool active = applicable == true;
                if (applicable is null) unresolved.Add(field.Name);
                if (!active) effective.Remove(field.Name);
                bool required = active && (field.Required || relationshipRequired.Contains(field.Name));
                states[field.Name] = new(field, active, required, active && previous.IsProvided,
                    diagnosticFields.Contains(field.Id));
            }
            unresolved.UnionWith(unknown.Except(deferred).Except(invalidControllers)
                .Except(observations is null ? [] : witnesses));
            for (int index = 0; index < nodes.Count; index++)
            {
                if (work is not null) await work.VisitAsync();
                DefinitionNodeState node = nodes[index];
                if (node.FieldId.Length == 0) continue;
                SetupFieldState root = states[fieldsById[node.FieldId].Name];
                bool active = root.IsActive && node.IsActive;
                bool required = node.RelativePath.Count == 0 ? root.IsRequired : node.IsRequired;
                bool error = diagnosticPaths.Contains((node.FieldId, DefinitionPath.FromSegments(node.RelativePath)));
                bool rootObligation = required && node.RelativePath.Count == 0 &&
                    (root.Definition.Node.Type != DefinitionNodeType.Mapping ||
                     !childObligations.Contains(node.FieldId));
                nodes[index] = new(node.FieldId, node.RelativePath, active, required, active && node.IsProvided,
                    !error && node.IsReady, active && (node.IsObligation || rootObligation),
                    node.IsObligationReady, node.IsMissing, node.Sensitivity, node.Location);
            }

            async ValueTask CloseUncertainty()
            {
                for (int scan = 0; scan <= fields.Count; scan++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = unknown.Count;
                    relationshipRequired.Clear();
                    foreach (SetupFieldDefinition field in rootFields)
                    {
                        if (work is not null) await work.VisitAsync();
                        if (field.ActiveWhen is { } condition && await Evaluate(condition, unknown) is null)
                            unknown.Add(field.Name);
                    }
                    JsonElement keywords = rootSchema.Keywords;
                    if (keywords.TryGetProperty("if", out JsonElement guard))
                    {
                        bool? holds = await EvaluateGuardAsync(guard, effective, unknown, observations, work,
                            DefinitionPath.Parse(""), invalidLocations);
                        Require(keywords.GetProperty("then").GetProperty("required"), holds);
                        if (keywords.TryGetProperty("else", out var alternative))
                            Require(alternative.GetProperty("required"), !holds);
                    }
                    if (keywords.TryGetProperty("dependentRequired", out JsonElement dependencies))
                        foreach (JsonProperty dependency in dependencies.EnumerateObject())
                            Require(dependency.Value, unknown.Contains(dependency.Name) ||
                                invalidControllers.Contains(dependency.Name) ||
                                observations?.State(DefinitionPath.FromSegments([dependency.Name])) == DefinitionImportObservationState.Unobserved
                                    ? null : effective.ContainsKey(dependency.Name));
                    if (unknown.Count == count) return;
                }
                throw new SetupDefinitionException("validation-limit");
            }

            void Require(JsonElement targets, bool? holds)
            {
                if (holds == false) return;
                foreach (JsonElement target in targets.EnumerateArray())
                {
                    string name = target.GetString()!;
                    if (holds == true) relationshipRequired.Add(name);
                    else if (!effective.ContainsKey(name) && states[name].IsActive) unknown.Add(name);
                }
            }
        }

        HashSet<string> Unknown() => deferred.Concat(unresolved)
            .Concat(observations is null ? invalidControllers : []).ToHashSet(StringComparer.Ordinal);

        ValueTask<bool?> Evaluate(JsonElement condition, IReadOnlySet<string> unknown, JsonElement? item = null,
            IReadOnlySet<string>? itemUnknown = null, DefinitionPath? itemLocation = null) =>
            EvaluateConditionAsync(condition, effective, unknown, item, itemUnknown, itemLocation: itemLocation,
                observations: observations, work: work, invalidLocations: invalidLocations);
    }

    private static async ValueTask<JsonElement> ElementAsync(JsonNode? value, DefinitionImportWork? work)
    {
        if (work is null) return Element(value);
        CancellationToken cancellationToken = work.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not (JsonObject or JsonArray)) return Element(value);
        Func<CancellationToken, ValueTask>? cooperativeYield = work.CooperativeYield;
        int materializedNodes = 0;
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
                await WriteAsync(writer, value);
            using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
            return document.RootElement.Clone();
        }
        finally { buffer.Clear(); }

        async ValueTask WriteAsync(Utf8JsonWriter writer, JsonNode? node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Materialization is scheduling work, not another semantic evaluation.
            if (++materializedNodes % 1024 == 0 && cooperativeYield is not null)
            {
                await cooperativeYield(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            switch (node)
            {
                case JsonObject mapping:
                    writer.WriteStartObject();
                    foreach ((string name, JsonNode? child) in mapping)
                    {
                        writer.WritePropertyName(name);
                        await WriteAsync(writer, child);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonArray sequence:
                    writer.WriteStartArray();
                    foreach (JsonNode? child in sequence) await WriteAsync(writer, child);
                    writer.WriteEndArray();
                    break;
                case null: writer.WriteNullValue(); break;
                default: node.WriteTo(writer); break;
            }
        }
    }

    private static JsonElement Element(JsonNode? value)
    {
        using JsonDocument document = JsonDocument.Parse(value?.ToJsonString() ?? "null");
        return document.RootElement.Clone();
    }

    internal static bool ValueValid(DefinitionNode schema, JsonElement value)
    {
        if (!DefinitionImportWork.CompleteSynchronously(LocalValueValidAsync(schema, value))) return false;
        if (value.ValueKind == JsonValueKind.Null) return true;
        if (schema.Variants.Count > 0)
            return SelectVariant(schema, value) is { } branch && ValueValid(branch, value);
        if (schema.Type == DefinitionNodeType.Mapping)
        {
            if (schema.AdditionalProperties is null && value.EnumerateObject().Any(member => !schema.Properties.ContainsKey(member.Name))) return false;
            foreach ((string name, DefinitionNode child) in schema.Properties)
            {
                bool present = value.TryGetProperty(name, out JsonElement supplied);
                if (!present && schema.Required.Contains(name, StringComparer.Ordinal) || present && !ValueValid(child, supplied)) return false;
            }
            if (schema.AdditionalProperties is { } map)
                foreach (JsonProperty entry in value.EnumerateObject())
                    if (!schema.Properties.ContainsKey(entry.Name) && !ValueValid(map, entry.Value)) return false;
        }
        if (schema.Type == DefinitionNodeType.Sequence)
            foreach (JsonElement item in value.EnumerateArray())
                if (!ValueValid(schema.Items!, item)) return false;
        return true;
    }

    /// <summary>Returns the declared branch selected by a supplied discriminator, without inventing a branch.</summary>
    public static DefinitionNode? SelectVariant(DefinitionNode schema, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Discriminator is null || value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(schema.Discriminator, out JsonElement discriminator)) return null;
        return DiscriminatedBranch(schema, discriminator);
    }

    private static DefinitionNode? DiscriminatedBranch(DefinitionNode schema, JsonElement discriminator) =>
        schema.Variants.FirstOrDefault(branch =>
            JsonElement.DeepEquals(branch.Properties[schema.Discriminator!].Keywords.GetProperty("const"), discriminator));

    private static async ValueTask<bool> LocalValueValidAsync(DefinitionNode schema, JsonElement value, bool complete = true,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        bool compatible = value.ValueKind == JsonValueKind.Null ? schema.IsNullable : schema.Type switch
        {
            DefinitionNodeType.Text => value.ValueKind == JsonValueKind.String,
            DefinitionNodeType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            DefinitionNodeType.WholeNumber => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            DefinitionNodeType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            DefinitionNodeType.Mapping => value.ValueKind == JsonValueKind.Object,
            DefinitionNodeType.Sequence => value.ValueKind == JsonValueKind.Array,
            _ => false
        };
        if (!compatible) return false;
        JsonElement keywords = schema.Keywords;
        if (keywords.TryGetProperty("const", out JsonElement fixedValue) && !JsonElement.DeepEquals(value, fixedValue)) return false;
        if (keywords.TryGetProperty("enum", out JsonElement choices))
        {
            bool found = false;
            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (work is not null) await work.VisitAsync();
                if (!JsonElement.DeepEquals(value, choice)) continue;
                found = true;
                break;
            }
            if (!found) return false;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            int length = text.EnumerateRunes().Count();
            if (keywords.TryGetProperty("minLength", out JsonElement minimum) && length < minimum.GetInt32()
                || keywords.TryGetProperty("maxLength", out JsonElement maximum) && length > maximum.GetInt32()
                || keywords.TryGetProperty("x-configurator", out JsonElement extension)
                    && extension.TryGetProperty("notBlank", out JsonElement notBlank) && notBlank.GetBoolean() && string.IsNullOrWhiteSpace(text)) return false;
            if (keywords.TryGetProperty("x-configurator", out JsonElement formatMetadata) &&
                formatMetadata.TryGetProperty("assertFormat", out JsonElement asserted) && asserted.GetBoolean() &&
                !FormatMatches(keywords.GetProperty("format").GetString()!, text)) return false;
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out decimal number)
                || keywords.TryGetProperty("minimum", out JsonElement minimum) && number < minimum.GetDecimal()
                || keywords.TryGetProperty("maximum", out JsonElement maximum) && number > maximum.GetDecimal()
                || keywords.TryGetProperty("exclusiveMinimum", out JsonElement exclusiveMinimum) && number <= exclusiveMinimum.GetDecimal()
                || keywords.TryGetProperty("exclusiveMaximum", out JsonElement exclusiveMaximum) && number >= exclusiveMaximum.GetDecimal()
                || keywords.TryGetProperty("multipleOf", out JsonElement multiple) && number % multiple.GetDecimal() != 0) return false;
        }
        if (value.ValueKind == JsonValueKind.Array && keywords.TryGetProperty("uniqueItems", out JsonElement unique) &&
            unique.GetBoolean())
        {
            var seen = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (work is not null) await work.VisitAsync();
                byte[] canonical = DefinitionJson.Canonicalize(item, keyPolicy: CompositionKeyPolicy.Exact);
                string identity;
                try { identity = Convert.ToHexString(SHA256.HashData(canonical)); }
                finally { CryptographicOperations.ZeroMemory(canonical); }
                if (!seen.TryGetValue(identity, out List<JsonElement>? bucket)) seen.Add(identity, bucket = []);
                if (bucket.Any(previous => JsonElement.DeepEquals(previous, item))) return false;
                bucket.Add(item);
            }
        }
        if (complete && value.ValueKind == JsonValueKind.Array
            && (keywords.TryGetProperty("minItems", out JsonElement minItems) && value.GetArrayLength() < minItems.GetInt32()
                || keywords.TryGetProperty("maxItems", out JsonElement maxItems) && value.GetArrayLength() > maxItems.GetInt32()
                || await DuplicateIndexAsync(schema, value, work) >= 0)) return false;
        if (value.ValueKind == JsonValueKind.Object && !PropertyCountValid(schema, value.EnumerateObject().Count(), complete)) return false;
        return true;
    }

    private static bool PropertyCountValid(DefinitionNode schema, int count, bool complete = true) =>
        (!complete || !schema.Keywords.TryGetProperty("minProperties", out JsonElement minimum) || count >= minimum.GetInt32()) &&
        (!schema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum) || count <= maximum.GetInt32());

    private static bool FormatMatches(string format, string text)
    {
        if (format == "hostname") return Hostname(text);
        if (format == "email-address")
        {
            int separator = text.IndexOf('@');
            if (separator is < 1 or > 64 || separator != text.LastIndexOf('@') || text.Length > 254) return false;
            string local = text[..separator];
            return local[0] != '.' && local[^1] != '.' && !local.Contains("..", StringComparison.Ordinal) &&
                local.All(character => char.IsAsciiLetterOrDigit(character) ||
                    ".!#$%&'*+-/=?^_`{|}~".Contains(character)) && Hostname(text[(separator + 1)..]);
        }
        if (text.Any(character => character <= ' ' || character >= 127) ||
            !Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || !uri.IsWellFormedOriginalString()) return false;
        return format == "uri" || format == "http-url" && (uri.Scheme is "http" or "https") &&
            uri.Host.Length != 0 && uri.UserInfo.Length == 0;

        static bool Hostname(string host)
        {
            if (host.EndsWith('.')) host = host[..^1];
            return host.Length is > 0 and <= 253 && host.Split('.').All(label =>
                label.Length is > 0 and <= 63 && char.IsAsciiLetterOrDigit(label[0]) &&
                char.IsAsciiLetterOrDigit(label[^1]) &&
                label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
        }
    }

    private static async ValueTask<int> DuplicateIndexAsync(DefinitionNode schema, JsonElement value, DefinitionImportWork? work = null)
    {
        if (value.ValueKind != JsonValueKind.Array || !schema.Keywords.TryGetProperty("x-configurator", out var metadata)
            || !metadata.TryGetProperty("uniqueBy", out var uniqueBy)) return -1;
        DefinitionPath path = DefinitionPath.Parse(uniqueBy.GetString()!);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (work is not null) await work.VisitAsync();
            JsonElement key = item;
            foreach (string name in path.Segments)
            {
                if (work is not null) await work.VisitAsync();
                if (key.ValueKind != JsonValueKind.Object || !key.TryGetProperty(name, out key)) return -1;
            }
            if (key.ValueKind != JsonValueKind.String) return -1;
            if (!keys.Add(key.GetString()!)) return index;
            index++;
        }
        return -1;
    }

    internal static bool ConditionMatches(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values) =>
        EvaluateCondition(condition, values, null) == true;

    internal static bool? EvaluateCondition(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved, JsonElement? item = null, IReadOnlySet<string>? itemUnknown = null,
        IReadOnlySet<DefinitionPath>? deferredLocations = null, DefinitionPath? itemLocation = null) =>
        DefinitionImportWork.CompleteSynchronously(
            EvaluateConditionAsync(condition, values, unresolved, item, itemUnknown, deferredLocations, itemLocation));

    private static async ValueTask<bool?> EvaluateConditionAsync(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved, JsonElement? item = null, IReadOnlySet<string>? itemUnknown = null,
        IReadOnlySet<DefinitionPath>? deferredLocations = null, DefinitionPath? itemLocation = null,
        DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null)
    {
        if (work is not null) await work.VisitAsync();
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            var results = new List<bool?>();
            foreach (JsonElement operand in condition.GetProperty("operands").EnumerateArray())
                results.Add(await EvaluateConditionAsync(operand, values, unresolved, item, itemUnknown,
                    deferredLocations, itemLocation, observations, work, observationBase, invalidLocations));
            if (operation == "not") return !results[0];
            return operation == "all" ? results.Contains(false) ? false : results.Contains(null) ? null : true
                : results.Contains(true) ? true : results.Contains(null) ? null : false;
        }
        JsonElement reference = condition.GetProperty("reference");
        DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
        if (observations is not null)
        {
            DefinitionPath? prefix = reference.GetProperty("scope").GetString() == "item" ? itemLocation : observationBase;
            DefinitionPath concrete = prefix is null ? path : DefinitionPath.FromSegments(prefix.Segments.Concat(path.Segments));
            if (observations.State(concrete) == DefinitionImportObservationState.Unobserved ||
                invalidLocations?.Any(location => DefinitionImportProjection.IsPrefix(location, concrete)) == true)
                return null;
            if (operation != "exists" && !observations.Complete(concrete)) return null;
        }
        if (reference.GetProperty("scope").GetString() == "root" && unresolved is not null
            && (path.Segments.Count == 0 ? unresolved.Count != 0 : unresolved.Contains(path.Segments[0]))) return null;
        if (reference.GetProperty("scope").GetString() == "item" && itemUnknown is not null
            && (path.Segments.Count == 0 ? itemUnknown.Count != 0 : itemUnknown.Contains(path.Segments[0]))) return null;
        if (reference.GetProperty("scope").GetString() == "item" && deferredLocations is not null && itemLocation is not null)
        {
            DefinitionPath concrete = DefinitionPath.FromSegments(itemLocation.Segments.Concat(path.Segments));
            if (deferredLocations.Any(location => location.Segments.Take(Math.Min(location.Segments.Count, concrete.Segments.Count))
                .SequenceEqual(concrete.Segments.Take(Math.Min(location.Segments.Count, concrete.Segments.Count)), StringComparer.Ordinal)))
                return null;
        }
        bool present = TryResolveReference(reference, values, item, out JsonElement value);
        if (operation == "exists") return present;
        if (!present) return false;
        return operation switch
        {
            "equals" => JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "notEquals" => !JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "in" => condition.GetProperty("values").EnumerateArray().Any(candidate => JsonElement.DeepEquals(value, candidate)),
            _ => throw new SetupDefinitionException("condition-operation")
        };
    }

    internal static bool TryResolveReference(JsonElement reference, IReadOnlyDictionary<string, JsonElement> values,
        JsonElement? item, out JsonElement value)
    {
        DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
        int start = 0;
        if (reference.GetProperty("scope").GetString() == "item")
        {
            if (item is null) { value = default; return false; }
            value = item.Value;
        }
        else if (path.Segments.Count == 0)
        {
            value = JsonSerializer.SerializeToElement(values);
            return true;
        }
        else
        {
            if (!values.TryGetValue(path.Segments[0], out value)) return false;
            start = 1;
        }
        for (int index = start; index < path.Segments.Count; index++)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(path.Segments[index], out value)) return false;
            }
            else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position)
                && position < value.GetArrayLength()) value = value[position];
            else { value = default; return false; }
        }
        return true;
    }

    internal static IEnumerable<string> References(JsonElement condition)
        => ScopedReferences(condition, "root");

    internal static IEnumerable<string> SchemaReferences(DefinitionNode schema, string scope) =>
        DefinitionImportWork.CompleteSynchronously(SchemaReferencesAsync(schema, scope));

    private static async ValueTask<IEnumerable<string>> SchemaReferencesAsync(DefinitionNode schema, string scope,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        IEnumerable<string> own = schema.Keywords.TryGetProperty("x-configurator", out var extension)
            && extension.TryGetProperty("applicableWhen", out var condition) ? await ScopedReferencesAsync(condition, scope, work) : [];
        IEnumerable<DefinitionNode> children = schema.Properties.Values.Concat(schema.Variants)
            .Concat(schema.AdditionalProperties is null ? [] : new[] { schema.AdditionalProperties })
            .Concat(scope == "root" && schema.Items is not null ? new[] { schema.Items } : []);
        var references = new List<string>(own);
        foreach (DefinitionNode child in children)
            references.AddRange(await SchemaReferencesAsync(child, scope, work));
        return references.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static IEnumerable<string> ScopedReferences(JsonElement condition, string scope) =>
        DefinitionImportWork.CompleteSynchronously(ScopedReferencesAsync(condition, scope));

    private static async ValueTask<IEnumerable<string>> ScopedReferencesAsync(JsonElement condition, string scope,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        if (condition.TryGetProperty("reference", out JsonElement reference))
        {
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            if (reference.GetProperty("scope").GetString() == scope && path.Segments.Count != 0)
                return [path.Segments[0]];
            return [];
        }
        var references = new List<string>();
        foreach (JsonElement operand in condition.GetProperty("operands").EnumerateArray())
            references.AddRange(await ScopedReferencesAsync(operand, scope, work));
        return references.ToArray();
    }

    private static ValueTask<bool?> EvaluateGuardAsync(JsonElement guard, JsonObject values, IReadOnlySet<string> unknown,
        DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null) =>
        EvaluateGuardAsync(guard, values.ToDictionary(property => property.Key, property => Element(property.Value),
            StringComparer.Ordinal), unknown, observations, work, observationBase, invalidLocations);

    private static ValueTask<bool?> EvaluateGuardAsync(JsonElement guard, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string> unknown, DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null)
    {
        var operands = new JsonArray();
        JsonObject Reference(string name) => new()
        {
            ["scope"] = "root",
            ["path"] = DefinitionPath.FromSegments([name]).ToPointer()
        };
        if (guard.TryGetProperty("required", out var required))
            foreach (JsonElement name in required.EnumerateArray())
                operands.Add(new JsonObject { ["op"] = "exists", ["reference"] = Reference(name.GetString()!) });
        if (guard.TryGetProperty("properties", out var properties))
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                bool constant = property.Value.TryGetProperty("const", out var expected);
                var assertion = new JsonObject
                {
                    ["op"] = constant ? "equals" : "in",
                    ["reference"] = Reference(property.Name),
                    [constant ? "value" : "values"] = JsonNode.Parse(
                        (constant ? expected : property.Value.GetProperty("enum")).GetRawText())
                };
                operands.Add(new JsonObject
                {
                    ["op"] = "any",
                    ["operands"] = new JsonArray(
                    new JsonObject
                    {
                        ["op"] = "not",
                        ["operands"] = new JsonArray(
                        new JsonObject { ["op"] = "exists", ["reference"] = Reference(property.Name) })
                    }, assertion)
                });
            }
        return EvaluateConditionAsync(Element(new JsonObject { ["op"] = "all", ["operands"] = operands }), values, unknown,
            observations: observations, work: work, observationBase: observationBase, invalidLocations: invalidLocations);
    }
}
