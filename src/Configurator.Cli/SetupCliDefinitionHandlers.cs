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
    internal static SetupCliCommandResult Execute(SetupCliCommand command, SetupCliInvocation invocation, SetupCliOutcomeLedger ledger)
    {
        try
        {
            if (command.Operation == "init") return Initialize(command, invocation, ledger);
            string source = DefinitionSourceFiles.Discover(command.Root ?? ".", command.Source);
            byte[] bytes = DefinitionSourceFiles.Read(source);
            bool yaml = IsYaml(source);
            DefinitionAuthoringDocument document = Parse(bytes, yaml);
            SetupDefinition definition = DefinitionAuthoringCompiler.Compile(document);
            string anchor = Anchor(source, definition.Revision);
            if (command.Operation == "revise")
            {
                string priorPath = command.Against ?? anchor;
                if (!File.Exists(priorPath)) throw new DefinitionFileException("definition-anchor-missing");
                SetupDefinition prior = SetupDefinitionCodec.ParseJson(DefinitionSourceFiles.Read(priorPath));
                if (prior.FamilyId != definition.FamilyId || prior.Revision != definition.Revision)
                    throw new DefinitionFileException("definition-anchor-identity");
                VerifyAnchor(anchor, prior, false);
                if (prior.Digest == definition.Digest) return Receipt("authoring-source", yaml, bytes, "none", definition.Digest.Value);
                if (definition.Revision == long.MaxValue) throw new DefinitionFileException("definition-revision-overflow");
                byte[] revised = DefinitionAuthoringIdentityEditor.UpdateRevision(bytes, yaml, definition.Revision + 1);
                SetupDefinition revisedDefinition = DefinitionAuthoringCompiler.Compile(Parse(revised, yaml));
                SetupCliCommandResult revisedResult = Receipt("authoring-source", yaml, revised, "not-attempted", revisedDefinition.Digest.Value);
                ledger.Plan(invocation, command, revisedResult);
                using DefinitionSourceFiles.PreparedArtifact replacement = DefinitionSourceFiles.PrepareReplace(source, bytes, revised);
                return Publish([replacement], revisedResult, ledger);
            }
            VerifyAnchor(anchor, definition, false);
            if (command.Against is not null) VerifyAnchor(command.Against, definition, true);
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
                    mapping[field.Name] = DefinitionAuthoringCompiler.FieldId(field.Name);
                map = Encoding.UTF8.GetBytes(mapping.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
                artifacts.Add(SetupCliResults.Artifact("logical-id-map", "application/json", map, "public", "output", "not-attempted"));
            }
            SetupCliCommandResult result = SetupCliResults.Success(artifacts) with { DefinitionDigest = definition.Digest.Value, Preparation = "ready" };
            ledger.Plan(invocation, command, result);
            using DefinitionSourceFiles.PreparedArtifact canonical = DefinitionSourceFiles.PrepareCreate(destination, definition.CanonicalBytes, true);
            using DefinitionSourceFiles.PreparedArtifact? idMap = command.IdMap is null ? null : DefinitionSourceFiles.PrepareCreate(command.IdMap, map!, true);
            return Publish(idMap is null ? [canonical] : [canonical, idMap], result, ledger);
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

    private static SetupCliCommandResult Initialize(SetupCliCommand command, SetupCliInvocation invocation, SetupCliOutcomeLedger ledger)
    {
        string root = command.Root ?? ".";
        if (command.ForkFrom is not null)
        {
            if (command.Output is null || command.FromSchema is not null || command.FromEnvExample is not null)
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-fork-options");
            byte[] original = DefinitionSourceFiles.Read(command.ForkFrom);
            bool yaml = IsYaml(command.ForkFrom);
            if (command.Format is not null && command.Format != (yaml ? "yaml" : "json") || IsYaml(command.Output) != yaml)
                return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
            RejectSourceDestination(command.ForkFrom, command.Output, false);
            byte[] fork = DefinitionAuthoringIdentityEditor.Fork(original, yaml, Guid.NewGuid());
            SetupDefinition definition = DefinitionAuthoringCompiler.Compile(Parse(fork, yaml));
            SetupCliCommandResult forkResult = Receipt("authoring-source", yaml, fork, "not-attempted", definition.Digest.Value);
            ledger.Plan(invocation, command, forkResult);
            using DefinitionSourceFiles.PreparedArtifact preparedFork = DefinitionSourceFiles.PrepareCreate(command.Output, fork, false);
            return Publish([preparedFork], forkResult, ledger);
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
        if (command.Output is null && SourceNames.Any(name => File.Exists(Path.Combine(root, name))))
            throw new DefinitionFileException("definition-output-conflict");
        if (IsYaml(output) != (format == "yaml")) return SetupCliResults.Failure(SetupCliExitCode.Usage, "definition-source-format");
        JsonObject fields = new();
        IReadOnlyList<string> differences = [];
        if (command.FromSchema is not null)
        {
            DefinitionSchemaImportResult imported = DefinitionSchemaSourceFiles.Import(command.FromSchema, command.AcceptBoundedSemantics);
            fields = JsonNode.Parse(imported.Fields.GetRawText())!.AsObject();
            differences = imported.Differences;
        }
        else if (command.FromEnvExample is not null)
            fields = EnvironmentKeys(DefinitionSourceFiles.Read(command.FromEnvExample));
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
        byte[] bytes = Serialize(source, format == "yaml");
        SetupDefinition compiled = DefinitionAuthoringCompiler.Compile(Parse(bytes, format == "yaml"));
        SetupCliCommandResult result = Receipt("authoring-source", format == "yaml", bytes, "not-attempted", compiled.Digest.Value) with
        { Diagnostics = differences.Select(code => new SetupCliMachineDiagnostic(code, "$.schema", "warning")).ToArray() };
        ledger.Plan(invocation, command, result);
        using DefinitionSourceFiles.PreparedArtifact preparedSource = DefinitionSourceFiles.PrepareCreate(output, bytes, false);
        return Publish([preparedSource], result, ledger);
    }

    private static SetupCliCommandResult Publish(DefinitionSourceFiles.PreparedArtifact[] prepared,
        SetupCliCommandResult result, SetupCliOutcomeLedger ledger)
    {
        bool cleanupFailed = false;
        try
        {
            foreach (DefinitionSourceFiles.PreparedArtifact artifact in prepared) artifact.Stage();
            for (int index = 0; index < prepared.Length; index++)
            {
                ledger.Record(index, "unknown");
                try { ledger.Record(index, prepared[index].Commit() ? "written" : "none"); }
                catch (DefinitionFileException exception)
                {
                    ledger.Record(index, exception.Code is "definition-output-conflict" or "definition-source-changed" ? "conflict" : "blocked");
                    throw;
                }
            }
        }
        finally
        {
            foreach (DefinitionSourceFiles.PreparedArtifact artifact in prepared)
            {
                try { artifact.Dispose(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { cleanupFailed = true; }
            }
            ledger.Cleanup(cleanupFailed ? "failed" : "completed");
        }
        return ledger.Preserve(cleanupFailed ? SetupCliResults.Failure(SetupCliExitCode.Io, "definition-cleanup-failed", "$.source") : result);
    }

    private static JsonObject EnvironmentKeys(byte[] bytes)
    {
        var fields = new JsonObject();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        foreach (string line in text.Split('\n'))
        {
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

    private static byte[] Serialize(JsonObject source, bool yaml)
    {
        if (!yaml) return Encoding.UTF8.GetBytes(source.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        using JsonDocument document = JsonDocument.Parse(source.ToJsonString());
        var text = new StringBuilder();
        WriteYaml(document.RootElement, text, 0);
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static void WriteYaml(JsonElement value, StringBuilder text, int indentation)
    {
        foreach (JsonProperty property in value.EnumerateObject())
        {
            text.Append(' ', indentation).Append(JsonSerializer.Serialize(property.Name)).Append(':');
            if (property.Value.ValueKind == JsonValueKind.Object && property.Value.EnumerateObject().Any())
            {
                text.Append('\n');
                WriteYaml(property.Value, text, indentation + 2);
            }
            else text.Append(' ').Append(property.Value.GetRawText()).Append('\n');
        }
    }

    private static void VerifyAnchor(string anchor, SetupDefinition definition, bool required)
    {
        if (!File.Exists(anchor))
        {
            if (required) throw new DefinitionFileException("definition-anchor-missing");
            return;
        }
        SetupDefinition prior = SetupDefinitionCodec.ParseJson(DefinitionSourceFiles.Read(anchor));
        if (prior.FamilyId != definition.FamilyId || prior.Revision != definition.Revision || prior.Digest != definition.Digest)
            throw new DefinitionFileException("definition-revision-conflict");
    }

    private static void RejectSourceDestination(string source, string destination, bool rejectDiscoveryCandidates = true)
    {
        string path = DefinitionSourceFiles.SafePath(destination);
        if (path == DefinitionSourceFiles.SafePath(source)
            || rejectDiscoveryCandidates && SourceNames.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            throw new DefinitionFileException("definition-output-source-conflict");
    }

    private static string Anchor(string source, long revision) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!,
        "dist", "configurator", "revisions", revision.ToString(CultureInfo.InvariantCulture) + ".json");
    private static DefinitionAuthoringDocument Parse(ReadOnlyMemory<byte> bytes, bool yaml) =>
        yaml ? DefinitionAuthoringCodec.ParseYaml(bytes) : DefinitionAuthoringCodec.ParseJson(bytes);
    private static bool IsYaml(string path) => Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml";
    private static SetupCliCommandResult Receipt(string kind, bool yaml, ReadOnlyMemory<byte> bytes, string status, string digest, string intent = "output") =>
        SetupCliResults.Success([SetupCliResults.Artifact(kind, yaml ? "application/yaml" : "application/json", bytes.Span, "public", intent, status)])
            with { DefinitionDigest = digest, Preparation = "ready" };
}
