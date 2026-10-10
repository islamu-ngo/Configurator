namespace Configurator.Browser.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;
using Microsoft.JSInterop;

/// <summary>Discovers optional public assets before answers and admits only explicitly selected snapshots.</summary>
public sealed class DefinitionStoreCatalog(BrowserDefinitionRegistry registry, IJSRuntime javascript, BrowserOfflinePolicy policy) : IAsyncDisposable
{
    public const int MaximumIndexBytes = 4 * 1024 * 1024;
    private IJSObjectReference? _module;
    private IReadOnlyList<DefinitionStoreProject> _projects = [];
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _loaded;
    private bool _disposed;

    public async Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Guard(cancellationToken);
        if (_loaded) return _projects;
        long workspaceGeneration = policy.WorkspaceGeneration;
        byte[] bytes = await ReadAsync("index.json", MaximumIndexBytes, cancellationToken);
        try
        {
            IReadOnlyList<DefinitionStoreProject> projects = ParseIndex(bytes);
            Guard(cancellationToken);
            if (workspaceGeneration != policy.WorkspaceGeneration) throw new BrowserLaunchException("store-invalidated");
            _projects = projects;
            _loaded = true;
            return projects;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public async Task<SetupDefinition> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default)
    {
        Guard(cancellationToken);
        long workspaceGeneration = policy.WorkspaceGeneration;
        DefinitionStoreProject? project = _projects.SingleOrDefault(project => project.ProjectId == projectId);
        DefinitionStoreRevision? selected = project?.Revisions.SingleOrDefault(item => item.Revision == revision);
        if (project is null || selected is null) throw new BrowserLaunchException("store-revision-unavailable");
        byte[] bytes = await ReadAsync(selected.SnapshotPath, BrowserDefinitionSource.MaximumDocumentBytes, cancellationToken);
        try
        {
            SetupDefinition definition = SetupDefinitionCodec.ParseJson(bytes);
            if (definition.FamilyId != project.FamilyId || definition.ApplicationId != project.ApplicationId
                || definition.Revision != selected.Revision || definition.Digest.Value != selected.Digest
                || definition.Publisher != selected.Publisher || definition.Title != selected.Title
                || !definition.ApplicationVersions.SequenceEqual(selected.ApplicationVersions, StringComparer.Ordinal)
                || !definition.EntryPoints.SequenceEqual(selected.EntryPoints, StringComparer.Ordinal)
                || !definition.Locales.SequenceEqual(selected.Locales, StringComparer.Ordinal)
                || !definition.Artifacts.Select(artifact => artifact.FileName).SequenceEqual(selected.OutputNames, StringComparer.Ordinal))
                throw new BrowserLaunchException("store-snapshot-mismatch");
            Guard(cancellationToken);
            if (workspaceGeneration != policy.WorkspaceGeneration) throw new BrowserLaunchException("store-invalidated");
            registry.Admit(definition);
            return definition;
        }
        catch (SetupDefinitionException exception) { throw new BrowserLaunchException(exception.Code); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static IReadOnlyList<DefinitionStoreProject> ParseIndex(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumIndexBytes) throw InvalidIndex();
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement root = document.RootElement;
            Members(root, "schemaVersion", "projects");
            if (root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number || !root.GetProperty("schemaVersion").TryGetInt32(out int version) || version != 1) throw InvalidIndex();
            JsonElement projects = Array(root, "projects", 1024);
            var result = new List<DefinitionStoreProject>();
            var projectIds = new HashSet<string>(StringComparer.Ordinal);
            var revisions = new Dictionary<(Guid, long), string>();
            int revisionCount = 0;
            foreach (JsonElement project in projects.EnumerateArray())
            {
                Members(project, "projectId", "name", "description", "sourceRepository", "license", "tags", "applicationId", "familyId", "revisions");
                string projectId = Text(project, "projectId", 64);
                if (!Slug(projectId) || !projectIds.Add(projectId)) throw InvalidIndex();
                string familyId = Text(project, "familyId", 36);
                if (!Guid.TryParseExact(familyId, "D", out Guid family) || family == Guid.Empty || family.ToString("D") != familyId) throw InvalidIndex();
                string repository = Text(project, "sourceRepository", 2048);
                if (!Uri.TryCreate(repository, UriKind.Absolute, out Uri? repositoryUri) || repositoryUri.Scheme != Uri.UriSchemeHttps
                    || repositoryUri.UserInfo.Length != 0 || repositoryUri.Query.Length != 0 || repositoryUri.Fragment.Length != 0
                    || repositoryUri.HostNameType != UriHostNameType.Dns || repositoryUri.IsLoopback
                    || repository.Contains('\\') || repository.Contains('%')) throw InvalidIndex();
                var projectRevisions = new List<DefinitionStoreRevision>();
                var revisionIds = new HashSet<long>();
                JsonElement revisionArray = Array(project, "revisions", 64);
                if (revisionArray.GetArrayLength() == 0) throw InvalidIndex();
                foreach (JsonElement item in revisionArray.EnumerateArray())
                {
                    if (++revisionCount > 4096) throw InvalidIndex();
                    Members(item, "revision", "digest", "publisher", "title", "applicationVersions", "entryPoints", "locales", "outputNames", "snapshotPath");
                    if (item.GetProperty("revision").ValueKind != JsonValueKind.Number || !item.GetProperty("revision").TryGetInt64(out long revision) || revision < 1 || !revisionIds.Add(revision)) throw InvalidIndex();
                    string digest = Text(item, "digest", 64);
                    if (!ArtifactDigest.TryParse(digest, out ArtifactDigest parsedDigest) || parsedDigest.Value != digest) throw InvalidIndex();
                    if (revisions.TryGetValue((family, revision), out string? knownDigest) && knownDigest != digest) throw InvalidIndex();
                    revisions[(family, revision)] = digest;
                    string path = Text(item, "snapshotPath", 128);
                    if (path != $"definitions/{projectId}/{revision.ToString(CultureInfo.InvariantCulture)}.json") throw InvalidIndex();
                    projectRevisions.Add(new DefinitionStoreRevision
                    {
                        Revision = revision,
                        Digest = digest,
                        Publisher = Text(item, "publisher", 256, false),
                        Title = Text(item, "title", 256, false),
                        ApplicationVersions = Strings(item, "applicationVersions", 128, 128, false),
                        EntryPoints = Strings(item, "entryPoints", 128, 128, false),
                        Locales = Strings(item, "locales", 128, 128, false),
                        OutputNames = Strings(item, "outputNames", 64, 128, false),
                        SnapshotPath = path
                    });
                }
                var tags = Strings(project, "tags", 16, 32, true, true);
                if (tags.Any(tag => !Slug(tag))) throw InvalidIndex();
                result.Add(new DefinitionStoreProject
                {
                    ProjectId = projectId,
                    Name = Text(project, "name", 128),
                    Description = Text(project, "description", 2048),
                    SourceRepository = repository,
                    License = Text(project, "license", 128),
                    Tags = tags,
                    ApplicationId = Text(project, "applicationId", 256, false),
                    FamilyId = family,
                    Revisions = System.Array.AsReadOnly(projectRevisions.OrderByDescending(item => item.Revision).ToArray())
                });
            }
            return System.Array.AsReadOnly(result.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ThenBy(project => project.ProjectId, StringComparer.Ordinal).ToArray());
        }
        catch (JsonException) { throw InvalidIndex(); }
    }

