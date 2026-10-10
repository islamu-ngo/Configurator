namespace Configurator.Browser.Services;

using Microsoft.JSInterop;
using MudBlazor;

/// <summary>Local presentation preferences are separate from configuration workspaces.</summary>
public sealed class AppearanceThemeService(IJSRuntime javascript) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<AppearanceThemeService>? _reference;
    private bool _initialized;
    private bool _systemDark;
    private bool _automaticRtl;
    private bool _disposed;
    private long _generation;

    public AppearanceState Current { get; private set; } = new();
    public bool IsDark => Current.ThemeMode is "dark" or "darkhighcontrast" || Current.ThemeMode == "system" && _systemDark;
    public bool IsRtl => Current.Direction == "rtl" || Current.Direction == "auto" && _automaticRtl;
    public event EventHandler? Changed;

    public async ValueTask InitializeAsync()
    {
        if (_initialized || _disposed) return;
        _initialized = true;
        long generation = _generation;
        try
        {
            IJSObjectReference module = await javascript.InvokeAsync<IJSObjectReference>("import", "./browser-appearance.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            AppearanceState preference = AppearanceState.Parse(await _module.InvokeAsync<string?>("read"));
            if (_disposed) return;
            if (generation == _generation) Current = preference;
            _reference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("watch", _reference);
            await _module.InvokeVoidAsync("apply", Current.ThemeMode, Current.Direction);
        }
        catch (JSException)
        {
            // Storage and platform restrictions must not prevent local configuration work.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask SetAsync(string mode, string direction)
    {
        if (_disposed || !AppearanceState.IsThemeMode(mode) || !AppearanceState.IsDirection(direction)) return;
        long generation = ++_generation;
        Current = new(mode, direction);
        Changed?.Invoke(this, EventArgs.Empty);
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("write", mode, direction);
            if (_disposed || generation != _generation) return;
            await _module.InvokeVoidAsync("apply", mode, direction);
        }
        catch (JSException)
        {
            // Retain the selected presentation for this tab when persistence is unavailable.
        }
    }

    [JSInvokable]
    public void SystemPreferencesChanged(bool dark, bool rtl)
    {
        if (_disposed) return;
        _systemDark = dark;
        _automaticRtl = rtl;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public MudTheme CreateTheme()
    {
        bool contrast = Current.ThemeMode is "lighthighcontrast" or "darkhighcontrast";
        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = contrast ? "#18181B" : "#0F766E",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#52525B",
                SecondaryContrastText = "#FFFFFF",
                Background = contrast ? "#FFFFFF" : "#F5F5F7",
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#18181B",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#18181B",
                DrawerIcon = "#52525B",
                TextPrimary = contrast ? "#000000" : "#18181B",
                TextSecondary = contrast ? "#18181B" : "#52525B",
                LinesDefault = contrast ? "#18181B" : "#A1A1AA",
                LinesInputs = contrast ? "#18181B" : "#A1A1AA",
                Divider = contrast ? "#18181B" : "#E4E4E7",
                Success = "#047857",
                Warning = "#B45309",
                Error = "#B91C1C",
                Info = "#52525B"
            },
            PaletteDark = new PaletteDark
            {
                Primary = contrast ? "#FAFAFA" : "#5EEAD4",
                PrimaryContrastText = "#1A1A1A",
                Secondary = "#A1A1AA",
                SecondaryContrastText = "#1A1A1A",
                Background = contrast ? "#000000" : "#16191D",
                Surface = contrast ? "#000000" : "#202429",
                BackgroundGray = "#242424",
                AppbarBackground = "#1A1A1A",
                AppbarText = "#FAFAFA",
                DrawerBackground = "#1A1A1A",
                DrawerText = "#FAFAFA",
                DrawerIcon = "#A1A1AA",
                TextPrimary = "#FAFAFA",
                TextSecondary = contrast ? "#FAFAFA" : "#A1A1AA",
                LinesDefault = contrast ? "#FAFAFA" : "#71717A",
                LinesInputs = contrast ? "#FAFAFA" : "#71717A",
                Divider = contrast ? "#FAFAFA" : "#363C44",
                Success = "#34D399",
                Warning = "#FBBF24",
                Error = "#F87171",
                Info = "#A1A1AA"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = ["system-ui", "-apple-system", "sans-serif"], FontSize = ".9375rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "-.011em" },
                H1 = new H1Typography { FontSize = "clamp(1.875rem, 1.5rem + 1.04vw, 2.5rem)", FontWeight = "700", LineHeight = "1.2", LetterSpacing = "-.022em" },
                H2 = new H2Typography { FontSize = "clamp(1.625rem, 1.375rem + .625vw, 2rem)", FontWeight = "600", LineHeight = "1.3" },
                H3 = new H3Typography { FontSize = "clamp(1.5rem, 1.333rem + .42vw, 1.75rem)", FontWeight = "600", LineHeight = "1.3" },
                H4 = new H4Typography { FontSize = "clamp(1.25rem, 1.083rem + .42vw, 1.5rem)", FontWeight = "600", LineHeight = "1.4" },
                H5 = new H5Typography { FontSize = "clamp(1.125rem, 1.042rem + .21vw, 1.25rem)", FontWeight = "600", LineHeight = "1.5" },
                H6 = new H6Typography { FontSize = "clamp(1rem, .958rem + .21vw, 1.125rem)", FontWeight = "600", LineHeight = "1.6" },
                Body1 = new Body1Typography { FontSize = ".9375rem", LineHeight = "1.6" },
                Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.5" },
                Button = new ButtonTypography { FontSize = ".875rem", FontWeight = "500", TextTransform = "none", LetterSpacing = "-.011em" }
            },
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "12px" }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unwatch");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _reference?.Dispose();
        Changed = null;
    }
}
