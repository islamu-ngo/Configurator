namespace Configurator.Desktop.Resources;

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Configurator.Presentation;
using Configurator.Presentation.Theming;
using HslColor = Configurator.Presentation.Theming.HslColor;

/// <summary>Applies session appearance mode, Event theme presets, custom HSL palettes, and native high-contrast precedence.</summary>
public sealed class DesktopTheme : IDisposable
{
    public static ThemeVariant HighContrast { get; } = new("ConfiguratorHighContrast", ThemeVariant.Dark);
    public static ThemeVariant LightHighContrast { get; } = new("ConfiguratorLightHighContrast", ThemeVariant.Light);

    private readonly Application _application;
    private readonly Action _unsubscribe;
    private bool _disposed;
    private bool _nativeHighContrast;
    private DesktopAppearance _preference;
    private WorkspaceDensity _density = WorkspaceDensity.Comfortable;
    private string _themeKey = ThemePresetCatalog.DefaultThemeKey;
    private string _customNaturalHex = ThemePreference.DefaultNaturalHex;
    private string _customBrandHex = ThemePreference.DefaultBrandHex;
    private ThemePreset _activePreset = ThemePresetCatalog.DefaultPreset;

    public bool IsHighContrast =>
        _nativeHighContrast || _preference is DesktopAppearance.LightHighContrast or DesktopAppearance.DarkHighContrast;

    public string ThemeKey => _themeKey;
    public string CustomNaturalHex => _customNaturalHex;
    public string CustomBrandHex => _customBrandHex;
    public ThemePreset ActivePreset => _activePreset;

    public event EventHandler? HighContrastChanged;
    public event EventHandler? ThemeChanged;

    public DesktopAppearance Preference
    {
        get => _preference;
        set
        {
            Dispatcher.UIThread.VerifyAccess();
            if (_disposed || _preference == value) return;
            bool wasHighContrast = IsHighContrast;
            _preference = value;
            ApplyVariant();
            if (wasHighContrast != IsHighContrast) HighContrastChanged?.Invoke(this, EventArgs.Empty);
            ThemeChanged?.Invoke(this, EventArgs.Empty);
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
        ApplyPresetPalettes();
        ApplyVariant();
        DesktopDensity.Apply(_application, _density);
    }

    /// <summary>Activates one of the eight Event presets or switches to the custom Natural + Brand builder.</summary>
    public void SelectPreset(string? themeKey)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;

        if (string.Equals(themeKey, ThemePresetCatalog.CustomThemeKey, StringComparison.OrdinalIgnoreCase))
        {
            _themeKey = ThemePresetCatalog.CustomThemeKey;
            _activePreset = ThemePaletteGenerator.CreateCustomPreset(_customNaturalHex, _customBrandHex);
        }
        else
        {
            ThemePreset preset = ThemePresetCatalog.GetPresetOrDefault(themeKey);
            _themeKey = preset.ThemeKey;
            _activePreset = preset;
        }

        ApplyPresetPalettes();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the session custom palette using validated 6-digit Natural and Brand hex colors.</summary>
    public bool SetCustomColors(string? naturalHex, string? brandHex)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed ||
            !HslColor.TryNormalizeHex(naturalHex, out string normalizedNatural) ||
            !HslColor.TryNormalizeHex(brandHex, out string normalizedBrand))
        {
            return false;
        }

        _customNaturalHex = normalizedNatural;
        _customBrandHex = normalizedBrand;
        _themeKey = ThemePresetCatalog.CustomThemeKey;
        _activePreset = ThemePaletteGenerator.CreateCustomPreset(_customNaturalHex, _customBrandHex);
        ApplyPresetPalettes();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Restores the default custom Natural + Brand seed colors and applies the custom palette.</summary>
    public void ResetCustomColors()
    {
        SetCustomColors(ThemePreference.DefaultNaturalHex, ThemePreference.DefaultBrandHex);
    }

    private void Changed(PlatformColorValues values)
    {
        if (Dispatcher.UIThread.CheckAccess()) ApplyNativeColorValues(values);
        else Dispatcher.UIThread.Post(() => ApplyNativeColorValues(values));
    }

