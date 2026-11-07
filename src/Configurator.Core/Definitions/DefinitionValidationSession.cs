namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Secrets;

using static Configurator.Core.Definitions.DefinitionValidator;

internal static class DefinitionValidationSession
{
    internal static async ValueTask<DefinitionValidationResult> RunAsync(Definition definition, AnswerSnapshot answers,
        bool draft, CancellationToken cancellationToken, DefinitionImportEvidence? observations = null,
        DefinitionImportWork? work = null, Func<DefinitionDiagnostic, ValueTask>? diagnosticObserved = null,
        DefinitionConfigurationContext? scopeObservation = null, CompositionBudget? sharedBudget = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        if (scopeObservation is null && DefinitionArtifactComposer.PreparationDiagnostics.IsEnabled("ValidationStarted"))
            DefinitionArtifactComposer.PreparationDiagnostics.Write("ValidationStarted", null);
#endif
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
        var rootSensitivity = (FieldSensitivity)Math.Max(
            (int)FieldDefinition.SensitivityOf(definition.Configuration), (int)FieldDefinition.SensitivityOf(rootSchema));
        IReadOnlyList<FieldDefinition> rootFields = ReferenceEquals(rootSchema, definition.Configuration) ? definition.Fields
            : Array.AsReadOnly(rootSchema.Properties.Select(property => new FieldDefinition(property.Key, property.Value,
                rootSchema.Required.Contains(property.Key, StringComparer.Ordinal), rootSensitivity)).ToArray());
        var effective = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var diagnostics = new List<DefinitionDiagnostic>();
        var states = new Dictionary<string, FieldState>(StringComparer.Ordinal);
        var nodes = new List<DefinitionNodeState>();
        var deferred = new HashSet<string>(StringComparer.Ordinal);
        var deferredLocations = new HashSet<DefinitionPath>();
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        var invalidControllers = new HashSet<string>(StringComparer.Ordinal);
        var invalidLocations = new HashSet<DefinitionPath>();
        var uncertainLocations = new HashSet<DefinitionPath>();
        var fields = rootFields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        CompositionBudget budget = sharedBudget ?? new(CompositionLimits.Default);
        var missing = new List<(string Root, string Id, string[] Path, DefinitionLocationSegment[] Correction)>();
        DefinitionConfigurationContext? configurationContext = null;
        try
        {
            configurationContext = scopeObservation ?? DefinitionScopeResolver.Resolve(definition, answers, budget, cancellationToken);
            if (configurationContext is { IsReady: false }) await Emit(new("deployment-context", ""));
            if (configurationContext is { HasUnresolvedDependencies: true }) await Emit(new("scope-dependency-unresolved", ""));
            if (rootSchema.AdditionalProperties is null && answers.Values.Keys.Any(name => !fields.ContainsKey(name)))
                await Emit(new("field-unknown", ""));
            if (configurationContext is not null && scopeObservation is null)
                foreach ((string name, JsonElement value) in answers.Values)
                    if (fields.TryGetValue(name, out FieldDefinition? field)) await CheckStructure(field.Node, value, field.Id, 1);
            foreach (DefinitionPath omission in answers.Omitted)
            {
                DefinitionNode current = rootSchema;
                for (int index = 0; index < omission.Segments.Count; index++)
                {
                    if (current.Type == DefinitionNodeType.Mapping &&
                        current.Properties.TryGetValue(omission.Segments[index], out DefinitionNode? child))
                        current = child;
                    else if (current.Type == DefinitionNodeType.Sequence && omission.TryGetArrayIndex(index, out int row) &&
                        row < CompositionLimits.DefaultSequenceEntries) current = current.Items!;
                    else if (current.Type == DefinitionNodeType.Mapping && current.AdditionalProperties is { } map)
                        current = map;
                    else { await Emit(new("answer-omission", "")); break; }
                }
            }
            foreach (FieldDefinition field in rootFields) await Activate(field);
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
            if (scopeObservation is null && !PropertyCountValid(rootSchema, effective.Count, rootComplete))
                await Emit(new("value-invalid", ""));
            if (scopeObservation is null && DefinitionArtifactComposer.PreparationDiagnostics.IsEnabled("ValidationCompleted"))
                DefinitionArtifactComposer.PreparationDiagnostics.Write("ValidationCompleted", draft);
            return new(rootFields.Select(field => states[field.Name]), diagnostics, effective, deferred, unresolved, nodes,
                deferredLocations, configurationContext, uncertainLocations.Concat(invalidLocations).Concat(deferredLocations).ToHashSet());
        }
        catch (CompositionException)
        {
            if (diagnosticObserved is not null) await diagnosticObserved(new("validation-limit", ""));
            return new([], [new("validation-limit", "")], new(StringComparer.Ordinal));
        }
        catch (DefinitionException exception) when (exception.Code == "path-limit")
        {
            if (diagnosticObserved is not null) await diagnosticObserved(new("validation-limit", ""));
            return new([], [new("validation-limit", "")], new(StringComparer.Ordinal));
        }

        async ValueTask Emit(DefinitionDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
            if (diagnosticObserved is not null) await diagnosticObserved(diagnostic);
        }

        async ValueTask Activate(FieldDefinition field)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (work is not null) await work.VisitAsync();
            if (states.ContainsKey(field.Name)) return;
            if (!visiting.Add(field.Name)) throw new DefinitionException("activation-cycle");
            foreach (string dependency in await SchemaReferencesAsync(field.Node, "root", work))
                if (fields.TryGetValue(dependency, out FieldDefinition? controller)) await Activate(controller);
            var unknown = Unknown();
            bool? applies = field.ActiveWhen is { } condition ? await Evaluate(condition, unknown) : true;
            if (applies is null && InScope([field.Name]))
            {
                unresolved.Add(field.Name);
                uncertainLocations.Add(DefinitionPath.FromSegments([field.Name]));
            }
            bool active = applies == true && InScope([field.Name]);
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
            string[] safePath, bool active, bool required, FieldSensitivity inherited,
            JsonObject? row = null, HashSet<string>? rowUnknown = null,
            DefinitionLocationSegment[]? correction = null, DefinitionPath? rowLocation = null)
        {
            correction ??= [];
            active &= InScope(location);
            required &= active;
            cancellationToken.ThrowIfCancellationRequested();
            if (work is not null) await work.ValidationAsync();
            budget.Node(location.Length);
            DefinitionPath observedPath = DefinitionPath.FromSegments(location);
            bool observed = observations is null || observations.State(observedPath) != DefinitionImportObservationState.Unobserved;
            bool complete = observations is null || observations.Complete(observedPath);
            int start = nodes.Count;
            int diagnosticsBefore = diagnostics.Count;
            var sensitivity = (FieldSensitivity)Math.Max((int)inherited, (int)FieldDefinition.SensitivityOf(schema));
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
                        FieldDefinition.EffectiveSensitivity(schema, inherited),
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
            bool missingSecret = active && !present && draft && sensitivity == FieldSensitivity.Secret
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
                uncertainLocations.Add(observedPath);
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
                    if (applicable is null && active && InScope(location.Append(name)))
                    {
                        uncertainLocations.Add(DefinitionPath.FromSegments(location.Append(name)));
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
                    if (applicable == true && InScope(location.Append(name)) && (!child.Valid || childRequired && !child.Present || draft && !child.Present &&
                        FieldDefinition.EffectiveSensitivity(childSchema, sensitivity) == FieldSensitivity.Secret))
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
                    if (keywords.TryGetProperty("if", out var guard) && GuardRelevant(keywords, location))
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
                        uncertainLocations.Add(DefinitionPath.FromSegments(location.Append(name)));
                        DefinitionNode child = schema.Properties[name];
                        if (draft && FieldDefinition.EffectiveSensitivity(child, sensitivity) == FieldSensitivity.Secret)
                            deferred.Add(location[0]);
                        else missing.Add((location[0], fieldId, [.. safePath, name],
                            [.. correction, DefinitionLocationSegment.Property(name)]));
                    }
                    void RequireLocal(JsonElement names)
                    {
                        foreach (JsonElement name in names.EnumerateArray())
                            if (InScope(location.Append(name.GetString()!))) requiredNames.Add(name.GetString()!);
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
                    if (!child.Present) sequence[index] = null;
                    else if (!ReferenceEquals(sequence[index], child.Value)) sequence[index] = child.Value;
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
            {
                IReadOnlyList<string> secretFailures = schema.Secret is { } secret && resolvedElement.ValueKind == JsonValueKind.String
                    ? SecretValueValidator.Validate(secret, resolvedElement.GetString()!) : [];
                IReadOnlyList<string> codes = secretFailures.Count == 0 ? ["value-invalid"] : secretFailures;
                foreach (string code in codes)
                    await Emit(new(code, fieldId, new DefinitionCorrectionLocation(answers.InputRevision, correction)));
            }
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
                obligation, obligationReady, !present, FieldDefinition.EffectiveSensitivity(schema, inherited),
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
                    uncertainLocations.Add(DefinitionPath.FromSegments([name]));
                    if (draft && FieldDefinition.EffectiveSensitivity(fields[name].Node) == FieldSensitivity.Secret)
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
            foreach (FieldDefinition field in rootFields)
            {
                FieldState previous = states[field.Name];
                if (work is not null) await work.VisitAsync();
                bool? applicable = field.ActiveWhen is { } condition ? await Evaluate(condition, unknown) : true;
                bool active = applicable == true && InScope([field.Name]);
                if (applicable is null && InScope([field.Name])) unresolved.Add(field.Name);
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
                FieldState root = states[fieldsById[node.FieldId].Name];
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
                    foreach (FieldDefinition field in rootFields)
                    {
                        if (work is not null) await work.VisitAsync();
                        if (InScope([field.Name]) && field.ActiveWhen is { } condition && await Evaluate(condition, unknown) is null)
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
                throw new DefinitionException("validation-limit");
            }

            void Require(JsonElement targets, bool? holds)
            {
                if (holds == false) return;
                foreach (JsonElement target in targets.EnumerateArray())
                {
                    string name = target.GetString()!;
                    if (!InScope([name])) continue;
                    if (holds == true) relationshipRequired.Add(name);
                    else if (!effective.ContainsKey(name) && states[name].IsActive) unknown.Add(name);
                }
            }
        }

        bool InScope(IEnumerable<string> location)
        {
            if (configurationContext is not null) return configurationContext.Includes(location);
            if (definition.Deployment is not { } deployment) return true;
            DefinitionPath path = DefinitionPath.FromSegments(location);
            return path.ToPointer() != deployment.RecipePointer && path.ToPointer() != deployment.ScopePointer;
        }

        bool GuardRelevant(JsonElement keywords, string[] location)
        {
            if (configurationContext is null) return true;
            JsonElement guard = keywords.GetProperty("if");
            IEnumerable<string> names = guard.TryGetProperty("required", out var required)
                ? required.EnumerateArray().Select(item => item.GetString()!) : [];
            if (guard.TryGetProperty("properties", out var properties)) names = names.Concat(properties.EnumerateObject().Select(item => item.Name));
            names = names.Concat(keywords.GetProperty("then").GetProperty("required").EnumerateArray().Select(item => item.GetString()!));
            if (keywords.TryGetProperty("else", out var alternative))
                names = names.Concat(alternative.GetProperty("required").EnumerateArray().Select(item => item.GetString()!));
            return names.Any(name => InScope(location.Append(name)));
        }

        async ValueTask CheckStructure(DefinitionNode schema, JsonElement value, string fieldId, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget.Node(depth);
            if (schema.Variants.Count > 0)
            {
                DefinitionNode? branch = SelectVariant(schema, value);
                if (branch is not null) await CheckStructure(branch, value, fieldId, depth);
                else await Emit(new("value-variant", fieldId));
                return;
            }
            if (schema.Type == DefinitionNodeType.Mapping && value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    DefinitionNode? child = schema.Properties.GetValueOrDefault(property.Name) ?? schema.AdditionalProperties;
                    if (child is null) await Emit(new("field-unknown", fieldId));
                    else await CheckStructure(child, property.Value, fieldId, depth + 1);
                }
            if (schema.Type == DefinitionNodeType.Sequence && value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in value.EnumerateArray()) await CheckStructure(schema.Items!, item, fieldId, depth + 1);
        }

        HashSet<string> Unknown() => deferred.Concat(unresolved)
            .Concat(observations is null ? invalidControllers : []).ToHashSet(StringComparer.Ordinal);

        ValueTask<bool?> Evaluate(JsonElement condition, IReadOnlySet<string> unknown, JsonElement? item = null,
            IReadOnlySet<string>? itemUnknown = null, DefinitionPath? itemLocation = null) =>
            EvaluateConditionAsync(condition, effective, unknown, item, itemUnknown, itemLocation: itemLocation,
                observations: observations, work: work, invalidLocations: invalidLocations);
    }
}
