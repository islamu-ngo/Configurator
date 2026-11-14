namespace Configurator.Presentation;

using Configurator.Core;

/// <summary>What an adapter has actually acknowledged for an artifact.</summary>
public enum DefinitionOutputDeliveryKind
{
    ClipboardCopied,
    DownloadRequested,
    NativeSaveVerified
}

public enum DefinitionOutputExportStatus
{
    NotExported,
    Partial,
    ExportedCurrent,
    ExportedStale
}

/// <summary>Value-free authority captured from a prepared workspace.</summary>
public sealed class DefinitionOutputExportContext
{
    internal DefinitionOutputExportContext(object owner, long generation, ArtifactDigest definitionDigest,
        string? option, long inputRevision, IEnumerable<string> artifactIds)
    {
        Owner = owner;
        Generation = generation;
        DefinitionDigest = definitionDigest;
        Option = option;
        InputRevision = inputRevision;
        ArtifactIds = Array.AsReadOnly(artifactIds.ToArray());
    }

    internal object Owner { get; }
    internal long Generation { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public string? Option { get; }
    public long InputRevision { get; }
    public IReadOnlyList<string> ArtifactIds { get; }
}

public sealed record DefinitionOutputDelivery(string ArtifactId, DefinitionOutputDeliveryKind Kind);

public sealed record DefinitionOutputTaskState(string? Option, DefinitionOutputExportStatus Status,
    IReadOnlyList<string> EligibleArtifactIds, IReadOnlyList<DefinitionOutputDelivery> Deliveries);

public sealed record DefinitionOutputNextActionState(string TargetOption, string Title,
    string? Description, string Priority);

public sealed record WorkspaceOutputWorkflow(IReadOnlyList<DefinitionOutputTaskState> Tasks,
    IReadOnlyList<DefinitionOutputNextActionState> ProposedNextActions);
