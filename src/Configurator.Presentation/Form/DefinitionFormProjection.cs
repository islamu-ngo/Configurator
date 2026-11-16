namespace Configurator.Presentation;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed record DefinitionBooleanLabels(string TrueLabel, string FalseLabel);

public sealed class DefinitionChoicePresentation
{
    internal DefinitionChoicePresentation(
        JsonElement value,
        string label,
        string? explanation,
        string? displayLabel = null,
        string? wireToken = null,
        bool isDeprecated = false,
        string? deprecationGuidance = null,
        bool hasCopyableWireToken = true)
    {
        Value = value.Clone();
        Label = label;
        Explanation = explanation;
        DisplayLabel = displayLabel ?? label;
        WireToken = wireToken ?? (value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
        IsDeprecated = isDeprecated;
        DeprecationGuidance = deprecationGuidance;
        HasCopyableWireToken = hasCopyableWireToken;
    }

    public JsonElement Value { get; }
    public string Label { get; }
    public string? Explanation { get; }
    public string DisplayLabel { get; }
    public string WireToken { get; }
    public bool IsDeprecated { get; }
    public string? DeprecationGuidance { get; }
    public bool HasCopyableWireToken { get; }
}

/// <summary>Public display metadata over the original admitted typed schema; no answer values are retained.</summary>
public sealed class DefinitionEditorPresentation
{
    internal DefinitionEditorPresentation(
        DefinitionNode schema,
        string title,
        string? description,
        string? instructions,
        string? units,
        IReadOnlyList<DefinitionChoicePresentation> choices,
        IReadOnlyList<DefinitionGuidanceLink> links,
        WorkspaceNamingMode namingMode = WorkspaceNamingMode.Humanized,
        string? displayLabel = null,
        string? wireToken = null,
        string? secondaryLabel = null,
        bool hasExplicitTitle = true,
        bool hasCopyableWireToken = true,
        string? placeholder = null,
        string? defaultText = null,
        string? ifOmittedDisplay = null,
        bool isDeprecated = false,
        string? deprecationGuidance = null,
        DefinitionFieldWidget widget = DefinitionFieldWidget.Auto,
        int? rows = null,
        string? syntax = null,
        DefinitionBooleanLabels? booleanLabels = null,
        string? itemTitlePointer = null,
        IReadOnlyList<DefinitionPath>? uniqueBy = null,
        IReadOnlyList<string>? orderedPropertyNames = null,
        int controlledFieldCount = 0,
        int controlledSectionCount = 0,
        string? controllerImpactSummary = null)
    {
        Schema = schema;
        Title = title;
        Description = description;
        Instructions = instructions;
        Units = units;
        Choices = choices;
        Links = links;
        NamingMode = namingMode;
        DisplayLabel = displayLabel ?? title;
        WireToken = wireToken ?? string.Empty;
        SecondaryLabel = secondaryLabel ?? string.Empty;
        HasExplicitTitle = hasExplicitTitle;
        HasCopyableWireToken = hasCopyableWireToken;
        Placeholder = placeholder;
        DefaultText = defaultText;
        IfOmittedDisplay = ifOmittedDisplay;
        IsDeprecated = isDeprecated;
        DeprecationGuidance = deprecationGuidance;
        Widget = widget;
        Rows = rows;
        Syntax = syntax;
        BooleanLabels = booleanLabels ?? new DefinitionBooleanLabels("Yes", "No");
        ItemTitlePointer = itemTitlePointer;
        UniqueBy = uniqueBy ?? Array.Empty<DefinitionPath>();
        OrderedPropertyNames = orderedPropertyNames ?? schema.OrderedPropertyNames;
        ControlledFieldCount = controlledFieldCount;
        ControlledSectionCount = controlledSectionCount;
        ControllerImpactSummary = controllerImpactSummary;
    }

