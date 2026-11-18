namespace Configurator.Browser.Services.Docking;

using Microsoft.AspNetCore.Components;

internal readonly record struct ActivationGroupKey(DockScope Scope, DockSide? Side);

internal readonly record struct DockSideAllocationSlot(
    DockPanelId[] PanelIds,
    int Width,
    int Priority,
    int TieOrder);

/// <summary>Manages docked and overlay panel state, clamping, and responsive degradation across shell and workspace panes.</summary>
public sealed class DockLayoutState : IDockPanelRegistry
{
    public const int DefaultMinimumContentWidth = 420;

    private readonly Dictionary<DockPanelId, DockPanelEntry> _entries = [];
    private readonly Dictionary<DockScope, int> _minimumContentWidths = [];
    private readonly HashSet<DockPanelId> _userActivatedInOverlay = [];
    private int _viewportWidth;
    private bool _isMobileViewport;

    public event Action? Changed;

    public DockLayoutChangeReason LastChangeReason { get; private set; } = DockLayoutChangeReason.None;

    /// <summary>Gets the most recently reported browser viewport width in CSS pixels, or 0 before measurement.</summary>
    public int ViewportWidth => _viewportWidth;

    public bool IsMobileViewport => _isMobileViewport;

    public void Register(DockPanelDescriptor descriptor, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(content);

        descriptor = descriptor.Validate();

        if (_entries.ContainsKey(descriptor.Id))
            throw new InvalidOperationException($"Dock panel '{descriptor.Id}' is already registered.");

        DockPanelState state = CreateDefaultState(descriptor);
        _entries.Add(descriptor.Id, new DockPanelEntry(descriptor, content, state));
        NotifyChanged(DockLayoutChangeReason.Registration);
    }

    public void RegisterOrUpdate(DockPanelDescriptor descriptor, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(content);

        descriptor = descriptor.Validate();

        if (!_entries.TryGetValue(descriptor.Id, out DockPanelEntry? existing))
        {
            DockPanelState state = CreateDefaultState(descriptor);
            _entries.Add(descriptor.Id, new DockPanelEntry(descriptor, content, state));
            NotifyChanged(DockLayoutChangeReason.Registration);
            return;
        }

        DockPanelState clampedState = existing.State with
        {
            Width = Math.Clamp(existing.State.Width, descriptor.MinWidth, descriptor.MaxWidth),
            Order = descriptor.Order
        };
        _entries[descriptor.Id] = new DockPanelEntry(descriptor, content, clampedState);
        NotifyChanged(DockLayoutChangeReason.Refresh);
    }

    public void Unregister(DockPanelId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        _userActivatedInOverlay.Remove(id);
        if (!_entries.Remove(id, out DockPanelEntry? removed))
            return;

        if (!_entries.Values.Any(entry => entry.Descriptor.Scope == removed.Descriptor.Scope))
            _minimumContentWidths.Remove(removed.Descriptor.Scope);

        SanitizeOverlayActiveStates();
        NotifyChanged(DockLayoutChangeReason.Registration);
    }

    /// <summary>Removes any explicit minimum content width floor registered for <paramref name="scope"/>.</summary>
    public void ClearMinimumContentWidth(DockScope scope)
    {
        if (!_minimumContentWidths.Remove(scope))
            return;

        SanitizeOverlayActiveStates();
        NotifyChanged(DockLayoutChangeReason.ViewportPolicy);
    }

    public IReadOnlyList<DockPanelEntry> GetPanels(DockScope scope, DockSide side)
    {
        return _entries.Values
            .Where(entry => entry.Descriptor.Scope == scope && entry.Descriptor.Side == side)
            .OrderBy(entry => entry.State.Order)
            .ThenBy(entry => entry.Descriptor.Title, StringComparer.Ordinal)
            .ToArray();
    }

    public DockPanelEntry? GetPanel(DockPanelId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _entries.GetValueOrDefault(id);
    }

