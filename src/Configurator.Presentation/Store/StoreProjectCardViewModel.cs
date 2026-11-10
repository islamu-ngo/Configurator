namespace Configurator.Presentation.Store;

/// <summary>Immutable presentation card metadata and search matching for one store project.</summary>
public sealed class StoreProjectCardViewModel
{
    public StoreProjectCardViewModel(DefinitionStoreProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Project = project;
        LatestRevisionEntry = project.Revisions.Count == 0
            ? null
            : project.Revisions.MaxBy(static revision => revision.Revision);
        TagsDisplay = project.Tags.Count == 0 ? string.Empty : string.Join(", ", project.Tags);
    }

    public DefinitionStoreProject Project { get; }
    public DefinitionStoreRevision? LatestRevisionEntry { get; }
    public string ProjectId => Project.ProjectId;
    public string Name => Project.Name;
    public string Description => Project.Description;
    public string ApplicationId => Project.ApplicationId;
    public string Publisher => LatestRevisionEntry?.Publisher ?? string.Empty;
    public IReadOnlyList<string> Tags => Project.Tags;
    public string TagsDisplay { get; }
    public bool HasTags => Project.Tags.Count > 0;
    public long LatestRevision => LatestRevisionEntry?.Revision ?? 0;

    /// <summary>Returns true when all whitespace-separated query terms match project discovery metadata.</summary>
    public bool MatchesQueryTerm(string term) =>
        Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || ProjectId.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(term, StringComparison.OrdinalIgnoreCase)
        || ApplicationId.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Publisher.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));
}