    public DefinitionNode Schema { get; }
    public string Title { get; }
    public string? Description { get; }
    public string? Instructions { get; }
    public string? Units { get; }
    public IReadOnlyList<DefinitionChoicePresentation> Choices { get; }
    public IReadOnlyList<DefinitionGuidanceLink> Links { get; }
    public WorkspaceNamingMode NamingMode { get; }
    public string DisplayLabel { get; }
    public string WireToken { get; }
    public string SecondaryLabel { get; }
    public bool HasExplicitTitle { get; }
    public bool HasCopyableWireToken { get; }
    public string? Placeholder { get; }
    public string? DefaultText { get; }
    public string? IfOmittedDisplay { get; }
    public bool IsDeprecated { get; }
    public string? DeprecationGuidance { get; }
    public DefinitionFieldWidget Widget { get; }
    public int? Rows { get; }
    public string? Syntax { get; }
    public DefinitionBooleanLabels BooleanLabels { get; }
    public string? ItemTitlePointer { get; }
    public IReadOnlyList<DefinitionPath> UniqueBy { get; }
    public IReadOnlyList<string> OrderedPropertyNames { get; }
    public int ControlledFieldCount { get; }
    public int ControlledSectionCount { get; }
    public bool IsController => ControlledFieldCount > 0;
    public string? ControllerImpactSummary { get; }

    /// <summary>Builds localized false/true choice metadata for boolean editors without altering enum Choices.</summary>
    public IReadOnlyList<DefinitionChoicePresentation> GetBooleanChoicePresentations()
    {
        if (Schema.Type != DefinitionNodeType.Boolean) return [];
        bool technical = NamingMode == WorkspaceNamingMode.Technical;
        return Array.AsReadOnly(new DefinitionChoicePresentation[]
        {
            new(JsonSerializer.SerializeToElement(false), technical ? "false" : BooleanLabels.FalseLabel, null,
                BooleanLabels.FalseLabel, "false", hasCopyableWireToken: HasCopyableWireToken),
            new(JsonSerializer.SerializeToElement(true), technical ? "true" : BooleanLabels.TrueLabel, null,
                BooleanLabels.TrueLabel, "true", hasCopyableWireToken: HasCopyableWireToken)
        });
    }
}

public sealed record DefinitionFormExperience
{
    internal DefinitionFormExperience(string id, string title, FieldDisclosure initialDisclosure,
        string navigation, IEnumerable<string> sections, string? description)
    {
        Id = id;
        Title = title;
        Description = description;
        InitialDisclosure = initialDisclosure;
        Navigation = navigation;
        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
}

public sealed record DefinitionFormProgress(int Ready, int Required, int Missing, int Invalid, int Deferred)
{
    public int? Percent => Required == 0 ? null : 100 * Ready / Required;
}

public sealed record DefinitionFormSectionGroup(
    string Id,
    string Title,
    string? Description,
    FieldDisclosure Disclosure,
    bool InitiallyCollapsed,
    bool IsApplicable,
    bool HasBlockingIssue,
    bool IsExpanded,
    IReadOnlyList<string> Pointers,
    IReadOnlyList<string> Fields);

public sealed record DefinitionFormSection
{
    internal DefinitionFormSection(
        string id,
        string title,
        string? description,
        IEnumerable<string> fields,
        FieldDisclosure disclosure = FieldDisclosure.Guided,
        IEnumerable<string>? pointers = null,
        IEnumerable<DefinitionFormSectionGroup>? groups = null,
        bool isApplicable = true)
    {
        Id = id;
        Title = title;
        Description = description;
        Fields = Array.AsReadOnly(fields.ToArray());
        Disclosure = disclosure;
        Pointers = Array.AsReadOnly(pointers?.ToArray() ?? []);
        Groups = Array.AsReadOnly(groups?.ToArray() ?? []);
        IsApplicable = isApplicable;
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<string> Fields { get; }
    public FieldDisclosure Disclosure { get; }
    public IReadOnlyList<string> Pointers { get; }
    public IReadOnlyList<DefinitionFormSectionGroup> Groups { get; }
    public bool IsApplicable { get; }
}

public sealed record WorkspaceContextProjection(
    string RecipeId,
    string RecipeTitle,
    string ScopeId,
    string ScopeTitle,
    IReadOnlyList<string> ComponentTitles,
    IReadOnlyList<string> ArtifactFileNames)
{
    public string Summary => $"{RecipeTitle} · {ScopeTitle}";
}

/// <summary>Value-free workspace projection shared by native and browser clients.</summary>
public sealed class DefinitionFormProjection
{
    private readonly HashSet<string> _visibleFields;
    private readonly Lazy<DefinitionDiscoverySnapshot> _discovery;
    private readonly Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> _entries;
    private readonly Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> _relevantEntries;

