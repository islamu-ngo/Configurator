using System.Globalization;
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
    internal static SetupCliCommandResult Execute(SetupCliCommand command, SetupCliInvocation invocation)
    {
        SetupDefinition definition = SetupCliIoOperations.Definition(invocation, command.Definition!);
        SetupArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == command.Artifact);
        if (artifact is null)
            return SetupCliResults.Failure(SetupCliExitCode.Validation, "artifact-unknown", "$.artifact");
        if (command.Family == "env" && artifact.Format != "dotenv"
            || command.Format is not null && artifact.Format != command.Format)
            return SetupCliResults.Failure(SetupCliExitCode.Usage, "artifact-format-mismatch", "$.artifact");
        if (artifact.ApplySemantics == "import"
            && command.Operation is "create" or "format" or "export" or "preview" or "render")
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "import-command-required", "$.artifact");

        SetupAnswerSnapshot answers = SetupCliIoOperations.Answers(invocation, command.Answers ?? command.Input!, command.InputRevision);
        if (command.Operation == "import")
        {
            if (artifact.ApplySemantics != "import"
                || !definition.Fields.Any(field => field.Id == command.RevisionField && field.Type == "string")
                || !CopiesRevision(definition, artifact.Id, command.RevisionField!))
                return SetupCliResults.Failure(SetupCliExitCode.Blocked, "revision-projection-required");
            if (answers.Values.TryGetValue(command.RevisionField!, out JsonElement supplied)
                && (supplied.ValueKind != JsonValueKind.String || supplied.GetString() != command.ExpectedRevision))
                return SetupCliResults.Failure(SetupCliExitCode.Validation, "revision-conflict", "$.input");
            var values = answers.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            values[command.RevisionField!] = JsonSerializer.SerializeToElement(command.ExpectedRevision);
            answers = new(answers.InputRevision, values);
        }

        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, answers);
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
                validation.Diagnostics.Take(128).Select(item => new SetupCliMachineDiagnostic(item.Code, "$.input", "error")).ToArray(),
                [], coverage, readiness);
        }
        DefinitionArtifactResult composed = DefinitionArtifactComposer.Compose(definition, artifact.Id, answers);
        if (!composed.Succeeded)
            return SetupCliResults.Failure(SetupCliExitCode.Validation, "artifact-composition-failed", "$.input");
        DefinitionArtifact output = composed.Artifact!;
        if (command.Answers is not null && !Equivalent(
            artifact.Format, SetupCliIoOperations.Read(invocation, command.Input!), output.Bytes))
            return SetupCliResults.Failure(SetupCliExitCode.Data, "artifact-projection-mismatch", "$.input");
        if (command.Operation == "diff")
        {
            if (command.Answers is not null)
            {
                bool equal = Equivalent(artifact.Format, SetupCliIoOperations.Read(invocation, command.Baseline!), output.Bytes);
                var compared = new SetupCliMachineCoverage(equal ? [artifact.Id] : [], equal ? [] : [artifact.Id]);
                return SetupCliResults.Success(coverage: compared);
            }
            SetupAnswerSnapshot baseline = SetupCliIoOperations.Answers(invocation, command.Baseline!, command.InputRevision);
            DefinitionArtifactResult previous = DefinitionArtifactComposer.Compose(definition, artifact.Id, baseline);
            if (!previous.Succeeded)
                return SetupCliResults.Failure(SetupCliExitCode.Validation, "baseline-invalid", "$.baseline");
            bool same = previous.Artifact!.Bytes.Span.SequenceEqual(output.Bytes.Span);
            var facts = new SetupCliMachineCoverage(same ? [artifact.Id] : [], same ? [] : [artifact.Id]);
            return SetupCliResults.Success(coverage: facts);
        }
        if (command.Operation == "coverage")
            return SetupCliResults.Success(coverage: coverage);

        bool write = command.Operation is "create" or "format" or "export" or "import" or "preview" or "render";
        if (write)
        {
            if (command.Output == "-")
                return SetupCliResults.Failure(SetupCliExitCode.Blocked, "restricted-stdout", "$.output");
            SetupCliIoOperations.Write(invocation, command,
                artifact.Format == "dotenv" ? SetupArtifactKind.Environment : SetupArtifactKind.Configuration, output.Bytes);
        }
        var hostReadiness = new SetupCliMachineReadiness("incomplete", ["host-validation-required"], []);
        return SetupCliResults.Success([SetupCliResults.Artifact("definition-artifact", output.MediaType, output.Bytes.Span,
            "restricted", write ? SetupCliResults.PathIntent(command.Output) : "input",
            write ? command.DryRun ? "planned" : "written" : "none", coverage, hostReadiness)], coverage, hostReadiness);
    }

    internal static SetupCliCommandResult Doctor()
    {
        if (!new ProtectedArtifactWriter().IsAvailable)
            return SetupCliResults.Failure(SetupCliExitCode.Blocked, "protected-writer-unavailable");
        byte[] schema = SetupCliCommandSchemaMetadata.GenerateSchema();
        return SetupCliResults.Success([SetupCliResults.Artifact("doctor-report", "application/json", schema,
            "public", "none", "none")]);
    }

    private static bool CopiesRevision(SetupDefinition definition, string artifactId, string field)
    {
        using JsonDocument document = JsonDocument.Parse(definition.CanonicalBytes);
        JsonElement projection = document.RootElement.GetProperty("artifacts").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == artifactId).GetProperty("projection");
        // An unconditional top-level copy ensures the option is actually emitted.
        return projection.GetProperty("op").GetString() == "object"
            && projection.GetProperty("members").EnumerateObject().Any(member =>
                member.Value.GetProperty("op").GetString() == "copy"
                && member.Value.GetProperty("field").GetString() == field
                && !member.Value.TryGetProperty("when", out _))
            && !projection.TryGetProperty("when", out _);
    }

    private static bool Equivalent(string format, ReadOnlyMemory<byte> source, ReadOnlyMemory<byte> expected)
    {
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
        SetupCompositionResult candidateResult = compiler.CompileAsync(candidateSource).AsTask().GetAwaiter().GetResult();
        SetupCompositionResult referenceResult = compiler.CompileAsync(referenceSource).AsTask().GetAwaiter().GetResult();
        if (!candidateResult.Succeeded || !referenceResult.Succeeded) throw new JsonException();
        return JsonElement.DeepEquals(candidateResult.Artifact!.Value, referenceResult.Artifact!.Value);
    }
}

