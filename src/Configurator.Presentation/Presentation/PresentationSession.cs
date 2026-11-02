namespace Configurator.Presentation;

using CommunityToolkit.Mvvm.Messaging;

public sealed class PresentationSession : IDisposable
{
    private readonly IOperationGenerationAllocator _allocator;
    private readonly object _gate = new();
    private readonly IMessenger _messenger;
    private readonly List<PresentationWorkspace> _workspaces = [];
    private OperationGeneration _lastGeneration;
    private bool _disposed;
    private bool _isTerminated;

    public PresentationSession(IMessenger messenger)
        : this(messenger, new OperationGenerationAllocator())
    {
    }

    public PresentationSession(
        IMessenger messenger,
        IOperationGenerationAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(allocator);
        _messenger = messenger;
        _allocator = allocator;
        SessionId = Guid.CreateVersion7();
    }

    public bool IsTerminated
    {
        get
        {
            lock (_gate)
                return _isTerminated;
        }
    }

    public Guid SessionId { get; }

    public PresentationWorkspace CreateWorkspace(
        WorkspaceId workspaceId,
        IPresentationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isTerminated)
                throw new InvalidOperationException("presentation-session-terminated");

            var workspace = new PresentationWorkspace(
                this,
                _messenger,
                workspaceId,
                operation);
            _workspaces.Add(workspace);
            return workspace;
        }
    }

    internal bool TryAllocate(out OperationGeneration generation)
    {
        PresentationWorkspace[] terminate = [];
        lock (_gate)
        {
            if (_disposed || _isTerminated)
            {
                generation = default;
                return false;
            }

            if (_allocator.TryAllocate(out generation)
                && generation.Value > _lastGeneration.Value)
            {
                _lastGeneration = generation;
                return true;
            }

            generation = default;
            _isTerminated = true;
            terminate = [.. _workspaces];
        }

        foreach (PresentationWorkspace workspace in terminate)
            workspace.TerminateFromSession();
        return false;
    }

    internal void Release(PresentationWorkspace workspace)
    {
        lock (_gate)
            _workspaces.Remove(workspace);
    }

    public void Dispose()
    {
        PresentationWorkspace[] terminate;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _isTerminated = true;
            terminate = [.. _workspaces];
            _workspaces.Clear();
        }

        foreach (PresentationWorkspace workspace in terminate)
            workspace.TerminateFromSession();
    }
}