    private void ApplyNativeColorValues(PlatformColorValues values)
    {
        if (_disposed) return;
        bool wasHighContrast = IsHighContrast;
        _nativeHighContrast = values.ContrastPreference == ColorContrastPreference.High;
        ApplyVariant();
        if (wasHighContrast != IsHighContrast) HighContrastChanged?.Invoke(this, EventArgs.Empty);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyVariant()
    {
        _application.RequestedThemeVariant = _preference switch
        {
            DesktopAppearance.LightHighContrast => LightHighContrast,
            DesktopAppearance.DarkHighContrast => HighContrast,
            _ when _nativeHighContrast => HighContrast,
            DesktopAppearance.Light => ThemeVariant.Light,
            DesktopAppearance.Dark => ThemeVariant.Dark,
            DesktopAppearance.System => ThemeVariant.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(Preference))
        };
    }

    private void ApplyPresetPalettes()
    {
        foreach (IResourceProvider merged in _application.Resources.MergedDictionaries)
        {
            if (merged is not ResourceDictionary resourceDict || resourceDict.ThemeDictionaries.Count == 0)
                continue;

            ApplyPaletteToVariant(resourceDict, ThemeVariant.Light, _activePreset.LightPalette, isDark: false, isHighContrast: false);
            ApplyPaletteToVariant(resourceDict, ThemeVariant.Dark, _activePreset.DarkPalette, isDark: true, isHighContrast: false);
            ApplyPaletteToVariant(resourceDict, LightHighContrast, _activePreset.LightHighContrastPalette, isDark: false, isHighContrast: true);
            ApplyPaletteToVariant(resourceDict, HighContrast, _activePreset.DarkHighContrastPalette, isDark: true, isHighContrast: true);
        }
    }

