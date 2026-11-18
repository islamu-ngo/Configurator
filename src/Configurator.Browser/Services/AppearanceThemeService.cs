namespace Configurator.Browser.Services;

using Configurator.Presentation.Theming;
using Microsoft.JSInterop;
using MudBlazor;

/// <summary>Local presentation preferences and shared ISLAMU Event theme resolution, isolated from workspace answers.</summary>
public sealed class AppearanceThemeService(IJSRuntime javascript) : IAsyncDisposable
{
    private static readonly Typography SharedTypography = new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "sans-serif"],
            FontSize = ".9375rem",
            FontWeight = "400",
            LineHeight = "1.5",
            LetterSpacing = "-.011em"
        },
        H1 = new H1Typography { FontSize = "clamp(1.875rem, 1.5rem + 1.04vw, 2.5rem)", FontWeight = "700", LineHeight = "1.2", LetterSpacing = "-.022em" },
        H2 = new H2Typography { FontSize = "clamp(1.625rem, 1.375rem + 0.625vw, 2rem)", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-.017em" },
        H3 = new H3Typography { FontSize = "clamp(1.5rem, 1.333rem + 0.42vw, 1.75rem)", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-.014em" },
        H4 = new H4Typography { FontSize = "clamp(1.25rem, 1.083rem + 0.42vw, 1.5rem)", FontWeight = "600", LineHeight = "1.4" },
        H5 = new H5Typography { FontSize = "clamp(1.125rem, 1.042rem + 0.21vw, 1.25rem)", FontWeight = "600", LineHeight = "1.5" },
        H6 = new H6Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.6" },
        Body1 = new Body1Typography { FontSize = ".9375rem", LineHeight = "1.6", LetterSpacing = "-.011em" },
        Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.5" },
        Button = new ButtonTypography { FontSize = ".875rem", FontWeight = "500", TextTransform = "none", LetterSpacing = "-.011em" },
        Caption = new CaptionTypography { FontSize = ".8125rem", LineHeight = "1.5" },
        Overline = new OverlineTypography { FontSize = ".75rem", FontWeight = "500", TextTransform = "uppercase", LetterSpacing = ".08em" }
    };

    private IJSObjectReference? _module;
    private DotNetObjectReference<AppearanceThemeService>? _reference;
    private bool _initialized;
    private bool _systemDark;
    private bool _systemHighContrast;
    private bool _automaticRtl;
    private bool _disposed;
    private long _generation;

    public AppearanceState Current { get; private set; } = new();

    public bool IsDark => Current.ThemeMode is "dark" or "darkhighcontrast"
        || (Current.ThemeMode == "system" && (Current.ThemeKey == "dark-hc" || (Current.ThemeKey != "light-hc" && _systemDark)));

    public bool IsHighContrast => Current.ThemeMode is "lighthighcontrast" or "darkhighcontrast"
        || (Current.ThemeMode == "system" && _systemHighContrast)
        || Current.ThemeKey is "light-hc" or "dark-hc";

    public bool IsRtl => Current.Direction == "rtl" || (Current.Direction == "auto" && _automaticRtl);

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
            await ApplyToDomAsync(Current);
        }
        catch (JSException)
        {
            // Storage and platform restrictions must not prevent local configuration work.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ValueTask SetDensityAsync(string density) =>
        AppearanceState.IsDensity(density)
            ? SetAsync(Current.ThemeMode, Current.Direction, density, Current.NamingMode)
            : ValueTask.CompletedTask;

    public ValueTask SetNamingModeAsync(string namingMode) =>
        AppearanceState.IsNamingMode(namingMode)
            ? SetAsync(Current.ThemeMode, Current.Direction, Current.Density, namingMode)
            : ValueTask.CompletedTask;

    public ValueTask SetModeAsync(string mode) =>
        AppearanceState.IsThemeMode(mode)
            ? SetAsync(mode, Current.Direction, Current.Density, Current.NamingMode)
            : ValueTask.CompletedTask;

    public ValueTask SetDirectionAsync(string direction) =>
        AppearanceState.IsDirection(direction)
            ? SetAsync(Current.ThemeMode, direction, Current.Density, Current.NamingMode)
            : ValueTask.CompletedTask;

    public ValueTask SetThemePresetAsync(string themeKey) =>
        ThemePresetCatalog.IsValidPresetKey(themeKey)
            ? SetAsync(Current.ThemeMode, Current.Direction, Current.Density, Current.NamingMode, themeKey)
            : ValueTask.CompletedTask;

    public ValueTask SetCustomThemeAsync(string naturalHex, string brandHex, string? baseMode = null)
    {
        if (!HslColor.TryNormalizeHex(naturalHex, out string normalizedNatural)
            || !HslColor.TryNormalizeHex(brandHex, out string normalizedBrand))
        {
            return ValueTask.CompletedTask;
        }

        string targetMode = baseMode is not null && AppearanceState.IsThemeMode(baseMode) ? baseMode : Current.ThemeMode;
        return SetAsync(
            targetMode,
            Current.Direction,
            Current.Density,
            Current.NamingMode,
            ThemePresetCatalog.CustomThemeKey,
            normalizedNatural,
            normalizedBrand);
    }

    public async ValueTask SetAsync(
        string mode,
        string direction,
        string? density = null,
        string? namingMode = null,
        string? themeKey = null,
        string? customNaturalHex = null,
        string? customBrandHex = null)
    {
        string targetDensity = density ?? Current.Density;
        string targetNaming = namingMode ?? Current.NamingMode;
        string targetThemeKey = themeKey ?? Current.ThemeKey;
        string targetNatural = customNaturalHex ?? Current.CustomNaturalHex;
        string targetBrand = customBrandHex ?? Current.CustomBrandHex;

        if (_disposed
            || !AppearanceState.IsThemeMode(mode)
            || !AppearanceState.IsDirection(direction)
            || !AppearanceState.IsDensity(targetDensity)
            || !AppearanceState.IsNamingMode(targetNaming)
            || !AppearanceState.IsThemeKey(targetThemeKey)
            || !HslColor.TryNormalizeHex(targetNatural, out string normalizedNatural)
            || !HslColor.TryNormalizeHex(targetBrand, out string normalizedBrand))
        {
            return;
        }

        long generation = ++_generation;
        Current = new AppearanceState(
            mode,
            direction,
            targetDensity,
            targetNaming,
            targetThemeKey,
            normalizedNatural,
            normalizedBrand);
        Changed?.Invoke(this, EventArgs.Empty);

        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync(
                "write",
                Current.ThemeMode,
                Current.Direction,
                Current.Density,
                Current.NamingMode,
                Current.ThemeKey,
                Current.CustomNaturalHex,
                Current.CustomBrandHex);
            if (_disposed || generation != _generation) return;
            await ApplyToDomAsync(Current);
        }
        catch (JSException)
        {
            // Retain the selected presentation for this tab when persistence is unavailable.
        }
    }

    [JSInvokable]
    public void SystemPreferencesChanged(bool dark, bool rtl) =>
        SystemPreferencesWithContrastChanged(dark, rtl, _systemHighContrast);

    [JSInvokable]
    public void SystemPreferencesWithContrastChanged(bool dark, bool rtl, bool highContrast)
    {
        if (_disposed) return;
        _systemDark = dark;
        _automaticRtl = rtl;
        _systemHighContrast = highContrast;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public MudTheme CreateTheme()
    {
        ThemePreset preset = Current.ThemeKey == ThemePresetCatalog.CustomThemeKey
            && HslColor.TryParseHex(Current.CustomNaturalHex, out _)
            && HslColor.TryParseHex(Current.CustomBrandHex, out _)
            ? ThemePaletteGenerator.CreateCustomPreset(Current.CustomNaturalHex, Current.CustomBrandHex)
            : ThemePresetCatalog.GetPresetOrDefault(Current.ThemeKey);

        bool highContrastLight = Current.ThemeMode == "lighthighcontrast"
            || (Current.ThemeMode == "system" && (_systemHighContrast || Current.ThemeKey == "light-hc"));
        bool highContrastDark = Current.ThemeMode == "darkhighcontrast"
            || (Current.ThemeMode == "system" && (_systemHighContrast || Current.ThemeKey == "dark-hc"));

        ThemePalette light = highContrastLight ? preset.LightHighContrastPalette : preset.LightPalette;
        ThemePalette dark = highContrastDark ? preset.DarkHighContrastPalette : preset.DarkPalette;

        return new MudTheme
        {
            PaletteLight = ComposeLight(light),
            PaletteDark = ComposeDark(dark),
            Typography = SharedTypography,
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px",
                AppbarHeight = "64px"
            }
        };
    }

    private ValueTask ApplyToDomAsync(AppearanceState state) =>
        _module is null
            ? ValueTask.CompletedTask
            : _module.InvokeVoidAsync(
                "apply",
                state.ThemeMode,
                state.Direction,
                state.Density,
                state.NamingMode,
                state.ThemeKey);

    private static PaletteLight ComposeLight(ThemePalette palette) => new()
    {
        Primary = palette.Primary,
        PrimaryContrastText = palette.PrimaryContrastText,
        Secondary = palette.Secondary,
        SecondaryContrastText = palette.SecondaryContrastText,
        Black = "#09090B",
        Background = palette.Background,
        Surface = palette.Surface,
        GrayLight = "#D4D4D8",
        GrayLighter = "#FAFAFA",
        AppbarBackground = palette.AppbarBackground,
        AppbarText = palette.AppbarText,
        DrawerBackground = palette.DrawerBackground,
        DrawerText = palette.DrawerText,
        DrawerIcon = palette.DrawerIcon,
        ActionDefault = palette.TextSecondary,
        ActionDisabled = palette.TextSecondary,
        ActionDisabledBackground = "#E4E4E7",
        TextPrimary = palette.TextPrimary,
        TextSecondary = palette.TextSecondary,
        TextDisabled = palette.TextSecondary,
        Info = palette.Info,
        Success = palette.Success,
        Warning = palette.Warning,
        Error = palette.Error,
        LinesDefault = palette.LinesDefault,
        LinesInputs = palette.LinesDefault,
        TableLines = palette.Divider,
        Divider = palette.Divider,
        OverlayLight = "rgba(250,250,250,0.8)"
    };

    private static PaletteDark ComposeDark(ThemePalette palette) => new()
    {
        Primary = palette.Primary,
        PrimaryContrastText = palette.PrimaryContrastText,
        Secondary = palette.Secondary,
        SecondaryContrastText = palette.SecondaryContrastText,
        Background = palette.Background,
        Surface = palette.Surface,
        BackgroundGray = palette.Surface,
        GrayLight = "#3F3F46",
        GrayLighter = "#27272A",
        AppbarBackground = palette.AppbarBackground,
        AppbarText = palette.AppbarText,
        DrawerBackground = palette.DrawerBackground,
        DrawerText = palette.DrawerText,
        DrawerIcon = palette.DrawerIcon,
        ActionDefault = palette.TextSecondary,
        ActionDisabled = "#52525B",
        ActionDisabledBackground = "#27272A",
        TextPrimary = palette.TextPrimary,
        TextSecondary = palette.TextSecondary,
        TextDisabled = "#71717A",
        Info = palette.Info,
        Success = palette.Success,
        Warning = palette.Warning,
        Error = palette.Error,
        LinesDefault = palette.LinesDefault,
        LinesInputs = palette.LinesDefault,
        TableLines = palette.LinesDefault,
        Divider = palette.Divider,
        OverlayDark = "rgba(0,0,0,0.7)"
    };

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
            catch (JSDisconnectedException)
            {
            }
        }

        _reference?.Dispose();
        Changed = null;
    }
}