    private DefinitionFormProjection(
        DefinitionFormProgress progress,
        Lazy<DefinitionDiscoverySnapshot> discovery,
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries,
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> relevantEntries,
        IReadOnlyList<DefinitionFormSection> sections,
        IEnumerable<string> visibleFields,
        IReadOnlyDictionary<string, DefinitionFormProgress> sectionProgress,
        IReadOnlyDictionary<string, SectionStatusProjection> sectionStatuses,
        WorkspaceProgressSummary progressSummary,
        WorkspaceNavigationProjection navigation,
        WorkspacePresentationOptions presentationOptions,
        DefinitionConfigurationContext? configurationContext,
        WorkspaceContextProjection? contextSummary,
        IReadOnlyList<ArtifactDefinition> eligibleArtifacts,
        DefinitionDiagnostic? nextActionableDiagnostic,
        WorkspaceActionableTarget? nextActionableTarget)
    {
        Progress = progress;
        _discovery = discovery;
        _entries = entries;
        _relevantEntries = relevantEntries;
        Sections = sections;
        SectionProgress = sectionProgress;
        SectionStatuses = sectionStatuses;
        ProgressSummary = progressSummary;
        Navigation = navigation;
        PresentationOptions = presentationOptions;
        _visibleFields = visibleFields.ToHashSet(StringComparer.Ordinal);
        ConfigurationContext = configurationContext;
        ContextSummary = contextSummary;
        EligibleArtifacts = eligibleArtifacts;
        NextActionableDiagnostic = nextActionableDiagnostic;
        NextActionableTarget = nextActionableTarget;
    }

    private DefinitionFormProjection(
        DefinitionFormProjection current,
        Lazy<DefinitionDiscoverySnapshot>? discovery = null,
        IReadOnlyList<DefinitionFormSection>? sections = null,
        IReadOnlyDictionary<string, SectionStatusProjection>? sectionStatuses = null,
        WorkspaceProgressSummary? progressSummary = null,
        WorkspaceNavigationProjection? navigation = null,
        bool updateNextActionable = false,
        DefinitionDiagnostic? nextActionableDiagnostic = null,
        WorkspaceActionableTarget? nextActionableTarget = null)
    {
        Progress = current.Progress;
        _discovery = discovery ?? current._discovery;
        _entries = current._entries;
        Sections = sections ?? current.Sections;
        SectionProgress = current.SectionProgress;
        SectionStatuses = sectionStatuses ?? current.SectionStatuses;
        ProgressSummary = progressSummary ?? current.ProgressSummary;
        Navigation = navigation ?? current.Navigation;
        PresentationOptions = current.PresentationOptions;
        _relevantEntries = current._relevantEntries;
        _visibleFields = current._visibleFields;
        ConfigurationContext = current.ConfigurationContext;
        ContextSummary = current.ContextSummary;
        EligibleArtifacts = current.EligibleArtifacts;
        NextActionableDiagnostic = updateNextActionable ? nextActionableDiagnostic : current.NextActionableDiagnostic;
        NextActionableTarget = updateNextActionable ? nextActionableTarget : current.NextActionableTarget;
    }

    internal DefinitionFormProjection WithDiscovery(Lazy<DefinitionDiscoverySnapshot> discovery) =>
        new(this, discovery: discovery);

