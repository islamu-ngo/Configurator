namespace Configurator.Browser.Services;

using Microsoft.AspNetCore.Components;

/// <summary>Connects appbar search to the active editor without copying its query or answers.</summary>
public sealed class WorkspaceSearchState
{
    private object? _owner;
    private Func<string>? _query;
    private Action<string>? _search;
    public long Generation { get; private set; }
    public bool IsActive => _owner is not null;
    public string Query => _query?.Invoke() ?? string.Empty;
    public RenderFragment? Content { get; private set; }
    public event EventHandler? Changed;
    public event EventHandler? Navigated;

    public void CompleteNavigation(object owner, long generation)
    {
        if (ReferenceEquals(owner, _owner) && generation == Generation)
            Navigated?.Invoke(this, EventArgs.Empty);
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
        Content = content;
        Generation++;
        Changed?.Invoke(this, EventArgs.Empty);
        return Generation;
    }

    public void Release(object owner, long generation)
    {
        if (!ReferenceEquals(owner, _owner) || generation != Generation) return;
        _owner = null;
        _query = null;
        _search = null;
        Content = null;
        Generation++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Search(long generation, string query)
    {
        if (_owner is null || generation != Generation) return;
        _search!(query);
    }

    public void Refresh(object owner, long generation)
    {
        if (ReferenceEquals(owner, _owner) && generation == Generation)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
