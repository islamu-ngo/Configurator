namespace Configurator.Browser.Services;

/// <summary>Owns the current editor's discard boundary without retaining answers.</summary>
public sealed class BrowserNavigationState
{
    private object? _owner;
    private Action? _invalidate;
    private Func<Task>? _discard;
    private long _generation;
    public bool IsActive => _owner is not null;
    public event EventHandler? Changed;

    /// <summary>Returns the exact registration lease, including when the same editor reacquires custody.</summary>
    public long Register(object owner, Action invalidate, Func<Task> discard)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(invalidate);
        ArgumentNullException.ThrowIfNull(discard);
        long lease = ++_generation;
        _owner = owner;
        _invalidate = invalidate;
        _discard = discard;
        Changed?.Invoke(this, EventArgs.Empty);
        return lease;
    }

    public void Release(object owner, long lease)
    {
        if (!ReferenceEquals(owner, _owner) || lease != _generation) return;
        _generation++;
        _owner = null;
        _invalidate = null;
        _discard = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> TryLeaveAsync(Func<Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        if (_owner is null) return true;
        object owner = _owner;
        long generation = _generation;
        if (!await confirm() || generation != _generation) return false;
        Action invalidate = _invalidate!;
        Func<Task> discard = _discard!;
        invalidate();
        if (generation != _generation) return false;
        await discard();
        // Cleanup may have yielded while the same editor registered a new workspace.
        if (_owner is not null && (!ReferenceEquals(owner, _owner) || generation != _generation)) return false;
        Release(owner, generation);
        return true;
    }
}
