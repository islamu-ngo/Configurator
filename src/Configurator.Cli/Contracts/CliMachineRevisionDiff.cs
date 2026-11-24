using System.Text.Json.Serialization;

namespace Configurator.Cli;

public sealed record CliMachineRevisionChange(
    string Impact,
    string Category,
    string SchemaPath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail = null);

public sealed record CliMachineRevisionDiff(
    string BaselineFamilyId,
    long BaselineRevision,
    string BaselineDigest,
    string CandidateFamilyId,
    long CandidateRevision,
    string CandidateDigest,
    bool SameFamily,
    bool CanonicalIdentical,
    bool HasBehavioralChanges,
    bool HasDisplayOnlyChanges,
    int BehavioralCount,
    int DisplayOnlyCount,
    int TotalChanges,
    bool Truncated,
    IReadOnlyList<CliMachineRevisionChange> Changes);
