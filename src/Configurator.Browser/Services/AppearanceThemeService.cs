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
            IJSObjectReference module = await javascript.InvokeAsync<IJSObjectReference>("import", "./js/browser-appearance.js");
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
                LinesDefault = contrast ? "#18181B" : "#E4E4E7",
                LinesInputs = contrast ? "#18181B" : "#D4D4D8",
                Divider = contrast ? "#18181B" : "#E4E4E7",
                Success = "#047857",
                Warning = "#B45309",
                Error = "#B91C1C",
                Info = "#52525B"
            },
            PaletteDark = new PaletteDark
            {
                Primary = contrast ? "#FAFAFA" : "#2DD4BF",
                PrimaryContrastText = "#0D1117",
                Secondary = "#A1A1AA",
                SecondaryContrastText = "#0D1117",
                Background = contrast ? "#000000" : "#141415",
                Surface = contrast ? "#000000" : "#19191B",
                BackgroundGray = "#1F1F23",
                AppbarBackground = contrast ? "#000000" : "#141415",
                AppbarText = "#EDEDED",
                DrawerBackground = contrast ? "#000000" : "#141415",
                DrawerText = "#EDEDED",
                DrawerIcon = "#85858B",
                TextPrimary = "#EDEDED",
                TextSecondary = contrast ? "#FAFAFA" : "#A1A1A6",
                LinesDefault = contrast ? "#FAFAFA" : "#2E2E33",
                LinesInputs = contrast ? "#FAFAFA" : "#3E3E44",
                Divider = contrast ? "#FAFAFA" : "#27272A",
                Success = "#34D399",
                Warning = "#FBBF24",
                Error = "#F87171",
                Info = "#85858B"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = ["system-ui", "-apple-system", "sans-serif"], FontSize = ".9375rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0" },
                H1 = new H1Typography { FontSize = "clamp(1.75rem, 1.35rem + 1vw, 2.25rem)", FontWeight = "600", LineHeight = "1.2", LetterSpacing = "-.015em" },
                H2 = new H2Typography { FontSize = "clamp(1.5rem, 1.25rem + .5vw, 1.875rem)", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-.01em" },
                H3 = new H3Typography { FontSize = "clamp(1.25rem, 1.15rem + .3vw, 1.5rem)", FontWeight = "600", LineHeight = "1.3" },
                H4 = new H4Typography { FontSize = "clamp(1.125rem, 1.05rem + .2vw, 1.25rem)", FontWeight = "600", LineHeight = "1.4" },
                H5 = new H5Typography { FontSize = "1.0625rem", FontWeight = "600", LineHeight = "1.5" },
                H6 = new H6Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.6" },
                Body1 = new Body1Typography { FontSize = ".9375rem", LineHeight = "1.6" },
                Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.5" },
                Button = new ButtonTypography { FontSize = ".875rem", FontWeight = "500", TextTransform = "none", LetterSpacing = "0" }
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
