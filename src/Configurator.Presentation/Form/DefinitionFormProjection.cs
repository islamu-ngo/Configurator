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
        IReadOnlyList<ArtifactDefinition> eligibleArtifacts)
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
        EligibleArtifacts = eligibleArtifacts;
    }

    private DefinitionFormProjection(DefinitionFormProjection current, Lazy<DefinitionDiscoverySnapshot> discovery)
    {
        Progress = current.Progress;
        _discovery = discovery;
        _entries = current._entries;
        Sections = current.Sections;
        SectionProgress = current.SectionProgress;
        SectionStatuses = current.SectionStatuses;
        ProgressSummary = current.ProgressSummary;
        Navigation = current.Navigation;
        PresentationOptions = current.PresentationOptions;
        _relevantEntries = current._relevantEntries;
        _visibleFields = current._visibleFields;
        ConfigurationContext = current.ConfigurationContext;
        EligibleArtifacts = current.EligibleArtifacts;
    }

    internal DefinitionFormProjection WithDiscovery(Lazy<DefinitionDiscoverySnapshot> discovery) => new(this, discovery);

    public DefinitionFormProgress Progress { get; }
    public DefinitionConfigurationContext? ConfigurationContext { get; }
    public IReadOnlyList<ArtifactDefinition> EligibleArtifacts { get; }
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
        if (facts.Validation.ConfigurationContext is not null)
        {
            baseSections = Array.AsReadOnly(baseSections.Select(section => new DefinitionFormSection(
                section.Id, section.Title, section.Description,
                section.Fields.Where(fieldId => !form.IsConfigurationSelector([fieldId])),
                section.Disclosure, section.Pointers, section.Groups, section.IsApplicable))
                .Where(section => section.Fields.Count > 0 || section.Pointers.Count > 0).ToArray());
        }

        // Enrich sections with runtime applicability + auto-expanded error/missing groups.
        DefinitionFormSection[] enrichedSections = baseSections.Select(section => EnrichSection(form, facts, section)).ToArray();

        // Build 2D SectionStatusProjection for every declared/active section.
        var statuses = new Dictionary<string, SectionStatusProjection>(StringComparer.Ordinal);
        foreach (DefinitionFormSection section in enrichedSections)
        {
            statuses[section.Id] = BuildSectionStatus(form, facts, section);
        }

        var readOnlyStatuses = new ReadOnlyDictionary<string, SectionStatusProjection>(statuses);
        int activeSections = statuses.Values.Count(status => status.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope);
        int openedSections = statuses.Values.Count(status =>
            status.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope &&
            status.Coverage != SectionReviewCoverage.NotVisited);
        int reviewedSections = statuses.Values.Count(status =>
            status.ConfigurationStatus != SectionConfigurationStatus.OutsideSelectedScope &&
            status.Coverage == SectionReviewCoverage.Reviewed);

        var progressSummary = new WorkspaceProgressSummary(
            facts.Progress.Ready,
            facts.Progress.Required,
            facts.Progress.Missing,
            facts.Progress.Invalid,
            facts.Progress.Deferred,
            activeSections,
            openedSections,
            reviewedSections);

        DefinitionFormExperience activeExperience = form.Experiences.Single(item => item.Id == form.SelectedExperienceId);
        WorkspaceNavigationProjection navigation = WorkspaceNavigationProjection.Create(
            activeExperience, enrichedSections, readOnlyStatuses, form.CurrentSectionId);

        return new DefinitionFormProjection(
            facts.Progress,
            form.CurrentDiscovery,
            form.OrderedEntries,
            form.CurrentRelevantEntries,
            Array.AsReadOnly(enrichedSections),
            facts.Validation.Fields.Where(state => state.IsActive &&
                form.Fields.Any(field => field.Definition.Id == state.Id) && form.IsEditorVisible([state.Id])).Select(state => state.Id),
            facts.SectionProgress,
            readOnlyStatuses,
            progressSummary,
            navigation,
            form.PresentationOptions,
            facts.Validation.ConfigurationContext,
            form.EligibleArtifacts);
    }

    private static DefinitionFormSection EnrichSection(
        DefinitionFormModel form,
        DefinitionFormFacts facts,
        DefinitionFormSection section)
    {
        DefinitionSection? authored = form.Definition.Presentation?.Sections.FirstOrDefault(s => s.Id == section.Id);
        bool isApplicable = authored?.IsApplicable(facts.Validation) ?? true;
        if (section.Groups.Count == 0 && authored is null)
            return section;

        var enrichedGroups = new List<DefinitionFormSectionGroup>(section.Groups.Count);
        foreach (DefinitionFormSectionGroup group in section.Groups)
        {
            DefinitionSectionGroup? authoredGroup = authored?.Groups.FirstOrDefault(g => g.Id == group.Id);
            bool groupApplicable = authoredGroup?.IsApplicable(facts.Validation) ?? group.IsApplicable;
            DefinitionPath[] groupPaths = group.Pointers.Select(DefinitionPath.Parse).ToArray();
            bool hasBlocking = facts.CorrectionPaths.Any(path => MatchesAnyPrefix(path, groupPaths)) ||
                facts.InputDiagnostics.Any(diagnostic =>
                {
                    DefinitionPath diagPath = diagnostic.FieldId.Length > 0
                        ? form.DataPath([diagnostic.FieldId, .. diagnostic.RelativePath])
                        : DefinitionPath.FromSegments(diagnostic.RelativePath);
                    return MatchesAnyPrefix(diagPath, groupPaths);
                });
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
        DefinitionSection? authored = form.Definition.Presentation?.Sections.FirstOrDefault(s => s.Id == section.Id);
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

        var hiddenTitles = new List<string>();
        if (facts.SectionActivePaths.TryGetValue(section.Id, out HashSet<DefinitionPath>? activePaths))
        {
            foreach (DefinitionPath path in activePaths.OrderBy(p => p.ToString(), StringComparer.Ordinal))
            {
                if (form.IsEditorVisible(path)) continue;
                string? label = form.GetEditorPresentation(path)?.DisplayLabel ?? (path.Segments.Count > 0 ? path.Segments[^1] : null);
                if (!string.IsNullOrEmpty(label) && !hiddenTitles.Contains(label, StringComparer.Ordinal))
                    hiddenTitles.Add(label);
            }
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
            Array.AsReadOnly(hiddenTitles.ToArray()),
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
            if (path.Segments.Count >= prefix.Segments.Count &&
                path.Segments.Take(prefix.Segments.Count).SequenceEqual(prefix.Segments, StringComparer.Ordinal))
                return true;
        }
        return false;
    }
}
