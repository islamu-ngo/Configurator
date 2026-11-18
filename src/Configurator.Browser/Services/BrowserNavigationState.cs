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
    private Action<string>? _selectLocale;
    private Func<IReadOnlyList<string>>? _availableLocales;
    private string? _shellLocale;
    private long _generation;
    private long _workGeneration;
    private bool _pendingFocusCustomTheme;
    private bool _pendingRestoreConfigurationFocus;
    public bool IsActive => _owner is not null;
    public bool HasWork => _hasWork?.Invoke() ?? false;
    public bool IsSettingsOpen { get; private set; }
    public string? SettingsSection { get; private set; }
    public bool SettingsFocusCustomTheme { get; private set; }
    public (string Locale, string Direction) Culture => _culture?.Invoke()
        ?? ResolveShellCulture();
    public IReadOnlyList<string> AvailableLocales => _availableLocales?.Invoke() ?? ["en", "ar"];
    public event EventHandler? Changed;
    public event EventHandler? WorkChanged;

    /// <summary>Returns the exact registration lease, including when the same editor reacquires custody.</summary>
    public long Register(object owner, Action invalidate, Func<Task> discard, Func<(string Locale, string Direction)> culture,
        Func<bool> hasWork, Action<string>? selectLocale = null, Func<IReadOnlyList<string>>? availableLocales = null)
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
        _selectLocale = selectLocale;
        _availableLocales = availableLocales;
        Changed?.Invoke(this, EventArgs.Empty);
        return lease;
    }

    public void SelectLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return;
        string normalized = locale.Trim();
        if (_owner is not null && _selectLocale is not null)
        {
            _selectLocale(normalized);
            return;
        }

        _shellLocale = normalized;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private (string Locale, string Direction) ResolveShellCulture()
    {
        if (!string.IsNullOrWhiteSpace(_shellLocale))
        {
            bool rtl = string.Equals(_shellLocale, "ar", StringComparison.OrdinalIgnoreCase)
                || _shellLocale.StartsWith("ar-", StringComparison.OrdinalIgnoreCase);
            return (_shellLocale, rtl ? "rtl" : "ltr");
        }

        return (CultureInfo.CurrentUICulture.Name, CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr");
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
        _selectLocale = null;
        _availableLocales = null;
        IsSettingsOpen = false;
        SettingsSection = null;
        SettingsFocusCustomTheme = false;
        _pendingRestoreConfigurationFocus = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void OpenSettings(string? section = null, bool focusCustomTheme = false)
    {
        IsSettingsOpen = true;
        SettingsSection = string.IsNullOrWhiteSpace(section) ? null : section.Trim();
        SettingsFocusCustomTheme = focusCustomTheme;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectSettingsSection(string? section)
    {
        SettingsSection = string.IsNullOrWhiteSpace(section) ? null : section.Trim();
        SettingsFocusCustomTheme = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ReturnToConfiguration()
    {
        if (!IsSettingsOpen) return;
        IsSettingsOpen = false;
        SettingsSection = null;
        SettingsFocusCustomTheme = false;
        _pendingRestoreConfigurationFocus = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPendingFocusCustomTheme(bool focusCustomTheme) => _pendingFocusCustomTheme = focusCustomTheme;

    public bool ConsumePendingFocusCustomTheme()
    {
        bool pending = _pendingFocusCustomTheme;
        _pendingFocusCustomTheme = false;
        return pending;
    }

    public bool ConsumeRestoreConfigurationFocus()
    {
        bool pending = _pendingRestoreConfigurationFocus;
        _pendingRestoreConfigurationFocus = false;
        return pending;
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
