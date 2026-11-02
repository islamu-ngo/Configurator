namespace Configurator.Desktop.Resources;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

/// <summary>Follows native contrast changes while ordinary light/dark selection remains platform-owned.</summary>
public sealed class DesktopTheme : IDisposable
{
    public static ThemeVariant HighContrast { get; } = new("ConfiguratorHighContrast", ThemeVariant.Dark);
    private readonly Application _application;
    private readonly Action _unsubscribe;
    private bool _disposed;

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
    }

    private void Changed(PlatformColorValues values)
    {
        if (Dispatcher.UIThread.CheckAccess()) Apply(values);
        else Dispatcher.UIThread.Post(() => Apply(values));
    }

    private void Apply(PlatformColorValues values)
    {
        if (!_disposed)
            _application.RequestedThemeVariant = values.ContrastPreference == ColorContrastPreference.High
                ? HighContrast : ThemeVariant.Default;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe();
    }
}