    internal DefinitionFormProjection WithNavigation(DefinitionFormModel form, string? openedSectionId)
    {
        IReadOnlyDictionary<string, SectionStatusProjection> updatedStatuses = SectionStatuses;
        WorkspaceProgressSummary updatedSummary = ProgressSummary;
        if (!string.IsNullOrEmpty(openedSectionId) && SectionStatuses.TryGetValue(openedSectionId, out SectionStatusProjection? existing))
        {
            SectionReviewCoverage coverage = form.GetSectionCoverage(openedSectionId, out bool hasNewlyRelevant);
            if (existing.Coverage != coverage || existing.HasNewlyRelevantSettings != hasNewlyRelevant)
            {
                var next = new Dictionary<string, SectionStatusProjection>(SectionStatuses.Count, StringComparer.Ordinal);
                foreach (var pair in SectionStatuses)
                    next[pair.Key] = pair.Value;
                next[openedSectionId] = existing with
                {
                    Coverage = coverage,
                    HasNewlyRelevantSettings = hasNewlyRelevant
                };
                updatedStatuses = new ReadOnlyDictionary<string, SectionStatusProjection>(next);
                int openedCount = 0;
                foreach (var status in updatedStatuses.Values)
                {
                    if (status.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope &&
                        status.Coverage != SectionReviewCoverage.NotVisited)
                        openedCount++;
                }
                updatedSummary = ProgressSummary with { OpenedSections = openedCount };
            }
        }

        DefinitionFormExperience activeExperience = form.Experiences.Single(item => item.Id == form.SelectedExperienceId);
        WorkspaceNavigationProjection nextNavigation = WorkspaceNavigationProjection.Create(
            activeExperience, Sections, updatedStatuses, form.CurrentSectionId);
        DefinitionDiagnostic? nextDiagnostic = form.Facts.GetNextActionableDiagnostic(form, form.CurrentSectionId);
        WorkspaceActionableTarget? nextTarget = nextDiagnostic is null ? null : form.Facts.ResolveActionableTarget(form, nextDiagnostic);
        return new(
            this,
            sectionStatuses: updatedStatuses,
            progressSummary: updatedSummary,
            navigation: nextNavigation,
            updateNextActionable: true,
            nextActionableDiagnostic: nextDiagnostic,
            nextActionableTarget: nextTarget);
    }

    internal DefinitionFormProjection WithGroupExpansion(string sectionId, string groupId, bool expanded)
    {
        for (int s = 0; s < Sections.Count; s++)
        {
            DefinitionFormSection section = Sections[s];
            if (!string.Equals(section.Id, sectionId, StringComparison.Ordinal)) continue;
            for (int g = 0; g < section.Groups.Count; g++)
            {
                DefinitionFormSectionGroup group = section.Groups[g];
                if (!string.Equals(group.Id, groupId, StringComparison.Ordinal)) continue;
                bool targetExpanded = group.HasBlockingIssue || expanded;
                if (group.IsExpanded == targetExpanded) return this;
                var nextGroups = new DefinitionFormSectionGroup[section.Groups.Count];
                for (int k = 0; k < section.Groups.Count; k++)
                    nextGroups[k] = k == g ? group with { IsExpanded = targetExpanded } : section.Groups[k];
                var nextSections = new DefinitionFormSection[Sections.Count];
                for (int k = 0; k < Sections.Count; k++)
                {
                    nextSections[k] = k == s
                        ? new DefinitionFormSection(
                            section.Id,
                            section.Title,
                            section.Description,
                            section.Fields,
                            section.Disclosure,
                            section.Pointers,
                            nextGroups,
                            section.IsApplicable)
                        : Sections[k];
                }
                return new(this, sections: Array.AsReadOnly(nextSections));
            }
            return this;
        }
        return this;
    }

