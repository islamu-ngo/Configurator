using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

namespace Configurator.Cli;


internal static class CliArtifactHandlers
{
    internal static async Task<CliCommandResult> ExecuteAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        Definition definition = await CliIoOperations.DefinitionAsync(invocation, command.Definition!, cancellationToken);
        if (command.Operation is "bundle-check" or "bundle-create")
            return await BundleAsync(command, invocation, definition, ledger, cancellationToken);
        ArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == command.Artifact);
        if (artifact is null)
            return CliResults.Failure(CliExitCode.Validation, "artifact-unknown", "/artifact");
        if (command.Family == "env" && artifact.Format != DefinitionArtifactFormat.Dotenv
            || command.Format is not null && artifact.Format.ToToken() != command.Format)
            return CliResults.Failure(CliExitCode.Usage, "artifact-format-mismatch", "/artifact");
        if (artifact.ApplySemantics == DefinitionTargetUpdateMode.Import
            && command.Operation is "create" or "format" or "export" or "preview" or "render")
            return CliResults.Failure(CliExitCode.Blocked, "import-command-required", "/artifact");

        if (command.Operation is "check-existing" or "prefill")
            return await AssessExistingAsync(command, invocation, definition, artifact, ledger, cancellationToken);
        if (command.Operation == "compare")
            return await CompareAsync(command, invocation, definition, artifact, cancellationToken);

        AnswerSnapshot answers = await CliIoOperations.AnswersAsync(invocation,
            command.Answers ?? command.Input!, command.InputRevision, cancellationToken);
        if (command.Operation == "import")
        {
            if (artifact.ApplySemantics != DefinitionTargetUpdateMode.Import
                || !definition.Fields.Any(field => field.Id == command.RevisionField && field.Type == "string")
                || !CopiesRevision(definition, artifact.Id,
                    definition.Fields.Single(field => field.Id == command.RevisionField).Name))
                return CliResults.Failure(CliExitCode.Blocked, "revision-projection-required");
            string revisionName = definition.Fields.Single(field => field.Id == command.RevisionField).Name;
            if (answers.Values.TryGetValue(revisionName, out JsonElement supplied)
                && (supplied.ValueKind != JsonValueKind.String || supplied.GetString() != command.ExpectedRevision))
                return CliResults.Failure(CliExitCode.Validation, "revision-conflict", "/input");
            if (answers.Omitted.Any(path => path.Segments.Count == 1 && path.Segments[0] == revisionName))
                return CliResults.Failure(CliExitCode.Validation, "revision-conflict", "/input");
            var values = answers.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            values[revisionName] = JsonSerializer.SerializeToElement(command.ExpectedRevision);
            answers = new(answers.InputRevision, values, answers.Omitted);
        }

        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers, cancellationToken);
        if (command.Operation == "import" && !validation.Fields.Any(field =>
            field.Id == command.RevisionField && field.IsActive && field.IsProvided && !field.HasError))
            return CliResults.Failure(CliExitCode.Blocked, "revision-projection-required");
        CliMachineCoverage coverage = new(
            validation.Fields.Where(field => field.IsActive && field.IsProvided && !field.HasError).Select(field => field.Id),
            validation.Fields.Where(field => field.IsRequired && (!field.IsProvided || field.HasError)).Select(field => field.Id));
        if (!validation.IsReady)
        {
            bool missingOnly = validation.Diagnostics.All(item => item.Code == "value-required");
            var readiness = new CliMachineReadiness("incomplete",
                coverage.MissingKeys.Count > 0 ? coverage.MissingKeys : ["input-invalid"], []);
            return new(missingOnly ? CliExitCode.Incomplete : CliExitCode.Validation,
                validation.Diagnostics.Select(CliResults.Diagnostic).ToArray(),
                [], coverage, readiness)
            { DefinitionDigest = definition.Digest.Value, Preparation = "incomplete" };
        }
        DefinitionArtifactResult composed = DefinitionArtifactComposer.Compose(definition, artifact.Id, answers, cancellationToken);
        if (!composed.Succeeded)
            return CliResults.Failure(CliExitCode.Validation, "artifact-composition-failed", "/input");
        using DefinitionArtifact output = composed.Artifact!;
        ReadOnlyMemory<byte> outputBytes = output.Bytes;
        try
        {
            if (command.Answers is not null && !await EquivalentInputAsync(
                invocation, command.Input!, artifact, outputBytes, cancellationToken))
                return CliResults.Failure(CliExitCode.Data, "artifact-projection-mismatch", "/input");
            if (command.Operation == "diff")
            {
                if (command.Answers is not null)
                {
                    bool equal = await EquivalentInputAsync(invocation, command.Baseline!, artifact, outputBytes, cancellationToken);
                    var compared = new CliMachineCoverage(equal ? [artifact.Id] : [], equal ? [] : [artifact.Id]);
                    return CliResults.Success(coverage: compared);
                }
                AnswerSnapshot baseline = await CliIoOperations.AnswersAsync(invocation, command.Baseline!, command.InputRevision, cancellationToken);
                DefinitionArtifactResult previous = DefinitionArtifactComposer.Compose(definition, artifact.Id, baseline, cancellationToken);
                if (!previous.Succeeded)
                    return CliResults.Failure(CliExitCode.Validation, "baseline-invalid", "/baseline");
                using DefinitionArtifact prior = previous.Artifact!;
                ReadOnlyMemory<byte> priorBytes = prior.Bytes;
                try
                {
                    bool same = priorBytes.Span.SequenceEqual(outputBytes.Span);
                    var facts = new CliMachineCoverage(same ? [artifact.Id] : [], same ? [] : [artifact.Id]);
                    return CliResults.Success(coverage: facts);
                }
                finally { Clear(priorBytes); }
            }
            if (command.Operation == "coverage")
                return CliResults.Success(coverage: coverage);

            bool write = command.Operation is "create" or "format" or "export" or "import" or "preview" or "render";
            var hostReadiness = new CliMachineReadiness("incomplete", ["host-validation-required"], []);
            CliCommandResult result = CliResults.Success([new CliMachineArtifact("definition-artifact", output.MediaType,
                output.Digest.Value, "restricted", coverage, hostReadiness,
                write ? CliResults.PathIntent(command.Output) : "input",
                write ? command.DryRun ? "planned" : "not-attempted" : "none") { Id = artifact.Id }], coverage, hostReadiness)
                with
            { DefinitionDigest = definition.Digest.Value, Preparation = "ready" };
            if (command.Output == "-") return CliResults.Failure(CliExitCode.Blocked, "restricted-stdout", "/output");
            if (!write || command.DryRun) return result;
            ledger.Plan(invocation, command, result);
            cancellationToken.ThrowIfCancellationRequested();
            ledger.BeginPublication();
            CliPublicationResult publication = await invocation.Io.Output.WriteArtifactAsync(command.Output!,
                artifact.Format == DefinitionArtifactFormat.Dotenv ? ArtifactKind.Environment : ArtifactKind.Configuration,
                outputBytes, invocation.Io.MaximumBytes, cancellationToken);
            return ledger.Apply(publication, result);
        }
        finally { Clear(outputBytes); }
    }

    internal static CliCommandResult Doctor()
    {
        if (!new ProtectedArtifactWriter().IsAvailable)
            return CliResults.Failure(CliExitCode.Blocked, "protected-writer-unavailable");
        byte[] schema = CliCommandSchemaMetadata.GenerateSchema();
        return CliResults.Success([CliResults.Artifact("doctor-report", "application/json", schema,
            "public", "none", "none")]);
    }

    private static async Task<CliCommandResult> CompareAsync(CliCommand command, CliInvocation invocation,
        Definition definition, ArtifactDefinition artifact, CancellationToken cancellationToken)
    {
        AnswerSnapshot? context = command.Answers is null ? null
            : await CliIoOperations.AnswersAsync(invocation, command.Answers, command.InputRevision, cancellationToken);
        using CliInputBuffer source = await CliIoOperations.ReadAsync(invocation, command.Input!, cancellationToken);
        using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, artifact.Id, source.Bytes,
            context: context, cancellationToken: cancellationToken);
        DefinitionImportReport report = assessment.Report;
        string compatibility = report.Compatibility switch
        {
            DefinitionImportCompatibility.Compatible => "compatible",
            DefinitionImportCompatibility.ChangesRequired => "changes-required",
            DefinitionImportCompatibility.Incomplete => "incomplete",
            DefinitionImportCompatibility.Unsupported => "unsupported",
            DefinitionImportCompatibility.InvalidInput => "invalid-input",
            _ => throw new InvalidOperationException("assessment-classification")
        };
        CliExitCode exit = report.Compatibility switch
        {
            DefinitionImportCompatibility.InvalidInput => CliExitCode.Data,
            DefinitionImportCompatibility.Unsupported => CliExitCode.Blocked,
            DefinitionImportCompatibility.ChangesRequired => CliExitCode.Validation,
            _ when !report.CoverageComplete || !report.InventoryComplete => CliExitCode.Incomplete,
            DefinitionImportCompatibility.Incomplete => CliExitCode.Incomplete,
            _ => CliExitCode.Success
        };
        CliMachineFinding[] findings = report.Findings.Select(finding => new CliMachineFinding(
            finding.Kind switch
            {
                DefinitionImportFindingKind.MissingRequired => "missing-required",
                DefinitionImportFindingKind.InvalidSuppliedValue => "invalid-supplied-value",
                DefinitionImportFindingKind.UnrecognizedInput => "unrecognized-input",
                DefinitionImportFindingKind.StructuralOutputChange => "structural-output-change",
                DefinitionImportFindingKind.Unassessed => "unassessed",
                DefinitionImportFindingKind.ContextDerivedValue => "context-derived-value",
                _ => throw new InvalidOperationException("assessment-finding")
            }, finding.Code,
            finding.Source is { } location ? new(Array.AsReadOnly(location.Ordinals.ToArray())) : null,
            finding.Schema is { } schema ? new(schema.FieldId, BoundedSchemaPath(schema.RelativePath)) : null))
            .OrderBy(item => item.Classification, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => JsonSerializer.Serialize(item.Schema, CliJsonContext.Default.CliMachineFindingSchema), StringComparer.Ordinal)
            .ThenBy(item => JsonSerializer.Serialize(item.Source, CliJsonContext.Default.CliMachineFindingSource), StringComparer.Ordinal)
            .ToArray();
        return new(exit, report.ExecutionCompleted ? [] : [new(report.Code, "/input", "error")],
            [], CliResults.EmptyCoverage(), new("unassessed", [], []))
        {
            DefinitionDigest = report.DefinitionDigest,
            Completion = report.ExecutionCompleted ? "completed" : "failed",
            Preparation = "not-evaluated",
            Assessment = new(report.DefinitionDigest, artifact.Id, compatibility,
                report.CoverageComplete ? "complete" : "incomplete", report.InventoryComplete ? "complete" : "stopped",
                report.AutofillEligible ? "eligible" : "ineligible", report.RetainedFindingCount, report.TotalFindingCount)
            { Findings = Array.AsReadOnly(findings) }
        };
    }

    // Long declared paths navigate to a safe enclosing schema, never a private imported key.
    private static ReadOnlyCollection<string> BoundedSchemaPath(IReadOnlyList<string> path)
    {
        var result = new List<string>();
        int characters = 0;
        foreach (string segment in path.Take(32))
        {
            if (segment.Length > 256 - characters) break;
            characters += segment.Length;
            result.Add(segment);
        }
        return result.AsReadOnly();
    }

    private static async Task<CliCommandResult> AssessExistingAsync(CliCommand command, CliInvocation invocation,
        Definition definition, ArtifactDefinition artifact, CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        AnswerSnapshot current = command.Answers is null
            ? new(long.Parse(command.InputRevision, CultureInfo.InvariantCulture),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            : await CliIoOperations.AnswersAsync(invocation, command.Answers, command.InputRevision, cancellationToken);
        using CliInputBuffer source = await CliIoOperations.ReadAsync(invocation, command.Input!, cancellationToken);
        using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, artifact.Id, source.Bytes,
            context: current, cancellationToken: cancellationToken);
        DefinitionImportResult result = assessment.AssessAutofill(current, cancellationToken: cancellationToken);
        if (!result.Succeeded)
            return CliResults.Failure(result.Code == "answer-conflict" ? CliExitCode.Validation
                : result.Code == "inverse-unsupported" || result.Code == "import-unsupported"
                    ? CliExitCode.Blocked : CliExitCode.Data, result.Code, "/input");

        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, result.Answers!, cancellationToken);
        var coverage = new CliMachineCoverage(
            validation.Fields.Where(field => field.IsActive && field.IsProvided && !field.HasError)
                .Select(field => field.Id),
            validation.Fields.Where(field => field.IsRequired && (!field.IsProvided || field.HasError))
                .Select(field => field.Id));
        var readiness = validation.IsReady ? CliResults.Ready()
            : new CliMachineReadiness("incomplete", coverage.MissingKeys, []);
        if (command.Operation == "check-existing")
            return CliResults.Success(coverage: coverage, readiness: readiness) with
            {
                DefinitionDigest = definition.Digest.Value,
                Preparation = validation.IsReady ? "ready" : "incomplete"
            };

        if (command.Output == "-")
            return CliResults.Failure(CliExitCode.Blocked, "restricted-stdout", "/output");
        byte[] bytes = AnswerSnapshotCodec.Serialize(result.Answers!);
        try
        {
            CliCommandResult receipt = CliResults.Success([CliResults.Artifact("answer-snapshot", "application/json", bytes,
                "restricted", CliResults.PathIntent(command.Output),
                command.DryRun ? "planned" : "not-attempted", coverage, readiness)], coverage, readiness)
                with
            { DefinitionDigest = definition.Digest.Value, Preparation = validation.IsReady ? "ready" : "incomplete" };
            if (command.DryRun) return receipt;
            ledger.Plan(invocation, command, receipt);
            cancellationToken.ThrowIfCancellationRequested();
            ledger.BeginPublication();
            return ledger.Apply(await invocation.Io.Output.WriteArtifactAsync(command.Output!, ArtifactKind.Configuration, bytes,
                invocation.Io.MaximumBytes, cancellationToken), receipt);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static async Task<CliCommandResult> BundleAsync(CliCommand command, CliInvocation invocation,
        Definition definition, CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        string[] ids = command.Artifacts!;
        if (definition.Artifacts.Any(item => ids.Contains(item.Id, StringComparer.Ordinal)
            && item.ApplySemantics == DefinitionTargetUpdateMode.Import))
            return CliResults.Failure(CliExitCode.Blocked, "import-command-required", "/artifacts");
        AnswerSnapshot answers = await CliIoOperations.AnswersAsync(invocation, command.Input!, command.InputRevision, cancellationToken);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(definition, ids, answers, cancellationToken);
        if (!bundle.Succeeded)
            return new(CliExitCode.Validation,
                bundle.Diagnostics.Select(CliResults.Diagnostic).ToArray(),
                [], CliResults.EmptyCoverage(), new("incomplete", ["input-invalid"], []))
            { DefinitionDigest = definition.Digest.Value, Preparation = "incomplete" };

        bool inspectedOnly = command.Operation == "bundle-check" || command.DryRun;
        var planned = CliResults.Success(bundle.Artifacts.Select(artifact =>
            new CliMachineArtifact("definition-artifact", artifact.MediaType, artifact.Digest.Value, "restricted",
                CliResults.EmptyCoverage(), CliResults.Ready(), CliResults.PathIntent(command.Output),
                command.Operation == "bundle-check" ? "none" : command.DryRun ? "planned" : "not-attempted")
            { Id = artifact.ArtifactId }).ToArray(),
            new(bundle.Artifacts.Select(item => item.ArtifactId), []),
            new("incomplete", ["host-validation-required"], [])) with
        { DefinitionDigest = definition.Digest.Value, Preparation = "ready" };
        if (inspectedOnly) return planned;
        ledger.Plan(invocation, command, planned);
        cancellationToken.ThrowIfCancellationRequested();
        ledger.BeginPublication();
        return ledger.Apply(await invocation.Io.Output.WriteBundleAsync(bundle.Artifacts, command.Output!,
            invocation.Io.MaximumBytes, ledger.Record, cancellationToken), planned, blockedRejections: true);
    }

    private static async Task<bool> EquivalentInputAsync(CliInvocation invocation, string path,
        ArtifactDefinition artifact, ReadOnlyMemory<byte> expected, CancellationToken cancellationToken)
    {
        using CliInputBuffer candidate = await CliIoOperations.ReadAsync(invocation, path, cancellationToken);
        return await EquivalentAsync(artifact, candidate.Bytes, expected, cancellationToken);
    }

    private static void Clear(ReadOnlyMemory<byte> bytes)
    {
        if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment))
            CryptographicOperations.ZeroMemory(segment.AsSpan());
    }

    private static bool CopiesRevision(Definition definition, string artifactId, string field)
    {
        using JsonDocument document = JsonDocument.Parse(definition.CanonicalBytes);
        JsonElement projection = document.RootElement.GetProperty("artifacts").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == artifactId).GetProperty("projection");
        if (projection.GetProperty("op").GetString() == "copy" && !projection.TryGetProperty("when", out _))
        {
            JsonElement rootReference = projection.GetProperty("reference");
            return rootReference.GetProperty("scope").GetString() == "root" && rootReference.GetProperty("path").GetString() == "";
        }
        // An unconditional top-level copy ensures the option is actually emitted.
        return projection.GetProperty("op").GetString() == "object"
            && projection.GetProperty("members").EnumerateObject().Any(member =>
                member.Value.GetProperty("op").GetString() == "copy"
                && member.Value.GetProperty("reference").GetProperty("scope").GetString() == "root"
                && member.Value.GetProperty("reference").GetProperty("path").GetString() == DefinitionPath.FromSegments([field]).ToPointer()
                && !member.Value.TryGetProperty("when", out _))
            && !projection.TryGetProperty("when", out _);
    }

    private static async Task<bool> EquivalentAsync(ArtifactDefinition artifact, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string format = artifact.Format.ToToken();
        if (format == "markdown") return source.Span.SequenceEqual(expected.Span);
        if (format == "dotenv")
        {
            EnvironmentDestinationDialect dialect = artifact.EnvironmentDialect ?? EnvironmentDestinationDialect.DockerRun;
            DotenvParseResult candidate = DotenvCodec.Parse(source, dialect, cancellationToken: cancellationToken);
            DotenvParseResult reference = DotenvCodec.Parse(expected, dialect, cancellationToken: cancellationToken);
            if (!candidate.Succeeded || !reference.Succeeded) throw new JsonException();
            return candidate.Document!.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value))
                .SequenceEqual(reference.Document!.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => (entry.Key, entry.Value)));
        }
        if (format == "json")
        {
            using JsonDocument candidate = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 32 });
            using JsonDocument reference = JsonDocument.Parse(expected, new JsonDocumentOptions { MaxDepth = 32 });
            int nodes = 0;
            int characters = 0;
            Check(candidate.RootElement);
            return JsonElement.DeepEquals(candidate.RootElement, reference.RootElement);

            void Check(JsonElement value)
            {
                if (++nodes > 65_536) throw new JsonException();
                switch (value.ValueKind)
                {
                    case JsonValueKind.Object:
                        var names = new HashSet<string>(StringComparer.Ordinal);
                        foreach (JsonProperty property in value.EnumerateObject())
                        {
                            if (!names.Add(property.Name) || names.Count > 4096) throw new JsonException();
                            Charge(property.Name);
                            Check(property.Value);
                        }
                        break;
                    case JsonValueKind.Array:
                        if (value.GetArrayLength() > 4096) throw new JsonException();
                        foreach (JsonElement child in value.EnumerateArray()) Check(child);
                        break;
                    case JsonValueKind.String: Charge(value.GetString()!); break;
                    default: Charge(value.GetRawText()); break;
                }
            }
            void Charge(string value)
            {
                characters += value.Length;
                if (value.Length > 65_536 || characters > 1024 * 1024) throw new JsonException();
            }
        }
        var compiler = new CompositionCompiler();
        CompositionSource candidateSource = format == "yaml"
            ? new CompositionYamlSource(source) : new CompositionJsonSource(source);
        CompositionSource referenceSource = format == "yaml"
            ? new CompositionYamlSource(expected) : new CompositionJsonSource(expected);
        CompositionResult candidateResult = await compiler.CompileAsync(candidateSource, cancellationToken);
        CompositionResult referenceResult = await compiler.CompileAsync(referenceSource, cancellationToken);
        if (!candidateResult.Succeeded || !referenceResult.Succeeded) throw new JsonException();
        return JsonElement.DeepEquals(candidateResult.Artifact!.Value, referenceResult.Artifact!.Value);
    }
}