    private static void Members(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw InvalidIndex();
        var remaining = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject()) if (!remaining.Remove(property.Name)) throw InvalidIndex();
        if (remaining.Count != 0) throw InvalidIndex();
    }

    private static JsonElement Array(JsonElement element, string name, int maximum)
    {
        JsonElement array = element.GetProperty(name);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maximum) throw InvalidIndex();
        return array;
    }

    private static string Text(JsonElement element, string name, int maximum, bool trimmed = true) => String(element.GetProperty(name), maximum, trimmed);

    private static string String(JsonElement element, int maximum, bool trimmed)
    {
        if (element.ValueKind != JsonValueKind.String) throw InvalidIndex();
        string value = element.GetString()!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || (trimmed && value != value.Trim()) || value.Any(char.IsControl)) throw InvalidIndex();
        return value;
    }

    private static bool Slug(string value) => value[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
        && value[^1] != '-' && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static System.Collections.ObjectModel.ReadOnlyCollection<string> Strings(JsonElement element, string name, int maximumCount, int maximumLength, bool allowEmpty, bool trimmed = false)
    {
        string[] values = Array(element, name, maximumCount).EnumerateArray().Select(item => String(item, maximumLength, trimmed)).ToArray();
        if ((!allowEmpty && values.Length == 0) || values.Distinct(StringComparer.Ordinal).Count() != values.Length) throw InvalidIndex();
        return System.Array.AsReadOnly(values);
    }

    private static BrowserLaunchException InvalidIndex() => new("store-index-invalid");

    private void Guard(CancellationToken cancellationToken)
    {
        if (_disposed || cancellationToken.IsCancellationRequested) throw new BrowserLaunchException("store-cancelled");
        if (policy.IsWorkspaceActive) throw new BrowserLaunchException("store-active");
    }

    private async Task<byte[]> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        long generation = ++_generation;
        long workspaceGeneration = policy.WorkspaceGeneration;
        _pending?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pending = cancellation;
        byte[]? bytes = null;
        try
        {
            if (_module is null)
            {
                IJSObjectReference module = await javascript.InvokeAsync<IJSObjectReference>("import", cancellation.Token, "./browser-store.js");
                if (_disposed) { await module.DisposeAsync(); throw new BrowserLaunchException("store-cancelled"); }
                if (_module is null) _module = module;
                else if (!ReferenceEquals(_module, module)) await module.DisposeAsync();
            }
            Guard(cancellation.Token);
            using CancellationTokenRegistration registration = cancellation.Token.Register(() => { _ = CancelReadAsync(); });
            bytes = await _module.InvokeAsync<byte[]>("readAsset", cancellation.Token, path, maximumBytes);
            Guard(cancellation.Token);
            if (generation != _generation || workspaceGeneration != policy.WorkspaceGeneration) throw new BrowserLaunchException("store-invalidated");
            if (bytes is null || bytes.Length is 0 || bytes.Length > maximumBytes) throw new BrowserLaunchException("store-size-invalid");
            byte[] result = bytes;
            bytes = null;
            return result;
        }
        catch (OperationCanceledException) { throw new BrowserLaunchException("store-cancelled"); }
        catch (ObjectDisposedException) { throw new BrowserLaunchException("store-cancelled"); }
        catch (JSException) { throw new BrowserLaunchException("store-unavailable"); }
        finally
        {
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            if (ReferenceEquals(_pending, cancellation)) _pending = null;
        }
    }

    private async Task CancelReadAsync()
    {
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("cancelRead"); }
        catch (JSException) { }
        catch (ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _pending?.Cancel();
        await CancelReadAsync();
        if (_module is not null) await _module.DisposeAsync();
        _module = null;
        _projects = [];
    }
}

