namespace Configurator.Desktop.Resources;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Configurator.Presentation;

/// <summary>Applies session appearance with native high contrast taking precedence.</summary>
public sealed class DesktopTheme : IDisposable
{
    public static ThemeVariant HighContrast { get; } = new("ConfiguratorHighContrast", ThemeVariant.Dark);
    private readonly Application _application;
    private readonly Action _unsubscribe;
    private bool _disposed;
    private bool _highContrast;
    private DesktopAppearance _preference;
    private WorkspaceDensity _density = WorkspaceDensity.Comfortable;

    public bool IsHighContrast => _highContrast;
    public event EventHandler? HighContrastChanged;

    public DesktopAppearance Preference
    {
        get => _preference;
        set
        {
            Dispatcher.UIThread.VerifyAccess();
            if (_disposed || _preference == value) return;
            _preference = value;
            ApplyVariant();
        }
    }

    public WorkspaceDensity Density
    {
        get => _density;
        set
        {
            Dispatcher.UIThread.VerifyAccess();
            if (_disposed || _density == value) return;
            _density = value;
            DesktopDensity.Apply(_application, value);
        }
    }

    public DesktopTheme(Application application, IPlatformSettings settings)
        : this(application, changed =>
        {
            EventHandler<PlatformColorValues> handler = (_, values) => changed(values);
            settings.ColorValuesChanged += handler;
            try { changed(settings.GetColorValues()); }
            catch
            {
                settings.ColorValuesChanged -= handler;
                throw;
            }
            return () => settings.ColorValuesChanged -= handler;
        })
    {
    }

    internal DesktopTheme(Application application, Func<Action<PlatformColorValues>, Action> subscribe)
    {
        _application = application;
        _unsubscribe = subscribe(Changed);
        ApplyVariant();
        DesktopDensity.Apply(_application, _density);
    }

    private void Changed(PlatformColorValues values)
    {
        if (Dispatcher.UIThread.CheckAccess()) Apply(values);
        else Dispatcher.UIThread.Post(() => Apply(values));
    }

    private void Apply(PlatformColorValues values)
    {
        if (_disposed) return;
        bool next = values.ContrastPreference == ColorContrastPreference.High;
        bool changed = _highContrast != next;
        _highContrast = next;
        ApplyVariant();
        if (changed) HighContrastChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyVariant() =>
        _application.RequestedThemeVariant = _highContrast ? HighContrast : _preference switch
        {
            DesktopAppearance.System => ThemeVariant.Default,
            DesktopAppearance.Light => ThemeVariant.Light,
            DesktopAppearance.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(Preference))
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe();
    }
}
