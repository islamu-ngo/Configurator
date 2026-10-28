using System.Text.Json;
using System.Text.Json.Serialization;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli;

public enum SetupCliMode { Text, Machine }
public enum SetupCliExitCode { Success = 0, Validation = 2, Incomplete = 3, Blocked = 4, Usage = 64, Data = 65, Internal = 70, Io = 74, Cancelled = 130 }

public interface ISetupCliInput
{
    ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
}

/// <summary>Supplies an invocation's public authoring authority, independently of private answer I/O.</summary>
public interface ISetupCliAuthoringFiles
{
    ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken);
    ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    ISetupCliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical);
}

/// <summary>Separates staging, create-only publication and cleanup for truthful ordered receipts.</summary>
public interface ISetupCliPublicFilePreparation : IDisposable
{
    ValueTask StageAsync(CancellationToken cancellationToken);
    ValueTask<bool> CommitAsync(CancellationToken cancellationToken);
}

public interface ISetupCliWriter
{
    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
        ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    /// <summary>Reports established artifact outcomes synchronously before later work can fail; the result supplies final cleanup and cancellation facts.</summary>
    ValueTask<SetupCliPublicationResult> WriteBundleAsync(
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
        Action<string, SetupCliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken);
}

public sealed record SetupCliPublicationOutcome(ProtectedArtifactCommitResult? Result, bool Unknown = false,
    bool DeliveredToStandardOutput = false);
public sealed record SetupCliPublicationResult(IReadOnlyList<SetupCliPublicationOutcome> Outcomes,
    bool CleanupFailed = false, bool Cancelled = false);

public sealed record SetupCliIo
{
    public SetupCliIo(ISetupCliInput input, ISetupCliWriter output, ISetupCliWriter error,
        ISetupCliAuthoringFiles authoringFiles, int maximumOutputBytes, int maximumBytes)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Error = error ?? throw new ArgumentNullException(nameof(error));
        AuthoringFiles = authoringFiles ?? throw new ArgumentNullException(nameof(authoringFiles));
        if (maximumOutputBytes is < 1 or > 65_536 || maximumBytes is < 1 or > 4 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        MaximumOutputBytes = maximumOutputBytes;
        MaximumBytes = maximumBytes;
    }
    public ISetupCliInput Input { get; }
    public ISetupCliWriter Output { get; }
    public ISetupCliWriter Error { get; }
    public ISetupCliAuthoringFiles AuthoringFiles { get; }
    public int MaximumOutputBytes { get; }
    public int MaximumBytes { get; }
    public override string ToString() => $"{nameof(SetupCliIo)}:OutputBytes={MaximumOutputBytes}:Bytes={MaximumBytes}";
}

public sealed record SetupCliInvocation
{
    private readonly string[] _arguments;
    public SetupCliInvocation(IEnumerable<string> arguments, SetupCliMode mode, SetupCliIo io)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments.ToArray();
        if (_arguments.Any(argument => argument is null || argument.Length > 4096) || _arguments.Length > 256
            || _arguments.Sum(argument => System.Text.Encoding.UTF8.GetByteCount(argument) + 1) > 32_768)
            throw new ArgumentException("argument-bound-exceeded", nameof(arguments));
        Mode = mode;
        Io = io ?? throw new ArgumentNullException(nameof(io));
    }
    public IReadOnlyList<string> Arguments => Array.AsReadOnly((string[])_arguments.Clone());
    public SetupCliMode Mode { get; }
    public SetupCliIo Io { get; }
    public override string ToString() => $"{nameof(SetupCliInvocation)}:Mode={Mode}:Count={_arguments.Length}";
}