    public int GetEffectiveDockedWidth(DockScope scope, DockSide side)
    {
        if (_isMobileViewport)
            return 0;

        HashSet<DockPanelId> overlayIds = ComputeOverlayPanelIds();
        DockPanelEntry[] docked = _entries.Values
            .Where(entry => entry.Descriptor.Scope == scope
                && entry.Descriptor.Side == side
                && entry.State is { IsOpen: true, Mode: DockMode.Docked }
                && !overlayIds.Contains(entry.Descriptor.Id))
            .OrderBy(entry => entry.State.Order)
            .ToArray();

        if (docked.Length == 0)
            return 0;

        if (docked.Length > 1 && docked.All(entry => entry.Descriptor.StackStrategy == DockPanelStackStrategy.Split))
            return docked.Sum(entry => entry.State.Width);

        DockPanelEntry active = docked.FirstOrDefault(entry => entry.State.IsActive) ?? docked[0];
        return active.State.Width;
    }

    public bool ShouldRenderDockedPanelAsOverlay(DockPanelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.State is not { IsOpen: true, Mode: DockMode.Docked })
            return false;

        if (_isMobileViewport)
            return true;

        if (_viewportWidth <= 0)
            return false;

        DockPanelEntry? hypothetical = !_entries.TryGetValue(entry.Descriptor.Id, out DockPanelEntry? stored)
            || stored != entry
            ? entry
            : null;

