namespace Configurator.Presentation;

using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>Public schema coordinates and presence facts only; no retained answer or value reader.</summary>
public sealed record DefinitionTransferCandidate(int Id, DefinitionPath Source, DefinitionPath Target,
    string SourceTitle, string TargetTitle, bool RequiresMasking, bool DestinationProvided, bool SelectedByDefault);

/// <summary>An immutable, value-free review bound to one form lifecycle and exact answer revision.</summary>
public sealed class DefinitionTransitionTransferPlan
{
    internal DefinitionTransitionTransferPlan(object owner, long generation, ArtifactDigest digest,
        long inputRevision, string? targetOption, IEnumerable<DefinitionTransferCandidate> candidates)
    {
        Owner = owner;
        Generation = generation;
        DefinitionDigest = digest;
        InputRevision = inputRevision;
        TargetOption = targetOption;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    internal object Owner { get; }
    internal long Generation { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public long InputRevision { get; }
    public string? TargetOption { get; }
    public IReadOnlyList<DefinitionTransferCandidate> Candidates { get; }
}

public sealed record DefinitionTransferResult(bool Succeeded, string Code, long InputRevision)
{
    public bool ObserverFailed { get; init; }
}
