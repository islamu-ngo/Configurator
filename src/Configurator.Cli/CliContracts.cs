using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli;

public enum CliMode { Text, Machine }
public enum CliExitCode { Success = 0, Validation = 2, Incomplete = 3, Blocked = 4, Usage = 64, Data = 65, Internal = 70, Io = 74, Cancelled = 130 }

public interface ICliInput
{
    ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
}

/// <summary>Supplies an invocation's public authoring authority, independently of private answer I/O.</summary>
public interface ICliAuthoringFiles
{
    ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken);
    ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken);
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    ICliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical);
}

/// <summary>Separates staging, create-only publication and cleanup for truthful ordered receipts.</summary>
public interface ICliPublicFilePreparation : IDisposable
{
    ValueTask StageAsync(CancellationToken cancellationToken);
    ValueTask<bool> CommitAsync(CancellationToken cancellationToken);
}

public interface ICliWriter
{
    ValueTask<ProtectedArtifactStatus?> CheckArtifactDestinationAsync(string path, CancellationToken cancellationToken);

    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    ValueTask<CliPublicationResult> WriteArtifactAsync(string path, ArtifactKind kind,
        ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    /// <summary>Reports established artifact outcomes synchronously before later work can fail; the result supplies final cleanup and cancellation facts.</summary>
    ValueTask<CliPublicationResult> WriteBundleAsync(
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
        Action<string, CliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken);
}

public sealed record CliPublicationOutcome(ProtectedArtifactCommitResult? Result, bool Unknown = false,
    bool DeliveredToStandardOutput = false);
public sealed record CliPublicationResult(IReadOnlyList<CliPublicationOutcome> Outcomes,
    bool CleanupFailed = false, bool Cancelled = false);

public sealed record CliIo
{
    public CliIo(ICliInput input, ICliWriter output, ICliWriter error,
        ICliAuthoringFiles authoringFiles, int maximumOutputBytes, int maximumBytes)
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
    public ICliInput Input { get; }
    public ICliWriter Output { get; }
    public ICliWriter Error { get; }
    public ICliAuthoringFiles AuthoringFiles { get; }
    public int MaximumOutputBytes { get; }
    public int MaximumBytes { get; }
    public override string ToString() => $"{nameof(CliIo)}:OutputBytes={MaximumOutputBytes}:Bytes={MaximumBytes}";
}

public sealed record CliInvocation
{
    private readonly string[] _arguments;
    public CliInvocation(IEnumerable<string> arguments, CliMode mode, CliIo io, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments.ToArray();
        if (_arguments.Any(argument => argument is null || argument.Length > 4096) || _arguments.Length > 256
            || _arguments.Sum(argument => System.Text.Encoding.UTF8.GetByteCount(argument) + 1) > 32_768)
            throw new ArgumentException("argument-bound-exceeded", nameof(arguments));
        Mode = mode;
        Io = io ?? throw new ArgumentNullException(nameof(io));
        Culture = CultureInfo.ReadOnly((CultureInfo)(culture ?? CultureInfo.CurrentUICulture).Clone());
    }
    public IReadOnlyList<string> Arguments => Array.AsReadOnly((string[])_arguments.Clone());
    public CliMode Mode { get; }
    public CliIo Io { get; }
    public CultureInfo Culture { get; }
    public override string ToString() => $"{nameof(CliInvocation)}:Mode={Mode}:Count={_arguments.Length}";
}

public sealed record CliMachineInvocation(string CommandFamily, string Operation, string Mode);
public sealed record CliMachineDiagnostic(string Code, string Path, string Severity)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Line { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Column { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ByteOffset { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CliMachineSourceLocation? Source { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CliMachineCorrectionLocation? Correction { get; init; }
}
public sealed record CliMachineSourceLocation(string SchemaPath, int DocumentIndex,
    int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record CliMachineLocationSegment(string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PropertyName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Index);
public sealed record CliMachineCorrectionLocation(long InputRevision, string FieldId,
    IReadOnlyList<CliMachineLocationSegment> Segments);
public sealed record CliMachineSchema(string Kind, string Id, string Digest, string Profile,
    string AuthoringContractVersion, string RuntimeSchemaVersion, string AnswerContractVersion,
    IReadOnlyList<string> Capabilities, JsonElement Document);
public sealed record CliMachineCoverage
{
    private readonly string[] _coveredKeys;
    private readonly string[] _missingKeys;
    public CliMachineCoverage(IEnumerable<string> coveredKeys, IEnumerable<string> missingKeys)
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
public sealed record CliMachineReadiness
{
    private readonly string[] _missingKeys;
    private readonly string[] _blockedKeys;
    public CliMachineReadiness(string state, IEnumerable<string> missingKeys, IEnumerable<string> blockedKeys)
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
public sealed record CliMachineArtifact(string Kind, string MediaType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Digest, string Sensitivity,
    [property: JsonIgnore] CliMachineCoverage Coverage,
    [property: JsonIgnore] CliMachineReadiness Readiness, string PathIntent, string WriteStatus)
{
    public string Id { get; init; } = Kind;
    public string Installation { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
    public string Verification { get; init; } = PathIntent == "output"
        && Kind is not ("authoring-source" or "compiled-definition" or "logical-id-map") ? "not-attempted" : "not-applicable";
}

public sealed record CliMachineCoverageEntry(string Id, string State);
public sealed record CliMachineFindingSource
{
    public CliMachineFindingSource(IReadOnlyList<int> ordinals) => Ordinals = Array.AsReadOnly(ordinals.ToArray());
    public IReadOnlyList<int> Ordinals { get; }
}
public sealed record CliMachineFindingSchema
{
    public CliMachineFindingSchema(string fieldId, IReadOnlyList<string> relativePath)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }
    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
}
public sealed record CliMachineFinding(string Classification, string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CliMachineFindingSource? Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CliMachineFindingSchema? Schema);
public sealed record CliMachineAssessment(string DefinitionDigest, string ArtifactId, string Compatibility,
    string Coverage, string Inventory, string Autofill, int RetainedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total)
{
    private ReadOnlyCollection<CliMachineFinding> _findings = Array.AsReadOnly(Array.Empty<CliMachineFinding>());
    [JsonIgnore]
    public IReadOnlyList<CliMachineFinding> Findings
    {
        get => _findings;
        init => _findings = Array.AsReadOnly(value.ToArray());
    }
}
public sealed record CliMachineDetail(string Kind, int Offset, int Limit, int RetainedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total, string Digest,
    IReadOnlyList<CliMachineDiagnostic> Diagnostics, IReadOnlyList<CliMachineCoverageEntry> Coverage,
    IReadOnlyList<CliMachineFinding> Findings)
{
    private ReadOnlyCollection<CliMachineDiagnostic> _diagnostics = Array.AsReadOnly(Diagnostics.ToArray());
    private ReadOnlyCollection<CliMachineCoverageEntry> _coverage = Array.AsReadOnly(Coverage.ToArray());
    private ReadOnlyCollection<CliMachineFinding> _findings = Array.AsReadOnly(Findings.ToArray());
    public IReadOnlyList<CliMachineDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<CliMachineCoverageEntry> Coverage
    {
        get => _coverage;
        init => _coverage = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<CliMachineFinding> Findings
    {
        get => _findings;
        init => _findings = Array.AsReadOnly(value.ToArray());
    }
    public int ReturnedCount => Diagnostics.Count + Coverage.Count + Findings.Count;
    public bool Terminal => Offset + ReturnedCount == RetainedCount;
    public int? NextOffset => Terminal ? null : Offset + ReturnedCount;
}
public sealed record CliMachineEnvelope
{
    private readonly CliMachineDiagnostic[] _diagnostics;
    private readonly CliMachineArtifact[] _artifacts;
    public CliMachineEnvelope(string schemaVersion, CliMachineInvocation invocation,
        string status, string exitCategory, int exitCode, bool dryRun,
        IEnumerable<CliMachineDiagnostic> diagnostics, IEnumerable<CliMachineArtifact> artifacts,
        CliMachineCoverage coverage, CliMachineReadiness readiness)
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
    public CliMachineInvocation Invocation { get; }
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
    public CliMachineDetail? Detail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliMachineAssessment? Assessment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliHelpView? Help { get; init; }
    public IReadOnlyList<CliMachineDiagnostic> Diagnostics => Array.AsReadOnly((CliMachineDiagnostic[])_diagnostics.Clone());
    public IReadOnlyList<CliMachineArtifact> Artifacts => Array.AsReadOnly((CliMachineArtifact[])_artifacts.Clone());
    public CliMachineCoverage Coverage { get; }
    public CliMachineReadiness Readiness { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoringContractVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuntimeSchemaVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CliMachineSchema? Schema { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false)]
[JsonSerializable(typeof(CliMachineEnvelope))]
public partial class CliJsonContext : JsonSerializerContext;

public static class CliExecutableMarker
{
    public const string ExecutableName = "islamu-configurator";
}
