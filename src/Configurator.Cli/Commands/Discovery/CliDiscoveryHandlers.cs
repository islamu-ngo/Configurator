using System.Reflection;
using System.Text;
using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Cli;

internal static class CliDiscoveryHandlers
{
    internal const string StoreCurationSchemaId = "urn:islamu:configurator:store-curation:v" + ConfiguratorVersion.Current;
    private const string StoreCurationResourceName = "Configurator.Cli.Schemas.definition-store-curation.schema.json";

    internal static readonly string[] OrderedSchemaKinds =
    [
        "answers", "authoring", "command", "fixtures", "runtime", "store-curation"
    ];

    internal static bool IsSupportedSchemaKind(string kind) =>
        kind is "answers" or "authoring" or "command" or "fixtures" or "runtime" or "store-curation";

    internal static CliCommandResult Version(bool protectedWriteAvailable)
    {
        CliMachineInfo info = BuildInfo(protectedWriteAvailable);
        return CliResults.Success() with
        {
            Info = info,
            RawTextOutput = CliBuildInfo.ApplicationName + " " + info.Version + "\n"
        };
    }

    internal static CliCommandResult Info(bool protectedWriteAvailable) =>
        CliResults.Success() with
        {
            Info = BuildInfo(protectedWriteAvailable),
            Preparation = "ready"
        };

    internal static CliCommandResult Doctor(CliCommand command, bool protectedWriteAvailable)
    {
        CliMachineInfo info = BuildInfo(protectedWriteAvailable);
        bool requiresProtectedWrite = command.RequiredCapabilities is { Length: > 0 } required
            && required.Contains("protected-write", StringComparer.Ordinal);

        if (requiresProtectedWrite && !protectedWriteAvailable)
        {
            return CliResults.Failure(CliExitCode.Blocked, "protected-writer-unavailable") with
            {
                Info = info
            };
        }

        byte[] reportBytes = JsonSerializer.SerializeToUtf8Bytes(info, CliJsonContext.Default.CliMachineInfo);
        CliMachineArtifact report = CliResults.Artifact("doctor-report", "application/json", reportBytes,
            "public", "none", "none");
        CliMachineDiagnostic[] diagnostics = protectedWriteAvailable
            ? []
            : [new CliMachineDiagnostic("protected-writer-unavailable", "", "info")];
        CliMachineReadiness readiness = protectedWriteAvailable
            ? CliResults.Ready()
            : new CliMachineReadiness("blocked", [], ["protected-writer-unavailable"]);

        return CliResults.Success([report], readiness: readiness) with
        {
            Diagnostics = diagnostics,
            Info = info,
            Preparation = "ready"
        };
    }

