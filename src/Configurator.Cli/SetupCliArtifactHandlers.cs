using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Environment;

namespace Configurator.Cli;

internal static class SetupCliArtifactHandlers
{
    internal static async Task<SetupCliCommandResult> ExecuteAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        SetupDefinition definition = await SetupCliIoOperations.DefinitionAsync(invocation, command.Definition!, cancellationToken);
        if (command.Operation is "bundle-check" or "bundle-create")
            return await BundleAsync(command, invocation, definition, ledger, cancellationToken);
        SetupArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == command.Artifact);
        if (artifact is null)
            return SetupCliResults.Failure(SetupCliExitCode.Validation, "artifact-unknown", "$.artifact");
        if (command.Family == "env" && artifact.Format != "dotenv"
            || command.Format is not null && artifact.Format != command.Format)
            return SetupCliResults.Failure(SetupCliExitCode.Usage, "artifact-format-mismatch", "$.artifact");
        if (artifact.ApplySemantics == "import"
            && command.Operation is "create" or "format" or "export" or "preview" or "render")
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "import-command-required", "$.artifact");

        if (command.Operation is "check-existing" or "prefill")
            return await AssessExistingAsync(command, invocation, definition, artifact, ledger, cancellationToken);

        SetupAnswerSnapshot answers = await SetupCliIoOperations.AnswersAsync(invocation,
            command.Answers ?? command.Input!, command.InputRevision, cancellationToken);
        if (command.Operation == "import")
        {
            if (artifact.ApplySemantics != "import"
                || !definition.Fields.Any(field => field.Id == command.RevisionField && field.Type == "string")
                || !CopiesRevision(definition, artifact.Id,
                    definition.Fields.Single(field => field.Id == command.RevisionField).Name))
                return SetupCliResults.Failure(SetupCliExitCode.Blocked, "revision-projection-required");
            string revisionName = definition.Fields.Single(field => field.Id == command.RevisionField).Name;
            if (answers.Values.TryGetValue(revisionName, out JsonElement supplied)
                && (supplied.ValueKind != JsonValueKind.String || supplied.GetString() != command.ExpectedRevision))
                return SetupCliResults.Failure(SetupCliExitCode.Validation, "revision-conflict", "$.input");
            if (answers.Omitted.Any(path => path.Segments.Count == 1 && path.Segments[0] == revisionName))
                return SetupCliResults.Failure(SetupCliExitCode.Validation, "revision-conflict", "$.input");
            var values = answers.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            values[revisionName] = JsonSerializer.SerializeToElement(command.ExpectedRevision);
            answers = new(answers.InputRevision, values, answers.Omitted);
        }

        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers, cancellationToken);
        if (command.Operation == "import" && !validation.Fields.Any(field =>
            field.Id == command.RevisionField && field.IsActive && field.IsProvided && !field.HasError))
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "revision-projection-required");
        SetupCliMachineCoverage coverage = new(
            validation.Fields.Where(field => field.IsActive && field.IsProvided && !field.HasError).Select(field => field.Id),
            validation.Fields.Where(field => field.IsRequired && (!field.IsProvided || field.HasError)).Select(field => field.Id));
        if (!validation.IsReady)
        {
            bool missingOnly = validation.Diagnostics.All(item => item.Code == "value-required");
            var readiness = new SetupCliMachineReadiness("incomplete",
                coverage.MissingKeys.Count > 0 ? coverage.MissingKeys : ["input-invalid"], []);
            return new(missingOnly ? SetupCliExitCode.Incomplete : SetupCliExitCode.Validation,
                validation.Diagnostics.Select(SetupCliResults.Diagnostic).ToArray(),
                [], coverage, readiness)
            { DefinitionDigest = definition.Digest.Value, Preparation = "incomplete" };
        }
        DefinitionArtifactResult composed = DefinitionArtifactComposer.Compose(definition, artifact.Id, answers, cancellationToken);
        if (!composed.Succeeded)
            return SetupCliResults.Failure(SetupCliExitCode.Validation, "artifact-composition-failed", "$.input");
        using DefinitionArtifact output = composed.Artifact!;
        ReadOnlyMemory<byte> outputBytes = output.Bytes;
        try
        {
            if (command.Answers is not null && !await EquivalentInputAsync(
                invocation, command.Input!, artifact.Format, outputBytes, cancellationToken))
                return SetupCliResults.Failure(SetupCliExitCode.Data, "artifact-projection-mismatch", "$.input");
            if (command.Operation == "diff")
            {
                if (command.Answers is not null)
                {
                    bool equal = await EquivalentInputAsync(invocation, command.Baseline!, artifact.Format, outputBytes, cancellationToken);
                    var compared = new SetupCliMachineCoverage(equal ? [artifact.Id] : [], equal ? [] : [artifact.Id]);
                    return SetupCliResults.Success(coverage: compared);
                }
                SetupAnswerSnapshot baseline = await SetupCliIoOperations.AnswersAsync(invocation, command.Baseline!, command.InputRevision, cancellationToken);
                DefinitionArtifactResult previous = DefinitionArtifactComposer.Compose(definition, artifact.Id, baseline, cancellationToken);
                if (!previous.Succeeded)
                    return SetupCliResults.Failure(SetupCliExitCode.Validation, "baseline-invalid", "$.baseline");
                using DefinitionArtifact prior = previous.Artifact!;
                ReadOnlyMemory<byte> priorBytes = prior.Bytes;
                try
                {
                    bool same = priorBytes.Span.SequenceEqual(outputBytes.Span);
                    var facts = new SetupCliMachineCoverage(same ? [artifact.Id] : [], same ? [] : [artifact.Id]);
                    return SetupCliResults.Success(coverage: facts);
                }
                finally { Clear(priorBytes); }
            }
            if (command.Operation == "coverage")
                return SetupCliResults.Success(coverage: coverage);

            bool write = command.Operation is "create" or "format" or "export" or "import" or "preview" or "render";
            var hostReadiness = new SetupCliMachineReadiness("incomplete", ["host-validation-required"], []);
            SetupCliCommandResult result = SetupCliResults.Success([new SetupCliMachineArtifact("definition-artifact", output.MediaType,
                output.Digest.Value, "restricted", coverage, hostReadiness,
                write ? SetupCliResults.PathIntent(command.Output) : "input",
                write ? command.DryRun ? "planned" : "not-attempted" : "none") { Id = artifact.Id }], coverage, hostReadiness)
                with
            { DefinitionDigest = definition.Digest.Value, Preparation = "ready" };
            if (command.Output == "-") return SetupCliResults.Failure(SetupCliExitCode.Blocked, "restricted-stdout", "/output");
            if (!write || command.DryRun) return result;
            ledger.Plan(invocation, command, result);
            cancellationToken.ThrowIfCancellationRequested();
            ledger.BeginPublication();
            SetupCliPublicationResult publication = await invocation.Io.Output.WriteArtifactAsync(command.Output!,
                artifact.Format == "dotenv" ? SetupArtifactKind.Environment : SetupArtifactKind.Configuration,
                outputBytes, invocation.Io.MaximumBytes, cancellationToken);
            return ledger.Apply(publication, result);
        }
        finally { Clear(outputBytes); }
    }

    internal static SetupCliCommandResult Doctor()
    {
        if (!new ProtectedArtifactWriter().IsAvailable)
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "protected-writer-unavailable");
        byte[] schema = SetupCliCommandSchemaMetadata.GenerateSchema();
        return SetupCliResults.Success([SetupCliResults.Artifact("doctor-report", "application/json", schema,
            "public", "none", "none")]);
    }

    private static async Task<SetupCliCommandResult> AssessExistingAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupDefinition definition, SetupArtifactDefinition artifact, SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        SetupAnswerSnapshot current = command.Answers is null
            ? new(long.Parse(command.InputRevision, CultureInfo.InvariantCulture),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            : await SetupCliIoOperations.AnswersAsync(invocation, command.Answers, command.InputRevision, cancellationToken);
        using SetupCliInputBuffer source = await SetupCliIoOperations.ReadAsync(invocation, command.Input!, cancellationToken);
        DefinitionImportResult result = DefinitionImport.Assess(definition, artifact.Id, source.Bytes, current);
        if (!result.Succeeded)
            return SetupCliResults.Failure(result.Code == "answer-conflict" ? SetupCliExitCode.Validation
                : result.Code == "inverse-unsupported" || result.Code == "import-unsupported"
                    ? SetupCliExitCode.Blocked : SetupCliExitCode.Data, result.Code, "$.input");

        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, result.Answers!, cancellationToken);
        var coverage = new SetupCliMachineCoverage(
            validation.Fields.Where(field => field.IsActive && field.IsProvided && !field.HasError)
                .Select(field => field.Id),
            validation.Fields.Where(field => field.IsRequired && (!field.IsProvided || field.HasError))
                .Select(field => field.Id));
        var readiness = validation.IsReady ? SetupCliResults.Ready()
            : new SetupCliMachineReadiness("incomplete", coverage.MissingKeys, []);
        if (command.Operation == "check-existing")
            return SetupCliResults.Success(coverage: coverage, readiness: readiness) with
            {
                DefinitionDigest = definition.Digest.Value,
                Preparation = validation.IsReady ? "ready" : "incomplete"
            };

        if (command.Output == "-")
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "restricted-stdout", "$.output");
        byte[] bytes = SetupAnswerSnapshotCodec.Serialize(result.Answers!);
        try
        {
            SetupCliCommandResult receipt = SetupCliResults.Success([SetupCliResults.Artifact("answer-snapshot", "application/json", bytes,
                "restricted", SetupCliResults.PathIntent(command.Output),
                command.DryRun ? "planned" : "not-attempted", coverage, readiness)], coverage, readiness)
                with
            { DefinitionDigest = definition.Digest.Value, Preparation = validation.IsReady ? "ready" : "incomplete" };
            if (command.DryRun) return receipt;
            ledger.Plan(invocation, command, receipt);
            cancellationToken.ThrowIfCancellationRequested();
            ledger.BeginPublication();
            return ledger.Apply(await invocation.Io.Output.WriteArtifactAsync(command.Output!, SetupArtifactKind.Configuration, bytes,
                invocation.Io.MaximumBytes, cancellationToken), receipt);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static async Task<SetupCliCommandResult> BundleAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupDefinition definition, SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        string[] ids = command.Artifacts!;
        if (definition.Artifacts.Any(item => ids.Contains(item.Id, StringComparer.Ordinal)
            && item.ApplySemantics == "import"))
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "import-command-required", "$.artifacts");
        SetupAnswerSnapshot answers = await SetupCliIoOperations.AnswersAsync(invocation, command.Input!, command.InputRevision, cancellationToken);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(definition, ids, answers, cancellationToken);
        if (!bundle.Succeeded)
            return new(SetupCliExitCode.Validation,
                bundle.Diagnostics.Select(SetupCliResults.Diagnostic).ToArray(),
                [], SetupCliResults.EmptyCoverage(), new("incomplete", ["input-invalid"], []))
            { DefinitionDigest = definition.Digest.Value, Preparation = "incomplete" };

        bool inspectedOnly = command.Operation == "bundle-check" || command.DryRun;
        var planned = SetupCliResults.Success(bundle.Artifacts.Select(artifact =>
            new SetupCliMachineArtifact("definition-artifact", artifact.MediaType, artifact.Digest.Value, "restricted",
                SetupCliResults.EmptyCoverage(), SetupCliResults.Ready(), SetupCliResults.PathIntent(command.Output),
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

    private static async Task<bool> EquivalentInputAsync(SetupCliInvocation invocation, string path,
        string format, ReadOnlyMemory<byte> expected, CancellationToken cancellationToken)
    {
        using SetupCliInputBuffer candidate = await SetupCliIoOperations.ReadAsync(invocation, path, cancellationToken);
        return await EquivalentAsync(format, candidate.Bytes, expected, cancellationToken);
    }

    private static void Clear(ReadOnlyMemory<byte> bytes)
    {
        if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment))
            CryptographicOperations.ZeroMemory(segment.AsSpan());
    }

    private static bool CopiesRevision(SetupDefinition definition, string artifactId, string field)
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

    private static async Task<bool> EquivalentAsync(string format, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (format == "markdown") return source.Span.SequenceEqual(expected.Span);
        if (format == "dotenv")
        {
            DotenvParseResult candidate = DotenvCodec.Parse(source);
            DotenvParseResult reference = DotenvCodec.Parse(expected);
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
        var compiler = new SetupCompositionCompiler();
        SetupCompositionSource candidateSource = format == "yaml"
            ? new SetupCompositionYamlSource(source) : new SetupCompositionJsonSource(source);
        SetupCompositionSource referenceSource = format == "yaml"
            ? new SetupCompositionYamlSource(expected) : new SetupCompositionJsonSource(expected);
        SetupCompositionResult candidateResult = await compiler.CompileAsync(candidateSource, cancellationToken);
        SetupCompositionResult referenceResult = await compiler.CompileAsync(referenceSource, cancellationToken);
        if (!candidateResult.Succeeded || !referenceResult.Succeeded) throw new JsonException();
        return JsonElement.DeepEquals(candidateResult.Artifact!.Value, referenceResult.Artifact!.Value);
    }
}

internal static class SetupCliIoOperations
{
    internal static async ValueTask<SetupCliInputBuffer> ReadAsync(SetupCliInvocation invocation, string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetupCliInputBuffer buffer = await invocation.Io.Input.ReadAsync(path, invocation.Io.MaximumBytes, cancellationToken);
        if (buffer.Bytes.Length > invocation.Io.MaximumBytes || cancellationToken.IsCancellationRequested)
        {
            buffer.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("input-bound");
        }
        return buffer;
    }

    internal static async ValueTask<SetupDefinition> DefinitionAsync(SetupCliInvocation invocation, string path,
        CancellationToken cancellationToken)
    {
        using SetupCliInputBuffer buffer = await ReadAsync(invocation, path, cancellationToken);
        return Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml"
            ? SetupDefinitionSourceCodec.ParseYaml(buffer.Bytes) : SetupDefinitionSourceCodec.ParseJson(buffer.Bytes);
    }

    internal static async ValueTask<SetupAnswerSnapshot> AnswersAsync(SetupCliInvocation invocation, string path,
        string revision, CancellationToken cancellationToken)
    {
        using SetupCliInputBuffer buffer = await ReadAsync(invocation, path, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SetupAnswerSnapshot snapshot = SetupAnswerSnapshotCodec.ParseJson(buffer.Bytes, long.Parse(revision, CultureInfo.InvariantCulture));
        cancellationToken.ThrowIfCancellationRequested();
        return snapshot;
    }
}
