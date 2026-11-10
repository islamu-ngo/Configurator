namespace Configurator.Presentation.Store;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;

/// <summary>
/// Parses bounded store index manifests and verifies canonical snapshot metadata parity.
/// </summary>
public static class DefinitionStoreIndexParser
{
    public const int MaximumIndexBytes = 4 * 1024 * 1024;
    private const int MaximumProjects = 1024;
    private const int MaximumTotalRevisions = 4096;
    private const int MaximumProjectRevisions = 64;

    /// <summary>
    /// Verifies that an admitted <see cref="Definition"/> matches the index metadata
    /// selected by <paramref name="project"/> and <paramref name="revision"/>.
    /// </summary>
    public static bool MatchesDefinition(
        DefinitionStoreProject project,
        DefinitionStoreRevision revision,
        Definition definition)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(definition);

        return definition.FamilyId == project.FamilyId
            && definition.ApplicationId == project.ApplicationId
            && definition.Revision == revision.Revision
            && definition.Digest.Value == revision.Digest
            && definition.Publisher == revision.Publisher
            && definition.Title == revision.Title
            && definition.ApplicationVersions.SequenceEqual(revision.ApplicationVersions, StringComparer.Ordinal)
            && definition.EntryPoints.SequenceEqual(revision.EntryPoints, StringComparer.Ordinal)
            && definition.Locales.SequenceEqual(revision.Locales, StringComparer.Ordinal)
            && definition.Artifacts.Select(artifact => artifact.FileName)
                .SequenceEqual(revision.OutputNames, StringComparer.Ordinal);
    }

    /// <summary>
    /// Parses and validates a store <c>index.json</c> payload within closed schema and size bounds.
    /// </summary>
    public static IReadOnlyList<DefinitionStoreProject> ParseIndex(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumIndexBytes)
            throw InvalidIndex();

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement root = document.RootElement;
            RequireExactMembers(root, "schemaVersion", "projects");
            if (root.GetProperty("schemaVersion").ValueKind != JsonValueKind.String
                || root.GetProperty("schemaVersion").GetString() != ConfiguratorVersion.Current)
                throw InvalidIndex();

            JsonElement projects = ReadBoundedArray(root, "projects", MaximumProjects);
            var result = new List<DefinitionStoreProject>();
            var projectIds = new HashSet<string>(StringComparer.Ordinal);
            var revisions = new Dictionary<(Guid, long), string>();
            int totalRevisions = 0;

            foreach (JsonElement project in projects.EnumerateArray())
            {
                result.Add(ParseProject(project, projectIds, revisions, ref totalRevisions));
            }

            return Array.AsReadOnly(result
                .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(project => project.ProjectId, StringComparer.Ordinal)
                .ToArray());
        }
        catch (JsonException)
        {
            throw InvalidIndex();
        }
    }

    private static DefinitionStoreProject ParseProject(
        JsonElement project,
        HashSet<string> projectIds,
        Dictionary<(Guid, long), string> revisions,
        ref int totalRevisions)
    {
        RequireExactMembers(project,
            "projectId", "name", "description", "sourceRepository", "license",
            "tags", "applicationId", "familyId", "revisions");

        string projectId = ReadStringProperty(project, "projectId", 64);
        if (!IsSlug(projectId) || !projectIds.Add(projectId))
            throw InvalidIndex();

        Guid family = ParseCanonicalGuid(ReadStringProperty(project, "familyId", 36));
        string repository = ValidateRepositoryUri(ReadStringProperty(project, "sourceRepository", 2048));
        var projectRevisions = ParseRevisions(project, projectId, family, revisions, ref totalRevisions);

        ReadOnlyCollection<string> tags = ReadStringArray(project, "tags", 16, 32, allowEmpty: true, trimmed: true);
        if (tags.Any(tag => !IsSlug(tag)))
            throw InvalidIndex();

        return new DefinitionStoreProject
        {
            ProjectId = projectId,
            Name = ReadStringProperty(project, "name", 128),
            Description = ReadStringProperty(project, "description", 2048),
            SourceRepository = repository,
            License = ReadStringProperty(project, "license", 128),
            Tags = tags,
            ApplicationId = ReadStringProperty(project, "applicationId", 256, trimmed: false),
            FamilyId = family,
            Revisions = Array.AsReadOnly(projectRevisions.OrderByDescending(item => item.Revision).ToArray())
        };
    }

    private static List<DefinitionStoreRevision> ParseRevisions(
        JsonElement project,
        string projectId,
        Guid family,
        Dictionary<(Guid, long), string> revisions,
        ref int totalRevisions)
    {
        var projectRevisions = new List<DefinitionStoreRevision>();
        var revisionIds = new HashSet<long>();
        JsonElement revisionArray = ReadBoundedArray(project, "revisions", MaximumProjectRevisions);
        if (revisionArray.GetArrayLength() == 0)
            throw InvalidIndex();

        foreach (JsonElement item in revisionArray.EnumerateArray())
        {
            if (++totalRevisions > MaximumTotalRevisions)
                throw InvalidIndex();

            RequireExactMembers(item,
                "revision", "digest", "publisher", "title",
                "applicationVersions", "entryPoints", "locales", "outputNames", "snapshotPath");

            JsonElement revisionElement = item.GetProperty("revision");
            if (revisionElement.ValueKind != JsonValueKind.Number
                || !revisionElement.TryGetInt64(out long revision)
                || revision < 1
                || !revisionIds.Add(revision))
                throw InvalidIndex();

            string digest = ReadStringProperty(item, "digest", 64);
            if (!ArtifactDigest.TryParse(digest, out ArtifactDigest parsedDigest) || parsedDigest.Value != digest)
                throw InvalidIndex();
            if (revisions.TryGetValue((family, revision), out string? knownDigest) && knownDigest != digest)
                throw InvalidIndex();
            revisions[(family, revision)] = digest;

            string path = ReadStringProperty(item, "snapshotPath", 128);
            if (path != $"definitions/{projectId}/{revision.ToString(CultureInfo.InvariantCulture)}.json")
                throw InvalidIndex();

            projectRevisions.Add(new DefinitionStoreRevision
            {
                Revision = revision,
                Digest = digest,
                Publisher = ReadStringProperty(item, "publisher", 256, trimmed: false),
                Title = ReadStringProperty(item, "title", 256, trimmed: false),
                ApplicationVersions = ReadStringArray(item, "applicationVersions", 128, 128, allowEmpty: false),
                EntryPoints = ReadStringArray(item, "entryPoints", 128, 128, allowEmpty: false),
                Locales = ReadStringArray(item, "locales", 128, 128, allowEmpty: false),
                OutputNames = ReadStringArray(item, "outputNames", 64, 128, allowEmpty: false),
                SnapshotPath = path
            });
        }

        return projectRevisions;
    }

    private static Guid ParseCanonicalGuid(string familyId)
    {
        if (!Guid.TryParseExact(familyId, "D", out Guid family)
            || family == Guid.Empty
            || family.ToString("D") != familyId)
            throw InvalidIndex();
        return family;
    }

    private static string ValidateRepositoryUri(string repository)
    {
        if (!Uri.TryCreate(repository, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || uri.HostNameType != UriHostNameType.Dns
            || uri.IsLoopback
            || repository.Contains('\\', StringComparison.Ordinal)
            || repository.Contains('%', StringComparison.Ordinal))
            throw InvalidIndex();
        return repository;
    }

    private static void RequireExactMembers(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw InvalidIndex();

        var remaining = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
                throw InvalidIndex();
        }
        if (remaining.Count != 0)
            throw InvalidIndex();
    }

    private static JsonElement ReadBoundedArray(JsonElement element, string name, int maximum)
    {
        JsonElement array = element.GetProperty(name);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maximum)
            throw InvalidIndex();
        return array;
    }

    private static string ReadStringProperty(JsonElement element, string name, int maximum, bool trimmed = true) =>
        ReadString(element.GetProperty(name), maximum, trimmed);

    private static string ReadString(JsonElement element, int maximum, bool trimmed)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw InvalidIndex();
        string value = element.GetString()!;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximum
            || (trimmed && value != value.Trim())
            || value.Any(char.IsControl))
            throw InvalidIndex();
        return value;
    }

    private static bool IsSlug(string value) =>
        value[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
        && value[^1] != '-'
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static ReadOnlyCollection<string> ReadStringArray(
        JsonElement element,
        string name,
        int maximumCount,
        int maximumLength,
        bool allowEmpty,
        bool trimmed = false)
    {
        string[] values = ReadBoundedArray(element, name, maximumCount)
            .EnumerateArray()
            .Select(item => ReadString(item, maximumLength, trimmed))
            .ToArray();
        if ((!allowEmpty && values.Length == 0)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw InvalidIndex();
        return Array.AsReadOnly(values);
    }

    private static DefinitionStoreException InvalidIndex() => new("store-index-invalid");
}
