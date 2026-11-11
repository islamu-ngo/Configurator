using Configurator.Core.Definitions;
using Configurator.Presentation.Store;

namespace Configurator.Browser.Services;

/// <summary>Enforces browser custody, workspace generation fencing, and registry admission over an <see cref="IDefinitionStoreProvider"/>.</summary>
public sealed class DefinitionStoreCatalog(
    IDefinitionStoreProvider storeProvider,
    BrowserDefinitionRegistry registry,
    BrowserOfflinePolicy policy) : IDisposable, IAsyncDisposable
{
    private long _generation;
    private bool _disposed;

    public async Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default)
    {
        (long generation, long workspaceGeneration) = BeginOperation(cancellationToken);
        try
        {
            IReadOnlyList<DefinitionStoreProject> projects = await storeProvider.LoadAsync(cancellationToken);
            EnsureStillValid(generation, workspaceGeneration, cancellationToken);
            return projects;
        }
        catch (DefinitionStoreException exception)
        {
            throw new BrowserLaunchException(exception.Code);
        }
        catch (OperationCanceledException)
        {
            throw new BrowserLaunchException("store-cancelled");
        }
        catch (ObjectDisposedException)
        {
            throw new BrowserLaunchException("store-cancelled");
        }
    }

    public async Task<Definition> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        (long generation, long workspaceGeneration) = BeginOperation(cancellationToken);
        try
        {
            DefinitionStoreResult result = await storeProvider.OpenAsync(projectId, revision, cancellationToken);
            EnsureStillValid(generation, workspaceGeneration, cancellationToken);

            Definition definition = result switch
            {
                DefinitionStoreResult.Success success => success.Definition,
                DefinitionStoreResult.ProjectNotFound => throw new BrowserLaunchException("store-revision-unavailable"),
                DefinitionStoreResult.RevisionNotFound => throw new BrowserLaunchException("store-revision-unavailable"),
                DefinitionStoreResult.SnapshotMismatch => throw new BrowserLaunchException("store-snapshot-mismatch"),
                DefinitionStoreResult.CorruptAsset => throw new BrowserLaunchException("store-unavailable"),
                _ => throw new BrowserLaunchException("store-unavailable")
            };

            registry.Admit(definition);
            return definition;
        }
        catch (DefinitionStoreException exception)
        {
            throw new BrowserLaunchException(exception.Code);
        }
        catch (DefinitionException exception)
        {
            throw new BrowserLaunchException(exception.Code);
        }
        catch (OperationCanceledException)
        {
            throw new BrowserLaunchException("store-cancelled");
        }
        catch (ObjectDisposedException)
        {
            throw new BrowserLaunchException("store-cancelled");
        }
    }

    private (long Generation, long WorkspaceGeneration) BeginOperation(CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        return (++_generation, policy.WorkspaceGeneration);
    }

    private void EnsureStillValid(long generation, long workspaceGeneration, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        if (generation != _generation || workspaceGeneration != policy.WorkspaceGeneration)
            throw new BrowserLaunchException("store-invalidated");
    }

    private void Guard(CancellationToken cancellationToken)
    {
        if (_disposed || cancellationToken.IsCancellationRequested)
            throw new BrowserLaunchException("store-cancelled");
        if (policy.IsWorkspaceActive)
            throw new BrowserLaunchException("store-active");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