    public DefinitionFormProgress Progress { get; }
    public DefinitionConfigurationContext? ConfigurationContext { get; }
    public WorkspaceContextProjection? ContextSummary { get; }
    public IReadOnlyList<ArtifactDefinition> EligibleArtifacts { get; }
    public DefinitionDiagnostic? NextActionableDiagnostic { get; }
    public WorkspaceActionableTarget? NextActionableTarget { get; }
    public DefinitionDiscoverySnapshot Discovery => _discovery.Value;
    public IReadOnlyList<DefinitionDiscoveryMatch> Matches => Discovery.Matches;
    public IReadOnlyList<DefinitionDiscoveryEntry> Entries => _entries.Value;
    /// <summary>Active relevant effects and their literal-path ancestors, before client paging.</summary>
    public IReadOnlyList<DefinitionDiscoveryEntry> RelevantEntries => _relevantEntries.Value;
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public IReadOnlyDictionary<string, DefinitionFormProgress> SectionProgress { get; }
    public IReadOnlyDictionary<string, SectionStatusProjection> SectionStatuses { get; }
    public WorkspaceProgressSummary ProgressSummary { get; }
    public WorkspaceNavigationProjection Navigation { get; }
    public WorkspacePresentationOptions PresentationOptions { get; }
    public bool IsFieldVisible(string fieldId) => _visibleFields.Contains(fieldId);

    internal static DefinitionFormProjection Create(DefinitionFormModel form)
    {
        DefinitionFormFacts facts = form.Facts;
        IReadOnlyList<DefinitionFormSection> baseSections = form.OrderedSections;
        if (facts.Validation.ConfigurationContext is not null && form.Definition.Deployment is not null)
        {
            baseSections = Array.AsReadOnly(baseSections.Select(section => new DefinitionFormSection(
                section.Id, section.Title, section.Description,
                section.Fields.Where(fieldId => !form.IsConfigurationSelectorRoot(fieldId, facts.Validation)),
                section.Disclosure, section.Pointers, section.Groups, section.IsApplicable))
                .Where(section => section.Fields.Count > 0 || section.Pointers.Count > 0).ToArray());
        }

        DefinitionPath[]? inputDiagnosticPaths = null;
        if (facts.HasInputErrors)
        {
            inputDiagnosticPaths = new DefinitionPath[facts.InputDiagnostics.Count];
            for (int index = 0; index < facts.InputDiagnostics.Count; index++)
            {
                DefinitionDiagnostic diagnostic = facts.InputDiagnostics[index];
                inputDiagnosticPaths[index] = diagnostic.FieldId.Length > 0
                    ? form.DataPath(diagnostic.RelativePath.Count == 0
                        ? [diagnostic.FieldId]
                        : [diagnostic.FieldId, .. diagnostic.RelativePath])
                    : DefinitionPath.FromSegments(diagnostic.RelativePath);
            }
        }

        // Enrich sections with runtime applicability + auto-expanded error/missing groups.
        DefinitionFormSection[] enrichedSections = baseSections.Count == 1
            ? [EnrichSection(form, facts, baseSections[0], inputDiagnosticPaths)]
            : baseSections.Select(section => EnrichSection(form, facts, section, inputDiagnosticPaths)).ToArray();

        // Build 2D SectionStatusProjection for every declared/active section.
        var statuses = new Dictionary<string, SectionStatusProjection>(enrichedSections.Length, StringComparer.Ordinal);
        Dictionary<string, bool>? continuousApplicability =
            form.EditorVisibility == DefinitionEditorVisibilityPolicy.Continuous
                ? new Dictionary<string, bool>(enrichedSections.Length, StringComparer.Ordinal)
                : null;
        foreach (DefinitionFormSection section in enrichedSections)
        {
            SectionStatusProjection status = BuildSectionStatus(form, facts, section);
            statuses[section.Id] = status;
            if (continuousApplicability is not null)
                continuousApplicability[section.Id] = status.IsApplicable;
        }

        var readOnlyStatuses = new ReadOnlyDictionary<string, SectionStatusProjection>(statuses);
        int activeSections = 0;
        int openedSections = 0;
        int reviewedSections = 0;
        foreach (SectionStatusProjection status in statuses.Values)
        {
            if (status.ConfigurationStatus == SectionConfigurationStatus.OutsideSelectedScope) continue;
            activeSections++;
            if (status.Coverage != SectionReviewCoverage.NotVisited) openedSections++;
            if (status.Coverage == SectionReviewCoverage.Reviewed) reviewedSections++;
        }

        var progressSummary = new WorkspaceProgressSummary(
            facts.Progress.Ready,
            facts.Progress.Required,
            facts.Progress.Missing,
            facts.Progress.Invalid,
            facts.Progress.Deferred,
            activeSections,
            openedSections,
            reviewedSections,
            facts.OptionalInvalidCount);

        DefinitionFormExperience activeExperience = form.Experiences.Single(item => item.Id == form.SelectedExperienceId);
        WorkspaceNavigationProjection navigation = WorkspaceNavigationProjection.Create(
            activeExperience, enrichedSections, readOnlyStatuses, form.CurrentSectionId);
        IReadOnlyList<ArtifactDefinition> eligibleArtifacts = form.EligibleArtifacts;
        WorkspaceContextProjection? contextSummary = BuildContextSummary(form, facts.Validation.ConfigurationContext, eligibleArtifacts);
        DefinitionDiagnostic? nextActionableDiagnostic = facts.GetNextActionableDiagnostic(form, form.CurrentSectionId);
        WorkspaceActionableTarget? nextActionableTarget = nextActionableDiagnostic is null
            ? null
            : facts.ResolveActionableTarget(form, nextActionableDiagnostic);

        bool isContinuous = form.EditorVisibility == DefinitionEditorVisibilityPolicy.Continuous;
        var visibleFields = new List<string>(facts.Validation.Fields.Count);
        foreach (FieldState state in facts.Validation.Fields)
        {
            if (state.IsActive && form.HasField(state.Id) &&
                form.IsRootFieldEditorVisible(state.Id, facts, isContinuous, activeExperience, continuousApplicability))
                visibleFields.Add(state.Id);
        }

        return new DefinitionFormProjection(
            facts.Progress,
            form.CurrentDiscovery,
            form.OrderedEntries,
            form.CurrentRelevantEntries,
            Array.AsReadOnly(enrichedSections),
            visibleFields,
            facts.SectionProgress,
            readOnlyStatuses,
            progressSummary,
            navigation,
            form.PresentationOptions,
            facts.Validation.ConfigurationContext,
            contextSummary,
            eligibleArtifacts,
            nextActionableDiagnostic,
            nextActionableTarget);
    }