    internal static async Task<CliCommandResult> SchemaAsync(
        CliCommand command,
        CliInvocation invocation,
        CliOutcomeLedger ledger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Operation == "list")
            return SchemaList();
        if (command.Operation == "export")
            return await SchemaExportAsync(command, invocation, ledger, cancellationToken).ConfigureAwait(false);
        return CliResults.Failure(CliExitCode.Usage, "operation-unknown");
    }

    internal static CliMachineInfo BuildInfo(bool protectedWriteAvailable) =>
        new(
            CliBuildInfo.ApplicationName,
            CliBuildInfo.Version,
            DefinitionProfile.Id,
            ConfiguratorVersion.Current,
            DefinitionProfile.RuntimeSchemaVersion,
            AnswerSnapshotCodec.ContractVersion,
            DefinitionFixtureDocument.ContractVersion,
            CliCommandSchemaMetadata.EnvelopeVersion,
            protectedWriteAvailable,
            BuildSchemaDescriptors(),
            DefinitionProfile.Capabilities);

    private static CliCommandResult SchemaList() =>
        CliResults.Success() with
        {
            SchemaCatalog = new CliMachineSchemaCatalog(BuildSchemaDescriptors()),
            Preparation = "ready"
        };

    private static async Task<CliCommandResult> SchemaExportAsync(
        CliCommand command,
        CliInvocation invocation,
        CliOutcomeLedger ledger,
        CancellationToken cancellationToken)
    {
        (string id, string fileName, byte[] rawBytes) = LoadSchema(command.SchemaKind);
        byte[] canonical = DefinitionProfile.CanonicalizeJson(rawBytes);
        string digest = ArtifactDigest.Compute(canonical).Value;
        using JsonDocument document = JsonDocument.Parse(canonical);

        var machineSchema = new CliMachineSchema(
            command.SchemaKind,
            id,
            digest,
            DefinitionProfile.Id,
            ConfiguratorVersion.Current,
            DefinitionProfile.RuntimeSchemaVersion,
            AnswerSnapshotCodec.ContractVersion,
            DefinitionProfile.Capabilities,
            document.RootElement.Clone());

        if (string.IsNullOrEmpty(command.Output) || command.Output == "-")
        {
            return CliResults.Success() with
            {
                Schema = machineSchema,
                DefinitionDigest = digest,
                Preparation = "ready",
                RawTextOutput = Encoding.UTF8.GetString(rawBytes)
            };
        }

        CliMachineArtifact artifact = CliResults.Artifact(
            "schema-document",
            "application/schema+json",
            rawBytes,
            "public",
            "output",
            command.DryRun ? "planned" : "not-attempted") with
        {
            Id = command.SchemaKind + "-schema"
        };

        CliCommandResult planned = CliResults.Success([artifact]) with
        {
            Schema = machineSchema,
            DefinitionDigest = digest,
            Preparation = "ready"
        };

        if (command.DryRun)
            return planned;

        ledger.Plan(invocation, command, planned);
        cancellationToken.ThrowIfCancellationRequested();
        ledger.BeginPublication();
        CliPublicationResult publication = await invocation.Io.Output.WriteArtifactAsync(
            command.Output,
            ArtifactKind.PublicTemplate,
            rawBytes,
            invocation.Io.MaximumBytes,
            cancellationToken).ConfigureAwait(false);
        return ledger.Apply(publication, planned);
    }

    internal static IReadOnlyList<CliMachineSchemaDescriptor> BuildSchemaDescriptors()
    {
        var descriptors = new CliMachineSchemaDescriptor[OrderedSchemaKinds.Length];
        for (int index = 0; index < OrderedSchemaKinds.Length; index++)
        {
            string kind = OrderedSchemaKinds[index];
            (string id, string fileName, byte[] rawBytes) = LoadSchema(kind);
            byte[] canonical = DefinitionProfile.CanonicalizeJson(rawBytes);
            string digest = ArtifactDigest.Compute(canonical).Value;
            descriptors[index] = new CliMachineSchemaDescriptor(kind, id, fileName, digest);
        }
        return Array.AsReadOnly(descriptors);
    }

    private static (string Id, string FileName, byte[] RawBytes) LoadSchema(string kind) => kind switch
    {
        "authoring" => (DefinitionAuthoringCodec.SchemaId, "configurator-authoring.schema.json",
            Encoding.UTF8.GetBytes(DefinitionAuthoringSchema.Generate())),
        "runtime" => ("urn:islamu:configurator:setup-definition:v" + ConfiguratorVersion.Current, "setup-definition.schema.json",
            Encoding.UTF8.GetBytes(DefinitionSchema.Generate())),
        "answers" => ("urn:islamu:configurator:answers:v" + ConfiguratorVersion.Current, "configurator-answers.schema.json",
            Encoding.UTF8.GetBytes(DefinitionSchema.GenerateAnswers())),
        "command" => (CliCommandSchemaMetadata.SchemaId, "configurator-cli.schema.json",
            CliCommandSchemaMetadata.GenerateSchema()),
        "fixtures" => (DefinitionFixtureDocument.SchemaId, "configurator-fixtures.schema.json",
            Encoding.UTF8.GetBytes(DefinitionFixtureSchema.Generate())),
        "store-curation" => (StoreCurationSchemaId, "definition-store-curation.schema.json",
            ReadEmbeddedStoreCurationSchema()),
        _ => throw new InvalidOperationException("schema-kind-unknown")
    };

    private static byte[] ReadEmbeddedStoreCurationSchema()
    {
        Assembly assembly = typeof(CliDiscoveryHandlers).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(StoreCurationResourceName)
            ?? throw new InvalidOperationException("store-curation-schema-missing");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
