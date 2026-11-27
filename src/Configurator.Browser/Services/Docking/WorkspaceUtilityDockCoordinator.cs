namespace Configurator.Browser.Services.Docking;

/// <summary>
/// Single-active coordinator over <see cref="DockLayoutState"/> for Workspace End-Dock utilities.
/// Eliminates drifting visibility booleans and enforces dirty Document (JSON) resolution before switching or closing.
/// </summary>
public sealed class WorkspaceUtilityDockCoordinator : IDisposable
{
    private readonly DockLayoutState _dockLayoutState;
    private readonly Func<Task<bool>> _resolvePendingDocumentAsync;
    private readonly Action _retireDocument;
    private readonly Action<WorkspaceUtilityKind>? _onBeforeOpen;
    private bool _disposed;

    public WorkspaceUtilityDockCoordinator(
        DockLayoutState dockLayoutState,
        Func<Task<bool>> resolvePendingDocumentAsync,
        Action retireDocument,
        Action<WorkspaceUtilityKind>? onBeforeOpen = null)
    {
        ArgumentNullException.ThrowIfNull(dockLayoutState);
        ArgumentNullException.ThrowIfNull(resolvePendingDocumentAsync);
        ArgumentNullException.ThrowIfNull(retireDocument);

        _dockLayoutState = dockLayoutState;
        _resolvePendingDocumentAsync = resolvePendingDocumentAsync;
        _retireDocument = retireDocument;
        _onBeforeOpen = onBeforeOpen;
        _dockLayoutState.Changed += OnDockLayoutChanged;
    }

    public event Action? Changed;

    public WorkspaceUtilityKind? ActiveUtility
    {
        get
        {
            foreach (WorkspaceUtilityDescriptor descriptor in WorkspaceUtilities.All)
            {
                DockPanelEntry? entry = _dockLayoutState.GetPanel(descriptor.PanelId);
                if (entry is { State.IsOpen: true }
                    && (!_dockLayoutState.ShouldRenderDockedPanelAsOverlay(entry) || entry.State.IsActive))
                {
                    return descriptor.Kind;
                }
            }

            return null;
        }
    }

    public DockPanelId? ActivePanelId =>
        ActiveUtility is { } kind ? WorkspaceUtilities.Get(kind).PanelId : null;

    public bool IsAnyOpen => ActiveUtility is not null;

    public bool IsOpen(WorkspaceUtilityKind kind) => ActiveUtility == kind;

    public async Task<bool> OpenAsync(
        WorkspaceUtilityKind kind,
        DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction)
    {
        if (_disposed) return false;

        if (ActiveUtility == WorkspaceUtilityKind.DocumentJson && kind != WorkspaceUtilityKind.DocumentJson)
        {
            if (!await _resolvePendingDocumentAsync())
                return false;
            if (_disposed) return false;
            _retireDocument();
        }

        _onBeforeOpen?.Invoke(kind);
        _dockLayoutState.OpenExclusive(WorkspaceUtilities.Get(kind).PanelId, reason);
        return true;
    }

    public Task<bool> ToggleAsync(
        WorkspaceUtilityKind kind,
        DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction) =>
        IsOpen(kind) ? CloseAsync(reason) : OpenAsync(kind, reason);

    public async Task<bool> CloseAsync(DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction)
    {
        if (_disposed) return false;

        if (ActiveUtility == WorkspaceUtilityKind.DocumentJson)
        {
            if (!await _resolvePendingDocumentAsync())
                return false;
            if (_disposed) return false;
            _retireDocument();
        }

        foreach (WorkspaceUtilityDescriptor descriptor in WorkspaceUtilities.All)
        {
            DockPanelEntry? entry = _dockLayoutState.GetPanel(descriptor.PanelId);
            if (entry is { State.IsOpen: true })
                _dockLayoutState.Close(descriptor.PanelId, reason);
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dockLayoutState.Changed -= OnDockLayoutChanged;
    }

    private void OnDockLayoutChanged()
    {
        if (_disposed) return;
        Changed?.Invoke();
    }
}