public sealed record SetupCliMachineInvocation(string CommandFamily, string Operation, string Mode);
public sealed record SetupCliMachineDiagnostic(string Code, string Path, string Severity)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Line { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Column { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ByteOffset { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SetupCliMachineSourceLocation? Source { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SetupCliMachineCorrectionLocation? Correction { get; init; }
}
public sealed record SetupCliMachineSourceLocation(string SchemaPath, int DocumentIndex,
    int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record SetupCliMachineLocationSegment(string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PropertyName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Index);
public sealed record SetupCliMachineCorrectionLocation(long InputRevision, string FieldId,
    IReadOnlyList<SetupCliMachineLocationSegment> Segments);
public sealed record SetupCliMachineSchema(string Kind, string Id, string Digest, string Profile,
    int AuthoringContractVersion, int RuntimeSchemaVersion, int AnswerContractVersion,
    IReadOnlyList<string> Capabilities, JsonElement Document);
public sealed record SetupCliMachineCoverage
{
    private readonly string[] _coveredKeys;
    private readonly string[] _missingKeys;
    public SetupCliMachineCoverage(IEnumerable<string> coveredKeys, IEnumerable<string> missingKeys)
    {
        _coveredKeys = Snapshot(coveredKeys);
        _missingKeys = Snapshot(missingKeys);
    }
    [JsonIgnore] public IReadOnlyList<string> CoveredKeys => Array.AsReadOnly((string[])_coveredKeys.Clone());
    [JsonIgnore] public IReadOnlyList<string> MissingKeys => Array.AsReadOnly((string[])_missingKeys.Clone());
    public int CoveredCount => _coveredKeys.Length;
    public int MissingCount => _missingKeys.Length;
    public string DetailDigest { get; init; } = "";
    private static string[] Snapshot(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
public sealed record SetupCliMachineReadiness
{
    private readonly string[] _missingKeys;
    private readonly string[] _blockedKeys;
    public SetupCliMachineReadiness(string state, IEnumerable<string> missingKeys, IEnumerable<string> blockedKeys)
    {
        State = state;
        _missingKeys = Snapshot(missingKeys);
        _blockedKeys = Snapshot(blockedKeys);
    }
    public string State { get; }
    [JsonIgnore] public IReadOnlyList<string> MissingKeys => Array.AsReadOnly((string[])_missingKeys.Clone());
    [JsonIgnore] public IReadOnlyList<string> BlockedKeys => Array.AsReadOnly((string[])_blockedKeys.Clone());
    public int MissingCount => _missingKeys.Length;
    public int BlockedCount => _blockedKeys.Length;
    private static string[] Snapshot(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
public sealed record SetupCliMachineArtifact(string Kind, string MediaType, string Digest, string Sensitivity,
    [property: JsonIgnore] SetupCliMachineCoverage Coverage,
    [property: JsonIgnore] SetupCliMachineReadiness Readiness, string PathIntent, string WriteStatus)
{
    public string Id { get; init; } = Kind;
    public string Installation { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
    public string Verification { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
}

public sealed record SetupCliMachineCoverageEntry(string Id, string State);
public sealed record SetupCliMachineDetail(string Kind, int Offset, int Limit, int Total, string Digest,
    IReadOnlyList<SetupCliMachineDiagnostic> Diagnostics, IReadOnlyList<SetupCliMachineCoverageEntry> Coverage);
public sealed record SetupCliMachineEnvelope
{
    private readonly SetupCliMachineDiagnostic[] _diagnostics;
    private readonly SetupCliMachineArtifact[] _artifacts;
    public SetupCliMachineEnvelope(string schemaVersion, SetupCliMachineInvocation invocation,
        string status, string exitCategory, int exitCode, bool dryRun,
        IEnumerable<SetupCliMachineDiagnostic> diagnostics, IEnumerable<SetupCliMachineArtifact> artifacts,
        SetupCliMachineCoverage coverage, SetupCliMachineReadiness readiness)
    {
        SchemaVersion = schemaVersion;
        Invocation = invocation;
        Status = status;
        ExitCategory = exitCategory;
        ExitCode = exitCode;
        DryRun = dryRun;
        _diagnostics = diagnostics.ToArray();
        _artifacts = artifacts.ToArray();
        Coverage = coverage;
        Readiness = readiness;
    }
    public string SchemaVersion { get; }
    public SetupCliMachineInvocation Invocation { get; }
    public string Status { get; }
    public string ExitCategory { get; }
    public int ExitCode { get; }
    public bool DryRun { get; }
    public string Completion { get; init; } = "completed";
    public string? Preparation { get; init; }
    public string TargetAcceptance { get; init; } = "unassessed";
    public string Cleanup { get; init; } = "not-required";
    public string Delivery { get; init; } = "attempt";
    public int DiagnosticsTotal { get; init; }
    public int DiagnosticsOmitted { get; init; }
    public string DetailDigest { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SetupCliMachineDetail? Detail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SetupCliHelpView? Help { get; init; }
    public IReadOnlyList<SetupCliMachineDiagnostic> Diagnostics => Array.AsReadOnly((SetupCliMachineDiagnostic[])_diagnostics.Clone());
    public IReadOnlyList<SetupCliMachineArtifact> Artifacts => Array.AsReadOnly((SetupCliMachineArtifact[])_artifacts.Clone());
    public SetupCliMachineCoverage Coverage { get; }
    public SetupCliMachineReadiness Readiness { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AuthoringContractVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RuntimeSchemaVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SetupCliMachineSchema? Schema { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false)]
[JsonSerializable(typeof(SetupCliMachineEnvelope))]
public partial class SetupCliJsonContext : JsonSerializerContext;

public static class SetupCliExecutableMarker
{
    public const string ExecutableName = "islamu-configurator";
}
