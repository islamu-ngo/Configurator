namespace Configurator.Browser.Services;

using System.Globalization;

/// <summary>Resolves private pending work before the workspace's separate loss confirmation.</summary>
public interface IBrowserPendingChanges
{
    Task<bool> ResolvePendingAsync();
}

/// <summary>Owns the current editor's discard boundary without retaining answers.</summary>
public sealed class BrowserNavigationState
{
    private object? _owner;
    private Action? _invalidate;
    private Func<Task>? _discard;
    private Func<(string Locale, string Direction)>? _culture;
    private Func<bool>? _hasWork;
    private long _generation;
    private long _workGeneration;
    public bool IsActive => _owner is not null;
    public bool HasWork => _hasWork?.Invoke() ?? false;
    public (string Locale, string Direction) Culture => _culture?.Invoke()
        ?? (CultureInfo.CurrentUICulture.Name, CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr");
    public event EventHandler? Changed;
    public event EventHandler? WorkChanged;

    /// <summary>Returns the exact registration lease, including when the same editor reacquires custody.</summary>
    public long Register(object owner, Action invalidate, Func<Task> discard, Func<(string Locale, string Direction)> culture,
        Func<bool> hasWork)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(invalidate);
        ArgumentNullException.ThrowIfNull(discard);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(hasWork);
        long lease = ++_generation;
        ++_workGeneration;
        _owner = owner;
        _invalidate = invalidate;
        _discard = discard;
        _culture = culture;
        _hasWork = hasWork;
        Changed?.Invoke(this, EventArgs.Empty);
        return lease;
    }

    public void Release(object owner, long lease)
    {
        if (!ReferenceEquals(owner, _owner) || lease != _generation) return;
        _generation++;
        ++_workGeneration;
        _owner = null;
        _invalidate = null;
        _discard = null;
        _culture = null;
        _hasWork = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Notifies chrome only for an actual locale change in the current workspace lease.</summary>
    public void RefreshCulture(object owner, long lease)
    {
        if (!ReferenceEquals(owner, _owner) || lease != _generation) return;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Refreshes only the loss guard; editing does not rerender workspace chrome or recreate its body.</summary>
    public void RefreshWork(object owner, long lease)
    {
        if (!ReferenceEquals(owner, _owner) || lease != _generation) return;
        ++_workGeneration;
        WorkChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> TryLeaveAsync(Func<Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        if (_owner is null) return true;
        object owner = _owner;
        long generation = _generation;
        if (owner is IBrowserPendingChanges pending && !await pending.ResolvePendingAsync()) return false;
        if (!ReferenceEquals(owner, _owner) || generation != _generation) return false;
        long workGeneration = _workGeneration;
        if ((HasWork && !await confirm()) || generation != _generation || workGeneration != _workGeneration) return false;
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