        return ComputeOverlayPanelIds(hypothetical).Contains(entry.Descriptor.Id);
    }

    private HashSet<DockPanelId> ComputeOverlayPanelIds(DockPanelEntry? hypotheticalEntry = null)
    {
        IEnumerable<DockPanelEntry> effectiveEntries = hypotheticalEntry is null
            ? _entries.Values
            : _entries.Values
                .Where(entry => entry.Descriptor.Id != hypotheticalEntry.Descriptor.Id)
                .Append(hypotheticalEntry);

        DockPanelEntry[] openDocked = effectiveEntries
            .Where(entry => entry.State is { IsOpen: true, Mode: DockMode.Docked })
            .ToArray();

        if (openDocked.Length == 0)
            return [];

        if (_isMobileViewport)
            return openDocked.Select(entry => entry.Descriptor.Id).ToHashSet();

        if (_viewportWidth <= 0)
            return [];

        int effectiveMinimumContentWidth = _minimumContentWidths.Count == 0
            ? DefaultMinimumContentWidth
            : _minimumContentWidths.Values.Max();

        var slots = new List<DockSideAllocationSlot>();
        foreach (IGrouping<(DockScope Scope, DockSide Side), DockPanelEntry> group in openDocked
            .Where(entry => entry.Descriptor.Side is DockSide.Start or DockSide.End)
            .GroupBy(entry => (entry.Descriptor.Scope, entry.Descriptor.Side)))
        {
            DockPanelEntry[] groupEntries = group.OrderBy(entry => entry.State.Order).ToArray();
            if (groupEntries.Length > 1
                && groupEntries.All(entry => entry.Descriptor.StackStrategy == DockPanelStackStrategy.Split))
            {
                foreach (DockPanelEntry entry in groupEntries)
                {
                    slots.Add(new DockSideAllocationSlot(
                        [entry.Descriptor.Id],
                        entry.State.Width,
                        entry.Descriptor.ResponsivePriority,
                        entry.State.Order));
                }

                continue;
            }

            DockPanelEntry active = groupEntries.FirstOrDefault(entry => entry.State.IsActive) ?? groupEntries[0];
            slots.Add(new DockSideAllocationSlot(
                groupEntries.Select(entry => entry.Descriptor.Id).ToArray(),
                active.State.Width,
                groupEntries.Max(entry => entry.Descriptor.ResponsivePriority),
                active.State.Order));
        }

        int remainingContentWidth = _viewportWidth;
        var overlayIds = new HashSet<DockPanelId>();

        foreach (DockSideAllocationSlot slot in slots
            .OrderByDescending(slot => slot.Priority)
            .ThenBy(slot => slot.TieOrder))
        {
            if (remainingContentWidth - slot.Width >= effectiveMinimumContentWidth)
            {
                remainingContentWidth -= slot.Width;
                continue;
            }

            foreach (DockPanelId id in slot.PanelIds)
                overlayIds.Add(id);
        }

        return overlayIds;
    }

    public void Refresh() => NotifyChanged(DockLayoutChangeReason.Refresh);

    public void Open(DockPanelId id, DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction)
    {
        ArgumentNullException.ThrowIfNull(id);
        DockPanelEntry activeEntry = RequireEntry(id);
        bool changed = false;

        if (reason is DockLayoutChangeReason.UserAction)
        {
            foreach (DockPanelEntry entry in _entries.Values)
            {
                if (IsActivationGroupEntry(entry, activeEntry))
                    _userActivatedInOverlay.Remove(entry.Descriptor.Id);
            }

            if (ShouldRenderDockedPanelAsOverlay(activeEntry with
            {
                State = activeEntry.State with { IsOpen = true, IsActive = true }
            }))
            {
                _userActivatedInOverlay.Add(id);
            }
        }

        foreach (DockPanelEntry entry in _entries.Values.ToArray())
        {
            if (!IsActivationGroupEntry(entry, activeEntry))
                continue;

            bool shouldItemBeOpen = entry.Descriptor.Id == id || entry.State.IsOpen;
            bool shouldItemBeActive = entry.Descriptor.Id == id;
            DockPanelState nextState = entry.State with
            {
                IsOpen = shouldItemBeOpen,
                IsActive = shouldItemBeActive
            };

            if (entry.State != nextState)
            {
                SetEntryState(entry.Descriptor.Id, nextState);
                changed = true;
            }
        }

        SanitizeOverlayActiveStates();

        if (changed)
            NotifyChanged(reason);
    }

    public void UpdateViewport(int viewportWidth, bool isMobile) =>
        UpdateViewportCore(viewportWidth, isMobile, projectionChanged: false);

    public void UpdateViewport(int viewportWidth, bool isMobile, DockScope scope, int minimumContentWidth)
    {
        int normalizedMinimum = Math.Max(0, minimumContentWidth);
        bool projectionChanged = !_minimumContentWidths.TryGetValue(scope, out int currentMinimum)
            || currentMinimum != normalizedMinimum;
        _minimumContentWidths[scope] = normalizedMinimum;
        UpdateViewportCore(viewportWidth, isMobile, projectionChanged);
    }

    /// <summary>Updates only the minimum content width floor for <paramref name="scope"/> without altering viewport width.</summary>
    public void SetMinimumContentWidth(DockScope scope, int minimumContentWidth)
    {
        int normalizedMinimum = Math.Max(0, minimumContentWidth);
        if (_minimumContentWidths.TryGetValue(scope, out int currentMinimum) && currentMinimum == normalizedMinimum)
            return;

        _minimumContentWidths[scope] = normalizedMinimum;
        SanitizeOverlayActiveStates();
        NotifyChanged(DockLayoutChangeReason.ViewportPolicy);
    }

    private void UpdateViewportCore(int viewportWidth, bool isMobile, bool projectionChanged)
    {
        int normalizedWidth = Math.Max(0, viewportWidth);
        bool viewportChanged = projectionChanged
            || _viewportWidth != normalizedWidth
            || _isMobileViewport != isMobile;

        _viewportWidth = normalizedWidth;
        _isMobileViewport = isMobile;

        SanitizeOverlayActiveStates();

        if (viewportChanged)
            NotifyChanged(DockLayoutChangeReason.ViewportPolicy);
    }

    private void SanitizeOverlayActiveStates()
    {
        HashSet<DockPanelId> overlayIds = ComputeOverlayPanelIds();
        foreach (DockPanelEntry entry in _entries.Values.ToArray())
        {
            if (entry.State.Mode != DockMode.Docked)
                continue;

            bool isOverlay = entry.State.IsOpen && overlayIds.Contains(entry.Descriptor.Id);
            if (!isOverlay)
                _userActivatedInOverlay.Remove(entry.Descriptor.Id);

            bool expectedActive = isOverlay
                ? entry.State.IsOpen && _userActivatedInOverlay.Contains(entry.Descriptor.Id)
                : entry.State.IsActive;

            if (entry.State.IsActive != expectedActive)
                SetEntryState(entry.Descriptor.Id, entry.State with { IsActive = expectedActive });
        }
    }

    public void Close(DockPanelId id, DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction)
    {
        ArgumentNullException.ThrowIfNull(id);
        DockPanelEntry entry = RequireEntry(id);

        if (!entry.Descriptor.CanClose)
            throw new InvalidOperationException($"Dock panel '{id}' cannot be closed.");

        _userActivatedInOverlay.Remove(id);
        UpdateState(id, state => state with { IsOpen = false, IsActive = false }, reason);
        SanitizeOverlayActiveStates();
    }

    public void Toggle(DockPanelId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        DockPanelEntry entry = RequireEntry(id);

        if (ShouldRenderDockedPanelAsOverlay(entry))
        {
            if (entry.State.IsOpen && entry.State.IsActive)
                Close(id);
            else
                Open(id, DockLayoutChangeReason.UserAction);
            return;
        }

        if (entry.State.IsOpen)
        {
            Close(id);
            return;
        }

        Open(id);
    }

    public void SetMode(DockPanelId id, DockMode mode, DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction) =>
        UpdateState(id, state => state with { Mode = mode }, reason);

    public void Resize(DockPanelId id, int width)
    {
        ArgumentNullException.ThrowIfNull(id);
        DockPanelEntry entry = RequireEntry(id);

        if (!entry.Descriptor.IsResizable)
            throw new InvalidOperationException($"Dock panel '{id}' cannot be resized.");

        int clampedWidth = Math.Clamp(width, entry.Descriptor.MinWidth, entry.Descriptor.MaxWidth);
        UpdateState(id, state => state with { Width = clampedWidth });
    }

    public void Activate(DockPanelId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        DockPanelEntry activeEntry = RequireEntry(id);
        bool changed = false;

        foreach (DockPanelEntry entry in _entries.Values.ToArray())
        {
            if (!IsActivationGroupEntry(entry, activeEntry))
                continue;

            bool shouldBeActive = entry.Descriptor.Id == id;
            DockPanelState nextState = entry.State with { IsActive = shouldBeActive };

            if (entry.State == nextState)
                continue;

            SetEntryState(entry.Descriptor.Id, nextState);
            changed = true;
        }

        if (changed)
            NotifyChanged(DockLayoutChangeReason.UserAction);
    }

    public DockLayoutSnapshot CreateSnapshot(string layoutKey, DockScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutKey);

        DockPanelState[] panels = _entries.Values
            .Where(entry => entry.Descriptor.Scope == scope && entry.Descriptor.PersistState)
            .OrderBy(entry => entry.Descriptor.Scope)
            .ThenBy(entry => entry.Descriptor.Side)
            .ThenBy(entry => entry.State.Order)
            .Select(entry => entry.State)
            .ToArray();

        return new DockLayoutSnapshot(layoutKey, panels, DateTimeOffset.UtcNow);
    }

    public void ResetToDefaults()
    {
        bool changed = false;

        foreach (DockPanelEntry entry in _entries.Values.ToArray())
        {
            DockPanelState defaultState = CreateDefaultState(entry.Descriptor);
            if (entry.State == defaultState)
                continue;

            SetEntryState(entry.Descriptor.Id, defaultState);
            changed = true;
        }

        if (changed)
            NotifyChanged(DockLayoutChangeReason.Reset);
    }

    public void RestoreSnapshot(DockLayoutSnapshot snapshot, string layoutKey, DockScope scope)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutKey);

        if (!string.Equals(snapshot.LayoutKey, layoutKey, StringComparison.Ordinal))
            return;

        bool restoredAny = false;
        Dictionary<DockPanelId, DockPanelState> nextStates = _entries.ToDictionary(entry => entry.Key, entry => entry.Value.State);

        foreach (DockPanelState panelState in snapshot.Panels)
        {
            if (!_entries.TryGetValue(panelState.Id, out DockPanelEntry? entry)
                || entry.Descriptor.Scope != scope
                || !entry.Descriptor.PersistState)
            {
                continue;
            }

            DockPanelState currentState = nextStates[panelState.Id];
            int width = entry.Descriptor.IsResizable
                ? Math.Clamp(panelState.Width, entry.Descriptor.MinWidth, entry.Descriptor.MaxWidth)
                : currentState.Width;
            bool isOpen = panelState.IsOpen || (!entry.Descriptor.CanClose && currentState.IsOpen);

            nextStates[panelState.Id] = panelState with
            {
                IsOpen = isOpen,
                Width = width,
                IsActive = isOpen && panelState.IsActive
            };
            restoredAny = true;
        }

        if (!restoredAny)
            return;

        NormalizeActiveStates(nextStates);

        bool changed = false;

        foreach (DockPanelEntry entry in _entries.Values.ToArray())
        {
            DockPanelState nextState = nextStates[entry.Descriptor.Id];
            if (entry.State == nextState)
                continue;

            SetEntryState(entry.Descriptor.Id, nextState);
            changed = true;
        }

        if (changed)
            NotifyChanged(DockLayoutChangeReason.SnapshotRestore);
    }

    private void NormalizeActiveStates(Dictionary<DockPanelId, DockPanelState> nextStates)
    {
        foreach (DockPanelId id in nextStates.Keys.ToArray())
        {
            DockPanelState state = nextStates[id];
            if (state is { IsOpen: false, IsActive: true })
                nextStates[id] = state with { IsActive = false };
        }

        foreach (IGrouping<ActivationGroupKey, DockPanelEntry> group in _entries.Values.GroupBy(GetActivationGroupKey))
        {
            DockPanelEntry[] openEntries = group
                .Where(entry => nextStates[entry.Descriptor.Id].IsOpen)
                .OrderBy(entry => nextStates[entry.Descriptor.Id].Order)
                .ThenBy(entry => entry.Descriptor.Title, StringComparer.Ordinal)
                .ToArray();

            DockPanelEntry[] activeEntries = openEntries
                .Where(entry => nextStates[entry.Descriptor.Id].IsActive)
                .ToArray();

            if (activeEntries.Length == 0 && openEntries.Length > 0)
            {
                DockPanelEntry firstOpenEntry = openEntries[0];
                DockPanelState state = nextStates[firstOpenEntry.Descriptor.Id];
                nextStates[firstOpenEntry.Descriptor.Id] = state with { IsActive = true };
                continue;
            }

            foreach (DockPanelEntry entry in activeEntries.Skip(1))
            {
                DockPanelState state = nextStates[entry.Descriptor.Id];
                nextStates[entry.Descriptor.Id] = state with { IsActive = false };
            }
        }
    }

    private bool IsActivationGroupEntry(DockPanelEntry entry, DockPanelEntry activeEntry) =>
        GetActivationGroupKey(entry) == GetActivationGroupKey(activeEntry);

    private ActivationGroupKey GetActivationGroupKey(DockPanelEntry entry) =>
        _isMobileViewport
            ? new ActivationGroupKey(entry.Descriptor.Scope, Side: null)
            : new ActivationGroupKey(entry.Descriptor.Scope, entry.Descriptor.Side);

    private DockPanelEntry RequireEntry(DockPanelId id) =>
        _entries.TryGetValue(id, out DockPanelEntry? entry)
            ? entry
            : throw new KeyNotFoundException($"Dock panel '{id}' is not registered.");

    private void UpdateState(
        DockPanelId id,
        Func<DockPanelState, DockPanelState> update,
        DockLayoutChangeReason reason = DockLayoutChangeReason.UserAction)
    {
        DockPanelEntry entry = RequireEntry(id);
        DockPanelState nextState = update(entry.State);

        if (entry.State == nextState)
            return;

        SetEntryState(id, nextState);
        NotifyChanged(reason);
    }

    private void SetEntryState(DockPanelId id, DockPanelState state)
    {
        DockPanelEntry entry = RequireEntry(id);
        _entries[id] = entry with { State = state };
    }

    private static DockPanelState CreateDefaultState(DockPanelDescriptor descriptor) =>
        new(
            descriptor.Id,
            IsOpen: false,
            descriptor.DefaultMode,
            descriptor.DefaultWidth,
            descriptor.Order,
            IsActive: false);

    private void NotifyChanged(DockLayoutChangeReason reason)
    {
        LastChangeReason = reason;
        Changed?.Invoke();
    }
}
