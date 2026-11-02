using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Secrets;

namespace Configurator.Cli;

internal static class CliSecretHandlers
{
    internal static async Task<CliCommandResult> ExecuteAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        Definition definition = await CliIoOperations.DefinitionAsync(invocation, command.Definition!, cancellationToken);
        AnswerSnapshot answers = await CliIoOperations.AnswersAsync(invocation, command.Input!, "0", cancellationToken);
        DefinitionPath path = DefinitionPath.Parse(command.Pointer!);
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers, cancellationToken);
        DefinitionNode? node = Resolve(definition, answers, validation, path, out DefinitionLocationSegment[] location);
        if (node?.Secret is not { Acquisition: DefinitionSecretAcquisition.Local, Generation: not null } specification)
            return CliResults.Failure(CliExitCode.Blocked, "secret-operation-unavailable", "/input");
        bool derives = specification.Kind is DefinitionSecretKind.PasswordHash or DefinitionSecretKind.Digest;
        bool entered = derives && specification.Generation.Input == DefinitionSecretInput.Entered;
        if (entered != (command.Operation == "derive"))
            return CliResults.Failure(CliExitCode.Usage, "secret-operation-mismatch");
        bool sidecar = derives && specification.Generation.Input == DefinitionSecretInput.Generated;
        if (sidecar && command.GeneratedPasswordOutput is null)
            return CliResults.Failure(CliExitCode.Usage, "generated-password-output-required");
        if (!sidecar && command.GeneratedPasswordOutput is not null)
            return CliResults.Failure(CliExitCode.Usage, "generated-password-output-inapplicable");
        string[] destinations = sidecar ? [command.GeneratedPasswordOutput!, command.Output!] : [command.Output!];
        var inputs = new List<string> { command.Definition!, command.Input! };
        if (command.SecretInput is { } secretPath) inputs.Add(secretPath);
        if (destinations.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).Count() != destinations.Length
            || destinations.Any(destination => inputs.Where(input => input != "-")
                .Any(input => Path.GetFullPath(input) == Path.GetFullPath(destination))))
            return CliResults.Failure(CliExitCode.Usage, "secret-output-conflict");
        foreach (string destination in destinations)
            if (await invocation.Io.Output.CheckArtifactDestinationAsync(destination, cancellationToken) is not null)
                return CliResults.Failure(CliExitCode.Blocked, "secret-output-unavailable", "/output");

        char[] input = [];
        byte[] answerBytes = [];
        byte[] passwordBytes = [];
        try
        {
            if (entered)
            {
                int maximum = Math.Min(invocation.Io.MaximumBytes, 65_536);
                using CliInputBuffer buffer = await invocation.Io.Input.ReadAsync(command.SecretInput!, maximum, cancellationToken);
                if (buffer.Bytes.Length > maximum)
                    return CliResults.Failure(CliExitCode.Data, "secret-input-limit", "/input");
                try
                {
                    var encoding = new UTF8Encoding(false, true);
                    input = new char[encoding.GetCharCount(buffer.Bytes.Span)];
                    encoding.GetChars(buffer.Bytes.Span, input);
                }
                catch (DecoderFallbackException) { return CliResults.Failure(CliExitCode.Data, "secret-input-encoding", "/input"); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            using SecretGenerationResult generated = SecretGenerator.Generate(specification, input);
            if (!generated.Succeeded)
                return CliResults.Failure(CliExitCode.Validation, generated.ErrorCode ?? "secret-generation-failed", "/input");
            cancellationToken.ThrowIfCancellationRequested();
            if (sidecar != (generated.GeneratedPassword is not null))
                return CliResults.Failure(CliExitCode.Internal, "secret-result-invalid");
            string value = generated.Output!.CopyValue();
            if (SecretValueValidator.Validate(specification, value).Count != 0
                || !DefinitionValidator.ValueValid(node, JsonSerializer.SerializeToElement(value)))
                return CliResults.Failure(CliExitCode.Validation, "secret-generated-value-invalid", "/input");
            AnswerSnapshot updated = Replace(answers, path, value);
            DefinitionValidationResult updatedValidation = DefinitionValidator.Validate(definition, updated, cancellationToken);
            FieldDefinition field = definition.Fields.Single(field => field.Name == path.Segments[0]);
            if (updatedValidation.Nodes.Any(state => state.FieldId == field.Id
                && state.Location.Segments.SequenceEqual(location) && state.HasError))
                return CliResults.Failure(CliExitCode.Validation, "secret-generated-value-invalid", "/input");
            var coverage = new CliMachineCoverage(updatedValidation.Fields.Where(item => item.IsActive && item.IsProvided && !item.HasError).Select(item => item.Id),
                updatedValidation.Fields.Where(item => item.IsRequired && (!item.IsProvided || item.HasError)).Select(item => item.Id));
            var readiness = updatedValidation.IsReady ? CliResults.Ready()
                : new CliMachineReadiness("incomplete", ["workspace-incomplete"], []);
            answerBytes = AnswerSnapshotCodec.Serialize(updated);
            if (sidecar) passwordBytes = generated.GeneratedPassword!.CopyUtf8Bytes();
            var receipts = new List<CliMachineArtifact>();
            if (sidecar) receipts.Add(Receipt("generated-password", "generated-password-output", "text/plain"));
            receipts.Add(Receipt("answer-snapshot", "answer-output", "application/json"));
            CliCommandResult result = CliResults.Success(receipts, coverage, readiness) with
            { DefinitionDigest = definition.Digest.Value, Preparation = updatedValidation.IsReady ? "ready" : "incomplete" };
            ledger.Plan(invocation, command, result);
            var outcomes = new CliPublicationOutcome[destinations.Length];
            Array.Fill(outcomes, new(null));
            bool cleanupFailed = false;
            bool cancelled = false;
            for (int index = 0; index < destinations.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ledger.Record(index, "unknown");
                ledger.Cleanup("unknown");
                CliPublicationResult publication = await invocation.Io.Output.WriteArtifactAsync(destinations[index],
                    ArtifactKind.Configuration, sidecar && index == 0 ? passwordBytes : answerBytes,
                    invocation.Io.MaximumBytes, cancellationToken);
                if (publication.Outcomes.Count != 1)
                    return ledger.Preserve(CliResults.Failure(CliExitCode.Internal, "publication-status-invalid"));
                outcomes[index] = publication.Outcomes[0];
                ledger.Record(receipts[index].Id, outcomes[index]);
                cleanupFailed |= publication.CleanupFailed;
                cancelled |= publication.Cancelled;
                ledger.Cleanup(cleanupFailed ? "failed" : outcomes[index].Unknown ? "unknown" : "completed");
                if (cancelled || cleanupFailed || outcomes[index].Unknown || outcomes[index].Result is not { Succeeded: true }) break;
            }
            return ledger.Apply(new(outcomes, cleanupFailed, cancelled), result, blockedRejections: true);

            CliMachineArtifact Receipt(string kind, string id, string mediaType) =>
                new(kind, mediaType, null, "restricted", coverage, readiness, "output", "not-attempted") { Id = id };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(input.AsSpan()));
            CryptographicOperations.ZeroMemory(answerBytes);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static DefinitionNode? Resolve(Definition definition, AnswerSnapshot answers,
        DefinitionValidationResult validation, DefinitionPath path, out DefinitionLocationSegment[] location)
    {
        location = [];
        if (path.Segments.Count == 0) return null;
        if (answers.Omitted.Any(omitted => omitted.Segments.Count < path.Segments.Count
            && path.Segments.Take(omitted.Segments.Count).SequenceEqual(omitted.Segments))) return null;
        FieldDefinition? field = definition.Fields.FirstOrDefault(field => field.Name == path.Segments[0]);
        if (field is null) return null;
        var segments = new List<DefinitionLocationSegment>();
        DefinitionNode schema = definition.Configuration;
        JsonElement value = JsonSerializer.SerializeToElement(answers.Values);
        for (int index = 0; index <= path.Segments.Count; index++)
        {
            if (schema.Keywords.TryGetProperty("readOnly", out JsonElement readOnly) && readOnly.ValueKind == JsonValueKind.True)
                return null;
            if (schema.Variants.Count != 0)
            {
                DefinitionNode? variant = DefinitionValidator.SelectVariant(schema, value);
                if (variant is null) return null;
                schema = variant;
                if (schema.Keywords.TryGetProperty("readOnly", out readOnly) && readOnly.ValueKind == JsonValueKind.True) return null;
            }
            if (index == path.Segments.Count)
            {
                location = segments.ToArray();
                return validation.Nodes.Any(state => state.FieldId == field.Id
                    && state.Location.Segments.SequenceEqual(segments) && state.IsActive) ? schema : null;
            }
            string segment = path.Segments[index];
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                if (value.ValueKind != JsonValueKind.Object) return null;
                bool declared = schema.Properties.TryGetValue(segment, out DefinitionNode? child);
                if (!declared) child = schema.AdditionalProperties;
                if (child is null) return null;
                if (index > 0)
                {
                    if (declared) segments.Add(DefinitionLocationSegment.Property(segment));
                    else
                    {
                        string[] keys = value.EnumerateObject().Where(member => !schema.Properties.ContainsKey(member.Name))
                            .Select(member => member.Name).ToArray();
                        int ordinal = Array.IndexOf(keys, segment);
                        if (ordinal < 0) return null;
                        segments.Add(DefinitionLocationSegment.MapEntry(ordinal));
                    }
                }
                value = value.TryGetProperty(segment, out JsonElement member) ? member : default;
                schema = child;
            }
            else if (schema.Type == DefinitionNodeType.Sequence && schema.Items is not null
                && value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row)
                && row < value.GetArrayLength())
            { schema = schema.Items; value = value[row]; segments.Add(DefinitionLocationSegment.ArrayRow(row)); }
            else return null;
        }
        return null;
    }

    private static AnswerSnapshot Replace(AnswerSnapshot answers, DefinitionPath path, string value)
    {
        JsonNode root = JsonSerializer.SerializeToNode(answers.Values)!;
        JsonNode parent = root;
        for (int index = 0; index < path.Segments.Count - 1; index++)
            parent = parent is JsonArray array ? array[int.Parse(path.Segments[index], System.Globalization.CultureInfo.InvariantCulture)]!
                : parent[path.Segments[index]]!;
        if (parent is JsonArray sequence) sequence[int.Parse(path.Segments[^1], System.Globalization.CultureInfo.InvariantCulture)] = value;
        else parent[path.Segments[^1]] = value;
        return new(answers.InputRevision + 1, root.AsObject().ToDictionary(item => item.Key,
            item => JsonSerializer.SerializeToElement(item.Value), StringComparer.Ordinal),
            answers.Omitted.Where(omitted => !omitted.Segments.Take(path.Segments.Count).SequenceEqual(path.Segments)));
    }
}