internal static class SetupCliIoOperations
{
    internal static byte[] Read(SetupCliInvocation invocation, string path)
    {
        ReadOnlyMemory<byte> bytes = invocation.Io.Input.Read(path, invocation.Io.MaximumBytes);
        if (bytes.Length > invocation.Io.MaximumBytes) throw new IOException("input-bound");
        return bytes.ToArray();
    }

    internal static SetupDefinition Definition(SetupCliInvocation invocation, string path)
    {
        byte[] bytes = Read(invocation, path);
        return Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml"
            ? SetupDefinitionCodec.ParseYaml(bytes) : SetupDefinitionCodec.ParseJson(bytes);
    }

    internal static SetupAnswerSnapshot Answers(SetupCliInvocation invocation, string path, string revision)
    {
        using JsonDocument document = JsonDocument.Parse(Read(invocation, path),
            new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException();
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
            if (!values.TryAdd(property.Name, property.Value.Clone())) throw new JsonException();
        return new(long.Parse(revision, CultureInfo.InvariantCulture), values);
    }

    internal static void Write(SetupCliInvocation invocation, SetupCliCommand command,
        SetupArtifactKind kind, ReadOnlyMemory<byte> bytes)
    {
        if (!command.DryRun)
            invocation.Io.Output.WriteArtifact(command.Output!, kind, bytes, invocation.Io.MaximumBytes);
    }
}