    private static WorkspaceContextProjection? BuildContextSummary(
        DefinitionFormModel form,
        DefinitionConfigurationContext? context,
        IReadOnlyList<ArtifactDefinition> eligibleArtifacts)
    {
        if (context is not { RecipeId: { } recipeId, ScopeId: { } scopeId } ||
            form.Definition.Deployment is not { } deployment)
            return null;

        int recipeIndex = -1;
        DefinitionDeploymentRecipe? recipe = null;
        for (int index = 0; index < deployment.Recipes.Count; index++)
        {
            if (deployment.Recipes[index].Id == recipeId)
            {
                recipeIndex = index;
                recipe = deployment.Recipes[index];
                break;
            }
        }
        if (recipe is null) return null;

        int scopeIndex = -1;
        DefinitionDeploymentScope? scope = null;
        for (int index = 0; index < recipe.Scopes.Count; index++)
        {
            if (recipe.Scopes[index].Id == scopeId)
            {
                scopeIndex = index;
                scope = recipe.Scopes[index];
                break;
            }
        }
        if (scope is null) return null;

        string recipeTitle = form.LocalizedText(
            DefinitionPath.FromSegments(["deployment", "recipes", recipeIndex.ToString(CultureInfo.InvariantCulture), "title"]),
            recipe.Title)!;
        string scopeTitle = form.LocalizedText(
            DefinitionPath.FromSegments(["deployment", "recipes", recipeIndex.ToString(CultureInfo.InvariantCulture), "scopes", scopeIndex.ToString(CultureInfo.InvariantCulture), "title"]),
            scope.Title)!;

        var componentTitles = new List<string>(scope.ComponentIds.Count);
        foreach (string componentId in scope.ComponentIds)
        {
            for (int compIndex = 0; compIndex < deployment.Components.Count; compIndex++)
            {
                DefinitionDeploymentComponent comp = deployment.Components[compIndex];
                if (comp.Id != componentId) continue;
                string title = form.LocalizedText(
                    DefinitionPath.FromSegments(["deployment", "components", compIndex.ToString(CultureInfo.InvariantCulture), "title"]),
                    comp.Title)!;
                componentTitles.Add(title);
                break;
            }
        }

        string[] artifactFileNames = eligibleArtifacts.Select(artifact => artifact.FileName).ToArray();
        return new WorkspaceContextProjection(
            recipeId,
            recipeTitle,
            scopeId,
            scopeTitle,
            Array.AsReadOnly(componentTitles.ToArray()),
            Array.AsReadOnly(artifactFileNames));
    }

