namespace Configurator.Presentation.Store;

/// <summary>Immutable presentation card metadata and search matching for one store project.</summary>
public sealed class StoreProjectCardViewModel
{
    private static readonly char[] WordDelimiters = [' ', '-', '_', '.'];
    private readonly byte[] _iconBytes;

    public StoreProjectCardViewModel(DefinitionStoreProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Project = project;
        LatestRevisionEntry = project.Revisions.Count == 0
            ? null
            : project.Revisions.MaxBy(static revision => revision.Revision);
        TagsDisplay = project.Tags.Count == 0 ? string.Empty : string.Join(", ", project.Tags);
        _iconBytes = !string.IsNullOrEmpty(project.IconDataUri)
            && !string.IsNullOrWhiteSpace(project.IconLicense)
            && !string.IsNullOrWhiteSpace(project.IconAttribution)
            && StoreIconValidator.TryDecodePngDataUri(project.IconDataUri, out byte[]? decoded)
            ? decoded
            : [];
        Monogram = ComputeMonogram(project.Name, project.ProjectId);
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
    public int OutputCount => LatestRevisionEntry?.OutputNames.Count ?? 0;
    public string LatestDigest => LatestRevisionEntry?.Digest ?? string.Empty;
    public string? IconDataUri => HasIcon ? Project.IconDataUri : null;
    public string? IconLicense => HasIcon ? Project.IconLicense : null;
    public string? IconAttribution => HasIcon ? Project.IconAttribution : null;
    public bool HasIcon => _iconBytes.Length > 0;
    public ReadOnlyMemory<byte> IconBytes => _iconBytes;
    public string Monogram { get; }

    /// <summary>Returns true when all whitespace-separated query terms match project discovery metadata.</summary>
    public bool MatchesQueryTerm(string term) =>
        Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || ProjectId.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(term, StringComparison.OrdinalIgnoreCase)
        || ApplicationId.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Publisher.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string ComputeMonogram(string name, string projectId)
    {
        string source = string.IsNullOrWhiteSpace(name) ? projectId : name.Trim();
        string[] words = source.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length >= 2)
            return string.Create(2, (words[0][0], words[1][0]), static (span, pair) =>
            {
                span[0] = char.ToUpperInvariant(pair.Item1);
                span[1] = char.ToUpperInvariant(pair.Item2);
            });

        if (words.Length == 1 && words[0].Length > 0)
            return words[0].Length >= 2
                ? words[0][..2].ToUpperInvariant()
                : char.ToUpperInvariant(words[0][0]).ToString();

        return "C";
    }
}
