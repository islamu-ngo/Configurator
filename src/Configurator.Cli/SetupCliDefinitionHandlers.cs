using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Cli;

internal static class SetupCliDefinitionHandlers
{
    private static readonly string[] SourceNames = ["configurator.yaml", "configurator.yml", "configurator.json"];
    private static readonly string[] SecretTerms = ["TOKEN", "PASSWORD", "SECRET", "CREDENTIAL", "PRIVATE_KEY", "API_KEY", "CONNECTION_STRING"];
    internal static async Task<SetupCliCommandResult> ExecuteAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (command.Operation == "init") return await InitializeAsync(command, invocation, ledger, cancellationToken).ConfigureAwait(false);
            ISetupCliAuthoringFiles files = invocation.Io.AuthoringFiles;
            int maximumBytes = invocation.Io.MaximumBytes;
            string source = await files.DiscoverSourceAsync(command.Root ?? ".", command.Source, cancellationToken).ConfigureAwait(false);
            using SetupCliInputBuffer input = await files.ReadAsync(source, maximumBytes, cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> bytes = input.Bytes;
            bool yaml = IsYaml(source);
            DefinitionAuthoringDocument document = Parse(bytes, yaml, cancellationToken);
            SetupDefinition definition = DefinitionAuthoringCompiler.Compile(document);
            cancellationToken.ThrowIfCancellationRequested();
            string anchor = Anchor(source, definition.Revision);
            if (command.Operation == "revise")
            {
                string priorPath = command.Against ?? anchor;
                if (!await files.ExistsAsync(priorPath, cancellationToken).ConfigureAwait(false)) throw new DefinitionFileException("definition-anchor-missing");
                using SetupCliInputBuffer priorInput = await files.ReadAsync(priorPath, maximumBytes, cancellationToken).ConfigureAwait(false);
                SetupDefinition prior = SetupDefinitionCodec.ParseJson(priorInput.Bytes);
                cancellationToken.ThrowIfCancellationRequested();
                if (prior.FamilyId != definition.FamilyId || prior.Revision != definition.Revision)
                    throw new DefinitionFileException("definition-anchor-identity");
                await VerifyAnchorAsync(files, maximumBytes, anchor, prior, false, cancellationToken).ConfigureAwait(false);
                if (prior.Digest == definition.Digest) return Receipt("authoring-source", yaml, bytes, "none", definition.Digest.Value);
                if (definition.Revision == long.MaxValue) throw new DefinitionFileException("definition-revision-overflow");
                if (string.IsNullOrEmpty(command.Output)) return SetupCliResults.Failure(SetupCliExitCode.Usage, "output-required");
                RejectSourceDestination(source, command.Output);
                if (IsYaml(command.Output) != yaml) return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
                byte[] revised = DefinitionAuthoringIdentityEditor.UpdateRevision(bytes, yaml, definition.Revision + 1);
                SetupDefinition revisedDefinition = DefinitionAuthoringCompiler.Compile(Parse(revised, yaml, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                SetupCliCommandResult revisedResult = Receipt("authoring-source", yaml, revised, "not-attempted", revisedDefinition.Digest.Value);
                ledger.Plan(invocation, command, revisedResult);
                using ISetupCliPublicFilePreparation candidate = files.PrepareCreate(command.Output, revised, maximumBytes, false);
                return await PublishAsync([candidate], revisedResult, ledger, cancellationToken).ConfigureAwait(false);
            }
            await VerifyAnchorAsync(files, maximumBytes, anchor, definition, false, cancellationToken).ConfigureAwait(false);
            if (command.Against is not null) await VerifyAnchorAsync(files, maximumBytes, command.Against, definition, true, cancellationToken).ConfigureAwait(false);
            if (command.Operation == "check")
                return Receipt("compiled-definition", false, definition.CanonicalBytes, "none", definition.Digest.Value, "input");
            string destination = command.Output ?? anchor;
            RejectSourceDestination(source, destination);
            if (command.IdMap is not null)
            {
                RejectSourceDestination(source, command.IdMap);
                if (Path.GetFullPath(command.IdMap) == Path.GetFullPath(destination))
                    throw new DefinitionFileException("definition-output-conflict");
            }
            var artifacts = new List<SetupCliMachineArtifact>
            {
                SetupCliResults.Artifact("compiled-definition", "application/json", definition.CanonicalBytes.Span,
                    "public", "output", "not-attempted")
            };
            byte[]? map = null;
            if (command.IdMap is not null)
            {
                JsonObject mapping = new();
                foreach (JsonProperty field in document.Root.GetProperty("fields").EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    mapping[field.Name] = DefinitionAuthoringCompiler.FieldId(field.Name);
                }
                map = Encoding.UTF8.GetBytes(mapping.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
                artifacts.Add(SetupCliResults.Artifact("logical-id-map", "application/json", map, "public", "output", "not-attempted"));
            }
            SetupCliCommandResult result = SetupCliResults.Success(artifacts) with { DefinitionDigest = definition.Digest.Value, Preparation = "ready" };
            ledger.Plan(invocation, command, result);
            using ISetupCliPublicFilePreparation canonical = files.PrepareCreate(destination, definition.CanonicalBytes, maximumBytes, true);
            using ISetupCliPublicFilePreparation? idMap = command.IdMap is null ? null : files.PrepareCreate(command.IdMap, map!, maximumBytes, true);
            return await PublishAsync(idMap is null ? [canonical] : [canonical, idMap], result, ledger, cancellationToken).ConfigureAwait(false);
        }
        catch (DefinitionFileException exception) { return ledger.Preserve(SetupCliResults.Failure(SetupCliExitCode.Data, exception.Code, "$.source")); }
        catch (DefinitionSchemaImportException exception)
        {
            var diagnostics = new[] { new SetupCliMachineDiagnostic(exception.Code, "$.schema", "error") }
                .Concat(exception.Differences.Select(code => new SetupCliMachineDiagnostic(code, "$.schema", "warning"))).ToArray();
            return new(SetupCliExitCode.Data, diagnostics, [], SetupCliResults.EmptyCoverage(), new("incomplete", ["schema-review-required"], []));
        }
        catch (SetupDefinitionException) { return SetupCliResults.Failure(SetupCliExitCode.Data, "definition-source-invalid", "$.source"); }
        catch (DecoderFallbackException) { return SetupCliResults.Failure(SetupCliExitCode.Data, "definition-source-invalid", "$.source"); }
    }

    private static async Task<SetupCliCommandResult> InitializeAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        string root = command.Root ?? ".";
        ISetupCliAuthoringFiles files = invocation.Io.AuthoringFiles;
        int maximumBytes = invocation.Io.MaximumBytes;
        if (command.ForkFrom is not null)
        {
            if (command.Output is null || command.FromSchema is not null || command.FromEnvExample is not null)
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-fork-options");
            using SetupCliInputBuffer original = await files.ReadAsync(command.ForkFrom, maximumBytes, cancellationToken).ConfigureAwait(false);
            bool yaml = IsYaml(command.ForkFrom);
            if (command.Format is not null && command.Format != (yaml ? "yaml" : "json") || IsYaml(command.Output) != yaml)
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
            RejectSourceDestination(command.ForkFrom, command.Output, false);
            byte[] fork = DefinitionAuthoringIdentityEditor.Fork(original.Bytes, yaml, Guid.NewGuid());
            SetupDefinition definition = DefinitionAuthoringCompiler.Compile(Parse(fork, yaml, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            SetupCliCommandResult forkResult = Receipt("authoring-source", yaml, fork, "not-attempted", definition.Digest.Value);
            ledger.Plan(invocation, command, forkResult);
            using ISetupCliPublicFilePreparation preparedFork = files.PrepareCreate(command.Output, fork, maximumBytes, false);
            return await PublishAsync([preparedFork], forkResult, ledger, cancellationToken).ConfigureAwait(false);
        }
        if (command.ProjectId is null || command.ProjectName is null || command.ApplicationVersion is null)
            return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-project-metadata-required");
        if (command.FromSchema is not null && command.FromEnvExample is not null)
            return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-scaffold-conflict");
        if (command.AcceptBoundedSemantics && command.FromSchema is null)
            return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-schema-required");
        string format = command.Format ?? (command.Output is null || IsYaml(command.Output) ? "yaml" : "json");
        if (format is not ("yaml" or "json")) return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
        string output = command.Output ?? Path.Combine(root, format == "yaml" ? "configurator.yaml" : "configurator.json");
        if (command.Output is null)
            foreach (string name in SourceNames)
                if (await files.ExistsAsync(Path.Combine(root, name), cancellationToken).ConfigureAwait(false))
                    throw new DefinitionFileException("definition-output-conflict");
        if (IsYaml(output) != (format == "yaml")) return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
        JsonObject fields = new();
        IReadOnlyList<string> differences = [];
        if (command.FromSchema is not null)
        {
            DefinitionSchemaImportResult imported = await DefinitionSchemaSourceFiles.ImportAsync(files, maximumBytes,
                command.FromSchema, command.AcceptBoundedSemantics, cancellationToken).ConfigureAwait(false);
            fields = JsonNode.Parse(imported.Fields.GetRawText())!.AsObject();
            differences = imported.Differences;
        }
        else if (command.FromEnvExample is not null)
        {
            using SetupCliInputBuffer example = await files.ReadAsync(command.FromEnvExample, maximumBytes, cancellationToken).ConfigureAwait(false);
            fields = EnvironmentKeys(example.Bytes, cancellationToken);
        }
        JsonObject source = new()
        {
            ["$schema"] = DefinitionAuthoringCodec.SchemaId,
            ["identity"] = new JsonObject { ["familyId"] = Guid.NewGuid().ToString("D"), ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = command.ProjectId, ["name"] = command.ProjectName },
            ["applicationVersions"] = new JsonArray(command.ApplicationVersion),
            ["fields"] = fields,
            ["outputs"] = new JsonObject
            {
                [command.FromEnvExample is null ? "config.json" : ".env"] = new JsonObject { ["format"] = command.FromEnvExample is null ? "json" : "dotenv" }
            }
        };
        byte[] bytes = Serialize(source, format == "yaml", cancellationToken);
        SetupDefinition compiled = DefinitionAuthoringCompiler.Compile(Parse(bytes, format == "yaml", cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        SetupCliCommandResult result = Receipt("authoring-source", format == "yaml", bytes, "not-attempted", compiled.Digest.Value) with
        { Diagnostics = differences.Select(code => new SetupCliMachineDiagnostic(code, "$.schema", "warning")).ToArray() };
        ledger.Plan(invocation, command, result);
        using ISetupCliPublicFilePreparation preparedSource = files.PrepareCreate(output, bytes, maximumBytes, false);
        return await PublishAsync([preparedSource], result, ledger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SetupCliCommandResult> PublishAsync(ISetupCliPublicFilePreparation[] prepared,
        SetupCliCommandResult result, SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        bool cleanupFailed = false;
        try
        {
            foreach (ISetupCliPublicFilePreparation artifact in prepared)
                await artifact.StageAsync(cancellationToken).ConfigureAwait(false);
            for (int index = 0; index < prepared.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ledger.Record(index, "unknown");
                try { ledger.Record(index, await prepared[index].CommitAsync(cancellationToken).ConfigureAwait(false) ? "written" : "none"); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Commit checks cancellation only before its final synchronous rename.
                    ledger.Record(index, "not-attempted");
                    throw;
                }
                catch (DefinitionFileException exception)
                {
                    ledger.Record(index, exception.Code is "definition-output-conflict" or "definition-source-changed" ? "conflict" : "blocked");
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            foreach (ISetupCliPublicFilePreparation artifact in prepared)
            {
                try { artifact.Dispose(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { cleanupFailed = true; }
            }
            ledger.Cleanup(cleanupFailed ? "failed" : "completed");
        }
        return ledger.Preserve(cleanupFailed ? SetupCliResults.Failure(SetupCliExitCode.Io, "definition-cleanup-failed", "$.source") : result);
    }

    private static JsonObject EnvironmentKeys(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fields = new JsonObject();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string text = new UTF8Encoding(false, true).GetString(bytes.Span);
        foreach (string line in text.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            int equals = trimmed.IndexOf('=');
            string key = equals < 0 ? "" : trimmed[..equals];
            if (key.StartsWith("export ", StringComparison.Ordinal)) key = key[7..];
            if (key.Length is < 1 or > 128 || !(char.IsAsciiLetter(key[0]) || key[0] == '_')
                || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')
                || !keys.Add(key) || keys.Count > 1024)
                throw new DefinitionFileException("definition-env-example-invalid");
            bool secret = SecretTerms.Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase));
            fields[key] = new JsonObject { ["type"] = "string", ["sensitivity"] = secret ? "secret" : "sensitive" };
        }
        return fields;
    }

    private static byte[] Serialize(JsonObject source, bool yaml, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!yaml) return Encoding.UTF8.GetBytes(source.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        using JsonDocument document = JsonDocument.Parse(source.ToJsonString());
        var text = new StringBuilder();
        WriteYaml(document.RootElement, text, 0, cancellationToken);
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static void WriteYaml(JsonElement value, StringBuilder text, int indentation, CancellationToken cancellationToken)
    {
        foreach (JsonProperty property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.Append(' ', indentation).Append(JsonSerializer.Serialize(property.Name)).Append(':');
            if (property.Value.ValueKind == JsonValueKind.Object && property.Value.EnumerateObject().Any())
            {
                text.Append('\n');
                WriteYaml(property.Value, text, indentation + 2, cancellationToken);
            }
            else text.Append(' ').Append(property.Value.GetRawText()).Append('\n');
        }
    }

    private static async ValueTask VerifyAnchorAsync(ISetupCliAuthoringFiles files, int maximumBytes,
        string anchor, SetupDefinition definition, bool required,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await files.ExistsAsync(anchor, cancellationToken).ConfigureAwait(false))
        {
            if (required) throw new DefinitionFileException("definition-anchor-missing");
            return;
        }
        using SetupCliInputBuffer input = await files.ReadAsync(anchor, maximumBytes, cancellationToken).ConfigureAwait(false);
        SetupDefinition prior = SetupDefinitionCodec.ParseJson(input.Bytes);
        cancellationToken.ThrowIfCancellationRequested();
        if (prior.FamilyId != definition.FamilyId || prior.Revision != definition.Revision || prior.Digest != definition.Digest)
            throw new DefinitionFileException("definition-revision-conflict");
    }

    private static void RejectSourceDestination(string source, string destination, bool rejectDiscoveryCandidates = true)
    {
        if (destination == "-") throw new DefinitionFileException("definition-local-file-required");
        string path = Path.GetFullPath(destination);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (path.Equals(Path.GetFullPath(source), comparison)
            || rejectDiscoveryCandidates && SourceNames.Any(name => name.Equals(Path.GetFileName(path), comparison)))
            throw new DefinitionFileException("definition-output-source-conflict");
    }

    private static string Anchor(string source, long revision) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!,
        "dist", "configurator", "revisions", revision.ToString(CultureInfo.InvariantCulture) + ".json");
    private static DefinitionAuthoringDocument Parse(ReadOnlyMemory<byte> bytes, bool yaml, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DefinitionAuthoringDocument document = yaml ? DefinitionAuthoringCodec.ParseYaml(bytes) : DefinitionAuthoringCodec.ParseJson(bytes);
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }
    private static bool IsYaml(string path) => Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml";
    private static SetupCliCommandResult Receipt(string kind, bool yaml, ReadOnlyMemory<byte> bytes, string status, string digest, string intent = "output") =>
        SetupCliResults.Success([SetupCliResults.Artifact(kind, yaml ? "application/yaml" : "application/json", bytes.Span, "public", intent, status)])
            with
        { DefinitionDigest = digest, Preparation = "ready" };
}