    private static void ApplyPaletteToVariant(
        ResourceDictionary root,
        ThemeVariant variant,
        ThemePalette palette,
        bool isDark,
        bool isHighContrast)
    {
        if (!root.ThemeDictionaries.TryGetValue(variant, out IThemeVariantProvider? provider) ||
            provider is not ResourceDictionary dictionary)
        {
            return;
        }

        Color fallbackBg = isDark ? Color.Parse("#111318") : Color.Parse("#FAF9FD");
        Color fallbackSurface = isDark ? Color.Parse("#1E2027") : Color.Parse("#FFFFFF");
        Color fallbackText = isDark ? Color.Parse("#FFFFFF") : Color.Parse("#000000");

        Color canvas = ParseColor(palette.Background, fallbackBg);
        Color surface = ParseColor(palette.Surface, fallbackSurface);
        Color input = isHighContrast ? surface : ShiftLightness(surface, isDark ? -3.0 : 2.0);
        Color surfaceContainer = isHighContrast
            ? (isDark ? Color.Parse("#121212") : Color.Parse("#F0F0F0"))
            : ShiftLightness(surface, isDark ? 4.0 : -3.0);
        Color surfaceContainerHigh = isHighContrast
            ? (isDark ? Color.Parse("#1C1C1C") : Color.Parse("#E4E4E4"))
            : ShiftLightness(surface, isDark ? 8.0 : -6.0);

        Color[] backgrounds = [canvas, surface, input];
        double minTextContrast = isHighContrast ? 7.0 : 4.55;
        double minBoundaryContrast = isHighContrast ? 7.0 : 3.05;

        Color accent = EnsureContrastAgainst(
            ParseColor(palette.Primary, isDark ? Color.Parse("#93C5FD") : Color.Parse("#1B5EBE")),
            backgrounds,
            isHighContrast ? 7.0 : 3.2,
            fallbackText);
        Color focus = EnsureContrastAgainst(accent, backgrounds, minBoundaryContrast, fallbackText);
        Color selected = EnsureContrastAgainst(accent, backgrounds, isHighContrast ? 4.55 : 2.8, accent);

        Color text = EnsureContrastAgainst(ParseColor(palette.TextPrimary, fallbackText), backgrounds, minTextContrast, fallbackText);
        Color secondaryText = EnsureContrastAgainst(ParseColor(palette.TextSecondary, fallbackText), backgrounds, minTextContrast, fallbackText);
        Color border = EnsureContrastAgainst(ParseColor(palette.LinesDefault, fallbackText), backgrounds, minBoundaryContrast, fallbackText);
        Color outlineVariant = isHighContrast
            ? border
            : ParseColor(palette.Divider, isDark ? Color.Parse("#44474F") : Color.Parse("#C4C6D0"));

        Color selectedTextFallback = IsDarkColor(selected) ? Colors.White : Colors.Black;
        Color selectedText = EnsureContrastAgainst(
            ParseColor(palette.PrimaryContrastText, selectedTextFallback),
            [selected],
            minTextContrast,
            selectedTextFallback);

        Color primaryContainer = isHighContrast
            ? selected
            : BlendColors(surface, selected, isDark ? 0.28 : 0.14);
        Color containerTextFallback = IsDarkColor(primaryContainer) ? Colors.White : Colors.Black;
        Color onPrimaryContainer = EnsureContrastAgainst(
            text,
            [primaryContainer],
            minTextContrast,
            containerTextFallback);

        Color error = EnsureContrastAgainst(
            ParseColor(palette.Error, isDark ? Color.Parse("#FFB4AB") : Color.Parse("#BA1A1A")),
            backgrounds,
            minTextContrast,
            fallbackText);
        Color success = EnsureContrastAgainst(
            ParseColor(palette.Success, isDark ? Color.Parse("#8AD88F") : Color.Parse("#1D682B")),
            backgrounds,
            minTextContrast,
            fallbackText);
        Color warning = EnsureContrastAgainst(
            ParseColor(palette.Warning, isDark ? Color.Parse("#F1C478") : Color.Parse("#785200")),
            backgrounds,
            minTextContrast,
            fallbackText);

        Color disabled = isHighContrast
            ? surfaceContainer
            : ShiftLightness(surface, isDark ? 2.5 : -5.0);
        Color disabledText = EnsureContrastAgainst(secondaryText, [disabled], 4.55, fallbackText);

        SetBrushColor(dictionary, "ConfiguratorCanvasBrush", canvas);
        SetBrushColor(dictionary, "ConfiguratorInputBrush", input);
        SetBrushColor(dictionary, "ConfiguratorSurfaceBrush", surface);
        SetBrushColor(dictionary, "ConfiguratorSurfaceContainerBrush", surfaceContainer);
        SetBrushColor(dictionary, "ConfiguratorSurfaceContainerHighBrush", surfaceContainerHigh);
        SetBrushColor(dictionary, "ConfiguratorSecondaryTextBrush", secondaryText);
        SetBrushColor(dictionary, "ConfiguratorAccentBrush", accent);
        SetBrushColor(dictionary, "ConfiguratorFocusBrush", focus);
        SetBrushColor(dictionary, "ConfiguratorPrimaryContainerBrush", primaryContainer);
        SetBrushColor(dictionary, "ConfiguratorOnPrimaryContainerBrush", onPrimaryContainer);
        SetBrushColor(dictionary, "ConfiguratorSuccessBrush", success);
        SetBrushColor(dictionary, "ConfiguratorWarningBrush", warning);
        SetBrushColor(dictionary, "ConfiguratorSelectedBrush", selected);
        SetBrushColor(dictionary, "ConfiguratorSelectedTextBrush", selectedText);
        SetBrushColor(dictionary, "ConfiguratorDisabledBrush", disabled);
        SetBrushColor(dictionary, "ConfiguratorDisabledTextBrush", disabledText);
        SetBrushColor(dictionary, "ConfiguratorErrorBrush", error);
        SetBrushColor(dictionary, "ConfiguratorTextBrush", text);
        SetBrushColor(dictionary, "ConfiguratorBorderBrush", border);
        SetBrushColor(dictionary, "ConfiguratorOutlineVariantBrush", outlineVariant);
    }

