namespace Configurator.Presentation;

using Configurator.Core.Definitions;

public sealed record DefinitionFormExperience
{
    internal DefinitionFormExperience(string id, string title, SetupFieldDisclosure initialDisclosure,
        string navigation, IEnumerable<string> sections)
    {
        Id = id;
        Title = title;
        InitialDisclosure = initialDisclosure;
        Navigation = navigation;
        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public SetupFieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
}

public sealed record DefinitionFormProgress(int Ready, int Required, int Missing, int Invalid, int Deferred)
{
    public int? Percent => Required == 0 ? null : 100 * Ready / Required;
}

public sealed record DefinitionFormSection
{
    internal DefinitionFormSection(string id, string title, string? description, IEnumerable<string> fields,
        SetupFieldDisclosure disclosure = SetupFieldDisclosure.Guided)
    {
        Id = id;
        Title = title;
        Description = description;
        Fields = Array.AsReadOnly(fields.ToArray());
        Disclosure = disclosure;
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<string> Fields { get; }
    public SetupFieldDisclosure Disclosure { get; }
}

/// <summary>Value-free workspace projection shared by native and browser clients.</summary>
public sealed class DefinitionFormProjection
{
    private readonly HashSet<string> _visibleFields;
    private readonly Lazy<DefinitionDiscoverySnapshot> _discovery;
    private readonly Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> _entries;
    private readonly Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> _relevantEntries;

    private DefinitionFormProjection(DefinitionFormProgress progress,
        Lazy<DefinitionDiscoverySnapshot> discovery, Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> entries,
        Lazy<IReadOnlyList<DefinitionDiscoveryEntry>> relevantEntries,
        IReadOnlyList<DefinitionFormSection> sections, IEnumerable<string> visibleFields,
        IReadOnlyDictionary<string, DefinitionFormProgress> sectionProgress)
    {
        Progress = progress;
        _discovery = discovery;
        _entries = entries;
        _relevantEntries = relevantEntries;
        Sections = sections;
        SectionProgress = sectionProgress;
        _visibleFields = visibleFields.ToHashSet(StringComparer.Ordinal);
    }

    private DefinitionFormProjection(DefinitionFormProjection current, Lazy<DefinitionDiscoverySnapshot> discovery)
    {
        Progress = current.Progress;
        _discovery = discovery;
        _entries = current._entries;
        Sections = current.Sections;
        SectionProgress = current.SectionProgress;
        _relevantEntries = current._relevantEntries;
        _visibleFields = current._visibleFields;
    }

    internal DefinitionFormProjection WithDiscovery(Lazy<DefinitionDiscoverySnapshot> discovery) => new(this, discovery);

    public DefinitionFormProgress Progress { get; }
    public DefinitionDiscoverySnapshot Discovery => _discovery.Value;
    public IReadOnlyList<DefinitionDiscoveryMatch> Matches => Discovery.Matches;
    public IReadOnlyList<DefinitionDiscoveryEntry> Entries => _entries.Value;
    /// <summary>Active relevant effects and their literal-path ancestors, before client paging.</summary>
    public IReadOnlyList<DefinitionDiscoveryEntry> RelevantEntries => _relevantEntries.Value;
    public IReadOnlyList<DefinitionFormSection> Sections { get; }
    public IReadOnlyDictionary<string, DefinitionFormProgress> SectionProgress { get; }
    public bool IsFieldVisible(string fieldId) => _visibleFields.Contains(fieldId);

    internal static DefinitionFormProjection Create(DefinitionFormModel form)
    {
        DefinitionFormFacts facts = form.Facts;
        return new(facts.Progress, form.CurrentDiscovery, form.OrderedEntries, form.CurrentRelevantEntries, form.OrderedSections,
            facts.Validation.Fields.Where(state => state.IsActive && form.IsEditorVisible([state.Id])).Select(state => state.Id),
            facts.SectionProgress);
    }
}
