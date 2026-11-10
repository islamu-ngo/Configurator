namespace Configurator.Presentation.Store;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>Loads and filters immutable store project cards on the caller's synchronization context.</summary>
public sealed partial class StoreCatalogViewModel : ObservableObject, IDisposable
{
    private static readonly char[] QueryDelimiters = [' ', '\t', '\r', '\n'];
    private readonly IDefinitionStoreProvider _provider;
    private IReadOnlyList<StoreProjectCardViewModel> _allProjects = [];
    private long _generation;
    private bool _disposed;

    public StoreCatalogViewModel(IDefinitionStoreProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    public ObservableCollection<StoreProjectCardViewModel> Projects { get; } = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasNoMatches;

    [ObservableProperty]
    private bool _loadFailed;

    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long generation = ++_generation;
        IsLoading = true;
        try
        {
            IReadOnlyList<DefinitionStoreProject> projects = await _provider.LoadAsync(cancellationToken);
            if (!IsCurrent(generation))
                return false;

            _allProjects = projects.Select(static project => new StoreProjectCardViewModel(project)).ToArray();
            LoadFailed = false;
            ApplyFilter();
            return true;
        }
        catch (OperationCanceledException) when (!IsCurrent(generation) || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (DefinitionStoreException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!IsCurrent(generation))
                return false;

            _allProjects = [];
            Projects.Clear();
            HasNoMatches = false;
            LoadFailed = true;
            return false;
        }
        finally
        {
            if (IsCurrent(generation))
                IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_generation;
        IsLoading = false;
    }

    private bool IsCurrent(long generation) => !_disposed && generation == _generation;

    private void ApplyFilter()
    {
        if (_disposed)
            return;

        string[] terms = (SearchQuery ?? string.Empty).Split(
            QueryDelimiters,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        StoreProjectCardViewModel[] filtered = _allProjects
            .Where(project => terms.All(project.MatchesQueryTerm))
            .ToArray();

        if (!Projects.SequenceEqual(filtered))
        {
            Projects.Clear();
            foreach (StoreProjectCardViewModel project in filtered)
                Projects.Add(project);
        }

        HasNoMatches = !LoadFailed && Projects.Count == 0;
    }
}
