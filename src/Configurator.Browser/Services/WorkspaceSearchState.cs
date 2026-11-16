namespace Configurator.Browser.Services;

using Microsoft.AspNetCore.Components;

/// <summary>Connects appbar search to the active editor without copying its query or answers.</summary>
public sealed class WorkspaceSearchState
{
    private object? _owner;
    private Func<string>? _query;
    private Action<string>? _search;
    private Func<int>? _optionCount;
    private Action<int>? _activateOption;
    public long Generation { get; private set; }
    public bool IsActive => _owner is not null;
    public string Query => _query?.Invoke() ?? string.Empty;
    public RenderFragment? Content { get; private set; }
    public int ActiveIndex { get; private set; } = -1;
    public string? ActiveOptionId => IsActive && ActiveIndex >= 0
        ? $"workspace-search-option-{Generation}-{ActiveIndex}"
        : null;
    public event EventHandler? Changed;
    public event EventHandler? Navigated;
    public event EventHandler? OpenRequested;

    public void RequestOpen(object owner, long generation)
    {
        if (ReferenceEquals(owner, _owner) && generation == Generation)
            OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteNavigation(object owner, long generation)
    {
        if (ReferenceEquals(owner, _owner) && generation == Generation)
        {
            ActiveIndex = -1;
            Navigated?.Invoke(this, EventArgs.Empty);
        }
    }

    public long Register(object owner, Func<string> query, Action<string> search, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(content);
        _owner = owner;
        _query = query;
        _search = search;
        _optionCount = null;
        _activateOption = null;
        ActiveIndex = -1;
        Content = content;
        Generation++;
        Changed?.Invoke(this, EventArgs.Empty);
        return Generation;
    }

    public void RegisterListbox(long generation, Func<int> optionCount, Action<int> activateOption)
    {
        if (_owner is null || generation != Generation) return;
        _optionCount = optionCount;
        _activateOption = activateOption;
        int count = _optionCount();
        if (count == 0) ActiveIndex = -1;
        else if (ActiveIndex >= count) ActiveIndex = count - 1;
    }

    public void Release(object owner, long generation)
    {
        if (!ReferenceEquals(owner, _owner) || generation != Generation) return;
        _owner = null;
        _query = null;
        _search = null;
        _optionCount = null;
        _activateOption = null;
        ActiveIndex = -1;
        Content = null;
        Generation++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Search(long generation, string query)
    {
        if (_owner is null || generation != Generation) return;
        ActiveIndex = -1;
        _search!(query);
    }

    public void MoveActiveOption(long generation, int delta)
    {
        if (_owner is null || generation != Generation || _optionCount is null) return;
        int count = _optionCount();
        if (count <= 0)
        {
            ActiveIndex = -1;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
        int next = ActiveIndex < 0
            ? (delta >= 0 ? 0 : count - 1)
            : (ActiveIndex + delta + count) % count;
        if (next != ActiveIndex)
        {
            ActiveIndex = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetActiveOption(long generation)
    {
        if (generation != Generation || ActiveIndex == -1) return;
        ActiveIndex = -1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryActivateOption(long generation)
    {
        if (_owner is null || generation != Generation || _optionCount is null || _activateOption is null)
            return false;
        int count = _optionCount();
        if (count <= 0) return false;
        int target = ActiveIndex is >= 0 and var index && index < count ? index : 0;
        _activateOption(target);
        return true;
    }

    public void Refresh(object owner, long generation)
    {
        if (ReferenceEquals(owner, _owner) && generation == Generation)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