    private static DefinitionFormSection EnrichSection(
        DefinitionFormModel form,
        DefinitionFormFacts facts,
        DefinitionFormSection section,
        DefinitionPath[]? inputDiagnosticPaths = null)
    {
        DefinitionSection? authored = form.FindAuthoredSection(section.Id);
        bool isApplicable = authored?.IsApplicable(facts.Validation) ?? true;
        if (section.Groups.Count == 0 && authored is null)
            return section;

        var enrichedGroups = new List<DefinitionFormSectionGroup>(section.Groups.Count);
        foreach (DefinitionFormSectionGroup group in section.Groups)
        {
            DefinitionSectionGroup? authoredGroup = authored?.Groups.FirstOrDefault(g => g.Id == group.Id);
            bool groupApplicable = authoredGroup?.IsApplicable(facts.Validation) ?? group.IsApplicable;
            DefinitionPath[] groupPaths = form.GetGroupPointerPaths(section.Id, group.Id);
            bool hasBlocking = false;
            foreach (DefinitionPath prefix in groupPaths)
            {
                if (facts.NeedsCorrection(prefix))
                {
                    hasBlocking = true;
                    break;
                }
            }
            if (!hasBlocking && inputDiagnosticPaths is not null)
            {
                foreach (DefinitionPath diagPath in inputDiagnosticPaths)
                {
                    if (MatchesAnyPrefix(diagPath, groupPaths))
                    {
                        hasBlocking = true;
                        break;
                    }
                }
            }
            bool? userExpanded = form.TryGetGroupExpansion(section.Id, group.Id);
            bool isExpanded = hasBlocking || (userExpanded ?? !group.InitiallyCollapsed);
            enrichedGroups.Add(group with
            {
                IsApplicable = groupApplicable,
                HasBlockingIssue = hasBlocking,
                IsExpanded = isExpanded
            });
        }

        return new DefinitionFormSection(
            section.Id,
            section.Title,
            section.Description,
            section.Fields,
            section.Disclosure,
            section.Pointers,
            enrichedGroups,
            isApplicable);
    }

