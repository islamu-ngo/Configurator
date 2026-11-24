namespace Configurator.Presentation;

/// <summary>Authoritative per-section configuration health ordered from highest-priority blocker downward.</summary>
public enum SectionConfigurationStatus
{
    Invalid,
    MissingRequired,
    UnresolvedApplicability,
    SecretDeferred,
    RequiredComplete,
    OptionalUnset,
    OutsideSelectedScope,
    PendingEvaluation
}

/// <summary>Transient per-section review coverage tracked separately from configuration status.</summary>
public enum SectionReviewCoverage
{
    NotVisited,
    Opened,
    Reviewed
}

/// <summary>Two-dimensional status, review coverage, and hidden-optional inventory for one definition section.</summary>
public sealed record SectionStatusProjection(
    string SectionId,
    string Title,
    DefinitionFormProgress Progress,
    SectionConfigurationStatus ConfigurationStatus,
    SectionReviewCoverage Coverage,
    bool HasNewlyRelevantSettings,
    bool IsApplicable,
    int AvailableOptionalCount,
    IReadOnlyList<string> HiddenOptionalTitles,
    int ActiveFieldCount,
    int SuppliedOptionalCount)
{
    public bool HasHiddenOptional => AvailableOptionalCount > 0;
}

/// <summary>Dual workspace progress denominators separating required work from section review coverage.</summary>
public sealed record WorkspaceProgressSummary(
    int ReadyRequired,
    int TotalRequired,
    int MissingRequired,
    int InvalidCount,
    int DeferredSecretCount,
    int ActiveSections,
    int OpenedSections,
    int ReviewedSections,
    int OptionalInvalidCount = 0,
    int OptionalMissing = 0)
{
    public int? RequiredPercent => TotalRequired == 0 ? null : 100 * ReadyRequired / TotalRequired;
    public int TotalInvalidCount => InvalidCount;
    public int RequiredInvalidCount => Math.Max(0, InvalidCount - OptionalInvalidCount);
    public bool HasBlockingErrors => InvalidCount > 0;
    public bool HasMissingNonDeferredRequired => MissingRequired > DeferredSecretCount;
    public bool IsReadyForCompleteDraft => InvalidCount == 0 && MissingRequired == 0;
    public bool IsReadyWithDeferredSecrets => InvalidCount == 0 && MissingRequired == DeferredSecretCount && DeferredSecretCount > 0;
    public bool IsReadyForExport => MissingRequired == 0 && DeferredSecretCount == 0 && TotalInvalidCount == 0;
}
