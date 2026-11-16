namespace Configurator.Presentation;

using Configurator.Core.Definitions;

/// <summary>Resolved control-level target coordinates for an actionable validation or required-work item.</summary>
public sealed record WorkspaceActionableTarget(
    string SectionId,
    DefinitionPath DefinitionPath,
    string FieldId,
    DefinitionDiagnostic Diagnostic,
    bool IsBlockingError,
    bool CanEdit);

/// <summary>Shared wizard/sections navigation projection and smart next-jump targets.</summary>
public sealed record WorkspaceNavigationProjection(
    bool IsWizardNavigation,
    string? CurrentSectionId,
    int WizardStepIndex,
    int WizardStepCount,
    bool CanAdvanceWizardStep,
    bool CanMoveBackWizardStep,
    string? NextStepSectionId,
    string? NextStepTitle,
    string? PreviousStepSectionId,
    string? PreviousStepTitle,
    string? NextRequiredSectionId,
    string? NextErrorSectionId,
    string? NextUnvisitedSectionId,
    IReadOnlyList<SectionStatusProjection> Steps)
{
    internal static WorkspaceNavigationProjection Create(
        DefinitionFormExperience experience,
        IReadOnlyList<DefinitionFormSection> orderedSections,
        IReadOnlyDictionary<string, SectionStatusProjection> statuses,
        string? currentSectionId)
    {
        bool isWizard = string.Equals(experience.Navigation, "wizard", StringComparison.OrdinalIgnoreCase);
        var activeSteps = orderedSections
            .Select(section => statuses[section.Id])
            .Where(status => status.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope)
            .ToArray();
        if (activeSteps.Length == 0)
            activeSteps = orderedSections.Select(section => statuses[section.Id]).ToArray();

        int stepIndex = -1;
        if (activeSteps.Length > 0)
        {
            if (currentSectionId is not null)
                stepIndex = Array.FindIndex(activeSteps, step => string.Equals(step.SectionId, currentSectionId, StringComparison.Ordinal));
            if (stepIndex < 0) stepIndex = 0;
        }

        string? resolvedCurrentId = stepIndex >= 0 ? activeSteps[stepIndex].SectionId : currentSectionId;
        bool canBack = stepIndex > 0;
        bool canNext = stepIndex >= 0 && stepIndex + 1 < activeSteps.Length;
        string? prevId = canBack ? activeSteps[stepIndex - 1].SectionId : null;
        string? prevTitle = canBack ? activeSteps[stepIndex - 1].Title : null;
        string? nextId = canNext ? activeSteps[stepIndex + 1].SectionId : null;
        string? nextTitle = canNext ? activeSteps[stepIndex + 1].Title : null;

        string? nextError = FindCircular(activeSteps, stepIndex, status =>
            status.ConfigurationStatus == SectionConfigurationStatus.Invalid);
        string? nextRequired = FindCircular(activeSteps, stepIndex, status =>
            status.ConfigurationStatus is SectionConfigurationStatus.MissingRequired or SectionConfigurationStatus.SecretDeferred);
        string? nextUnvisited = FindCircular(activeSteps, stepIndex, status =>
            status.Coverage == SectionReviewCoverage.NotVisited);

        return new WorkspaceNavigationProjection(
            isWizard,
            resolvedCurrentId,
            stepIndex,
            activeSteps.Length,
            canNext,
            canBack,
            nextId,
            nextTitle,
            prevId,
            prevTitle,
            nextRequired,
            nextError,
            nextUnvisited,
            Array.AsReadOnly(activeSteps));
    }

    private static string? FindCircular(
        SectionStatusProjection[] steps,
        int currentIndex,
        Func<SectionStatusProjection, bool> predicate)
    {
        if (steps.Length == 0) return null;
        int start = currentIndex >= 0 ? currentIndex + 1 : 0;
        for (int offset = 0; offset < steps.Length; offset++)
        {
            SectionStatusProjection candidate = steps[(start + offset) % steps.Length];
            if (predicate(candidate)) return candidate.SectionId;
        }
        return null;
    }
}