public sealed record DefinitionStoreProject
{
    internal DefinitionStoreProject() { }
    public string ProjectId { get; internal init; } = string.Empty;
    public string Name { get; internal init; } = string.Empty;
    public string Description { get; internal init; } = string.Empty;
    public string SourceRepository { get; internal init; } = string.Empty;
    public string License { get; internal init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; internal init; } = [];
    public string ApplicationId { get; internal init; } = string.Empty;
    public Guid FamilyId { get; internal init; }
    public IReadOnlyList<DefinitionStoreRevision> Revisions { get; internal init; } = [];
    public override string ToString() => $"{nameof(DefinitionStoreProject)}:{ProjectId}";
}

public sealed record DefinitionStoreRevision
{
    internal DefinitionStoreRevision() { }
    public long Revision { get; internal init; }
    public string Digest { get; internal init; } = string.Empty;
    public string Publisher { get; internal init; } = string.Empty;
    public string Title { get; internal init; } = string.Empty;
    public IReadOnlyList<string> ApplicationVersions { get; internal init; } = [];
    public IReadOnlyList<string> EntryPoints { get; internal init; } = [];
    public IReadOnlyList<string> Locales { get; internal init; } = [];
    public IReadOnlyList<string> OutputNames { get; internal init; } = [];
    public string SnapshotPath { get; internal init; } = string.Empty;
    public override string ToString() => $"{nameof(DefinitionStoreRevision)}:{Revision}:{Digest}";
}