    private static SectionStatusProjection BuildSectionStatus(
        DefinitionFormModel form,
        DefinitionFormFacts facts,
        DefinitionFormSection section)
    {
        DefinitionFormProgress progress = facts.SectionProgress.GetValueOrDefault(section.Id) ?? new(0, 0, 0, 0, 0);
        DefinitionSection? authored = form.FindAuthoredSection(section.Id);
        bool? applicability = authored?.EvaluateApplicability(facts.Validation);
        bool isApplicable = applicability ?? true;
        (int activeCount, int suppliedOptional) = facts.SectionActivity.GetValueOrDefault(section.Id);
        bool unresolved = applicability is null || SectionHasUnresolvedApplicability(form, facts, section);

        SectionConfigurationStatus status;
        if (progress.Invalid > 0)
            status = SectionConfigurationStatus.Invalid;
        else if (!isApplicable || (activeCount == 0 && authored?.ApplicableWhen is not null))
            status = SectionConfigurationStatus.OutsideSelectedScope;
        else if (progress.Missing > progress.Deferred)
            status = SectionConfigurationStatus.MissingRequired;
        else if (unresolved)
            status = SectionConfigurationStatus.UnresolvedApplicability;
        else if (progress.Deferred > 0)
            status = SectionConfigurationStatus.SecretDeferred;
        else if (progress.Required > 0 && progress.Ready == progress.Required)
            status = SectionConfigurationStatus.RequiredComplete;
        else if (suppliedOptional > 0)
            status = SectionConfigurationStatus.RequiredComplete;
        else
            status = SectionConfigurationStatus.OptionalUnset;

        SectionReviewCoverage coverage = form.GetSectionCoverage(section.Id, out bool hasNewlyRelevant);

        List<(string PathKey, string Label)>? hiddenCandidates = null;
        if (facts.SectionActivePaths.TryGetValue(section.Id, out HashSet<DefinitionPath>? activePaths))
        {
            bool continuous = form.EditorVisibility == DefinitionEditorVisibilityPolicy.Continuous;
            bool? collapsedSetting = form.TryGetSectionExpansion(section.Id);
            bool sectionCollapsed = collapsedSetting == false;
            if (continuous && isApplicable && !sectionCollapsed && form.AreContinuousPathsAlwaysVisible(section.Id))
            {
                hiddenCandidates = null;
            }
            else
            {
                foreach (DefinitionPath path in activePaths)
                {
                    bool visible = continuous
                        ? form.IsContinuousActivePathVisibleLocked(path, section.Id, isApplicable, sectionCollapsed)
                        : form.IsEditorVisible(path);
                    if (visible) continue;
                    string? label = form.GetEditorPresentation(path)?.DisplayLabel ?? (path.Segments.Count > 0 ? path.Segments[^1] : null);
                    if (!string.IsNullOrEmpty(label))
                        (hiddenCandidates ??= []).Add((path.ToString(), label));
                }
            }
        }
        System.Collections.ObjectModel.ReadOnlyCollection<string> hiddenTitles;
        if (hiddenCandidates is null)
        {
            hiddenTitles = Array.AsReadOnly(Array.Empty<string>());
        }
        else
        {
            hiddenCandidates.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.PathKey, b.PathKey));
            var distinctTitles = new List<string>(hiddenCandidates.Count);
            foreach (var (_, label) in hiddenCandidates)
                if (!distinctTitles.Contains(label, StringComparer.Ordinal))
                    distinctTitles.Add(label);
            hiddenTitles = Array.AsReadOnly(distinctTitles.ToArray());
        }

        return new SectionStatusProjection(
            section.Id,
            section.Title,
            progress,
            status,
            coverage,
            hasNewlyRelevant,
            isApplicable,
            hiddenTitles.Count,
            hiddenTitles,
            activeCount,
            suppliedOptional);
    }

    private static bool SectionHasUnresolvedApplicability(
        DefinitionFormModel form,
        DefinitionFormFacts facts,
        DefinitionFormSection section) =>
        facts.Validation.HasUndecidedDependencies &&
        form.DefinitionHasUndecidedControllersFor(section, facts.Validation);

    private static bool MatchesAnyPrefix(DefinitionPath path, DefinitionPath[] prefixes)
    {
        foreach (DefinitionPath prefix in prefixes)
        {
            if (path.Segments.Count < prefix.Segments.Count)
                continue;
            bool match = true;
            for (int index = 0; index < prefix.Segments.Count; index++)
            {
                if (!string.Equals(path.Segments[index], prefix.Segments[index], StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }
}