    private static void SetBrushColor(ResourceDictionary dictionary, string key, Color color)
    {
        if (dictionary.TryGetValue(key, out object? existing) && existing is SolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            dictionary[key] = new SolidColorBrush(color);
        }
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        if (Color.TryParse(value.Trim(), out Color parsed))
            return parsed;

        string trimmed = value.Trim();
        if (trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            string[] parts = trimmed[5..^1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3 &&
                byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
            {
                return Color.FromRgb(r, g, b);
            }
        }

        return fallback;
    }

    private static Color ShiftLightness(Color color, double deltaL)
    {
        HslColor hsl = HslColor.FromRgb(color.R / 255.0, color.G / 255.0, color.B / 255.0);
        HslColor shifted = hsl.AdjustLightness(hsl.L + deltaL);
        return ToAvaloniaColor(shifted);
    }

    private static Color BlendColors(Color a, Color b, double amountB)
    {
        double weightB = Math.Clamp(amountB, 0.0, 1.0);
        double weightA = 1.0 - weightB;
        return Color.FromRgb(
            (byte)Math.Round(a.R * weightA + b.R * weightB),
            (byte)Math.Round(a.G * weightA + b.G * weightB),
            (byte)Math.Round(a.B * weightA + b.B * weightB));
    }

    private static Color EnsureContrastAgainst(Color candidate, Color[] backgrounds, double minRatio, Color fallback)
    {
        if (MeetsAll(candidate, backgrounds, minRatio))
            return candidate;

        HslColor baseHsl = HslColor.FromRgb(candidate.R / 255.0, candidate.G / 255.0, candidate.B / 255.0);
        bool preferDark = backgrounds.Length > 0 && !IsDarkColor(backgrounds[0]);

        // Search preserving hue toward darker or lighter first, then opposite direction.
        foreach (bool goDark in new[] { preferDark, !preferDark })
        {
            for (int step = 1; step <= 40; step++)
            {
                double targetL = goDark
                    ? Math.Max(0.0, baseHsl.L - step * (baseHsl.L / 40.0))
                    : Math.Min(100.0, baseHsl.L + step * ((100.0 - baseHsl.L) / 40.0));
                Color adjusted = ToAvaloniaColor(baseHsl.AdjustLightness(targetL));
                if (MeetsAll(adjusted, backgrounds, minRatio))
                    return adjusted;
            }
        }

        if (MeetsAll(fallback, backgrounds, minRatio))
            return fallback;

        Color black = Colors.Black;
        Color white = Colors.White;
        return MinContrastRatio(black, backgrounds) >= MinContrastRatio(white, backgrounds) ? black : white;
    }

    private static bool MeetsAll(Color foreground, Color[] backgrounds, double minRatio) =>
        MinContrastRatio(foreground, backgrounds) >= minRatio;

    private static double MinContrastRatio(Color foreground, Color[] backgrounds)
    {
        double min = double.MaxValue;
        for (int index = 0; index < backgrounds.Length; index++)
        {
            double ratio = ContrastRatio(foreground, backgrounds[index]);
            if (ratio < min) min = ratio;
        }
        return min;
    }

    private static bool IsDarkColor(Color color)
    {
        HslColor hsl = HslColor.FromRgb(color.R / 255.0, color.G / 255.0, color.B / 255.0);
        return hsl.RelativeLuminance() < 0.18;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        HslColor hslA = HslColor.FromRgb(first.R / 255.0, first.G / 255.0, first.B / 255.0);
        HslColor hslB = HslColor.FromRgb(second.R / 255.0, second.G / 255.0, second.B / 255.0);
        return HslColor.ContrastRatio(hslA, hslB);
    }

    private static Color ToAvaloniaColor(HslColor hsl)
    {
        (double r, double g, double b) = hsl.ToRgb();
        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round(r * 255.0), 0, 255),
            (byte)Math.Clamp(Math.Round(g * 255.0), 0, 255),
            (byte)Math.Clamp(Math.Round(b * 255.0), 0, 255));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe();
    }
}
