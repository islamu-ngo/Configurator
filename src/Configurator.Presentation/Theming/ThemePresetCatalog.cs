namespace Configurator.Presentation.Theming;

using System.Diagnostics.CodeAnalysis;

/// <summary>Authoritative catalog of the 8 ISLAMU Event theme presets and palette resolution.</summary>
public static class ThemePresetCatalog
{
    public const string DefaultThemeKey = "enterprise-blue";
    public const string CustomThemeKey = "custom";

    private static readonly ThemePreset EnterpriseBlue = new()
    {
        ThemeKey = "enterprise-blue",
        DisplayName = "Enterprise Blue",
        Description = "Default professional theme with a clean monochrome chrome palette.",
        LightPalette = new ThemePalette
        {
            Primary = "#18181B",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#52525B",
            SecondaryContrastText = "#FFFFFF",
            Background = "#F5F5F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18181B",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#18181B",
            DrawerIcon = "#52525B",
            TextPrimary = "#18181B",
            TextSecondary = "#404040",
            Info = "#52525B",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = "#A1A1AA",
            Divider = "#E4E4E7"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#FAFAFA",
            PrimaryContrastText = "#1A1A1A",
            Secondary = "#A1A1AA",
            SecondaryContrastText = "#1A1A1A",
            Background = "#1A1A1A",
            Surface = "#242424",
            AppbarBackground = "rgba(26,26,26,0.92)",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#1A1A1A",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#A1A1AA",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#A1A1AA",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            LinesDefault = "#3F3F46",
            Divider = "#2E2E2E"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#F5F5F7", "#18181B"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#1A1A1A", "#93C5FD")
    };

    private static readonly ThemePreset EmeraldGreen = new()
    {
        ThemeKey = "emerald-green",
        DisplayName = "Emerald Green",
        Description = "Fresh and natural theme with green accents, suited to ISLAMU community branding.",
        LightPalette = new ThemePalette
        {
            Primary = "#16A34A",
            PrimaryContrastText = "#0F172A",
            Secondary = "#52525B",
            SecondaryContrastText = "#FFFFFF",
            Background = "#F5F5F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18181B",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#18181B",
            DrawerIcon = "#52525B",
            TextPrimary = "#18181B",
            TextSecondary = "#52525B",
            Info = "#2563EB",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = "#D4D4D8",
            Divider = "#E4E4E7"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#22C55E",
            PrimaryContrastText = "#18181B",
            Secondary = "#E4E4E7",
            SecondaryContrastText = "#18181B",
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "rgba(18,18,18,0.92)",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#121212",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#A1A1AA",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#60A5FA",
            Success = "#22C55E",
            Warning = "#FBBF24",
            Error = "#F87171",
            LinesDefault = "#3F3F46",
            Divider = "#27272A"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#F5F5F7", "#16A34A"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#121212", "#22C55E")
    };

    private static readonly ThemePreset AbyssalDark = new()
    {
        ThemeKey = "abyssal-dark",
        DisplayName = "Abyssal Dark",
        Description = "Deep charcoal surfaces with crisp blue accents.",
        LightPalette = new ThemePalette
        {
            Primary = "#0F62FE",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#52525B",
            SecondaryContrastText = "#FFFFFF",
            Background = "#F5F5F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18181B",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#18181B",
            DrawerIcon = "#71717A",
            TextPrimary = "#18181B",
            TextSecondary = "#71717A",
            Info = "#2563EB",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = "#D4D4D8",
            Divider = "#E4E4E7"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#60A5FA",
            PrimaryContrastText = "#18181B",
            Secondary = "#A1A1AA",
            SecondaryContrastText = "#18181B",
            Background = "#09090B",
            Surface = "#18181B",
            AppbarBackground = "rgba(9,9,11,0.95)",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#09090B",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#71717A",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#60A5FA",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            LinesDefault = "#27272A",
            Divider = "#27272A"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#F5F5F7", "#0F62FE"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#09090B", "#60A5FA")
    };

    private static readonly ThemePreset PureWhite = new()
    {
        ThemeKey = "pure-white",
        DisplayName = "Pure White",
        Description = "Minimal clean theme with subtle neutral boundaries for maximum clarity.",
        LightPalette = new ThemePalette
        {
            Primary = "#3B82F6",
            PrimaryContrastText = "#0F172A",
            Secondary = "#71717A",
            SecondaryContrastText = "#FFFFFF",
            Background = "#FFFFFF",
            Surface = "#FAFAFA",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18181B",
            DrawerBackground = "#FAFAFA",
            DrawerText = "#18181B",
            DrawerIcon = "#71717A",
            TextPrimary = "#18181B",
            TextSecondary = "#71717A",
            Info = "#3B82F6",
            Success = "#10B981",
            Warning = "#F59E0B",
            Error = "#EF4444",
            LinesDefault = "#E4E4E7",
            Divider = "#F4F4F5"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#60A5FA",
            PrimaryContrastText = "#18181B",
            Secondary = "#A1A1AA",
            SecondaryContrastText = "#18181B",
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "#121212",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#121212",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#71717A",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#60A5FA",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            LinesDefault = "#3F3F46",
            Divider = "#3F3F46"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#FFFFFF", "#3B82F6"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#121212", "#60A5FA")
    };

    private static readonly ThemePreset ClassicWhite = new()
    {
        ThemeKey = "classic-white",
        DisplayName = "White",
        Description = "Clean, bright theme with pure white surfaces and crisp blue accents.",
        LightPalette = new ThemePalette
        {
            Primary = "#2563EB",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#71717A",
            SecondaryContrastText = "#FFFFFF",
            Background = "#FFFFFF",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#18181B",
            DrawerBackground = "#FAFAFA",
            DrawerText = "#18181B",
            DrawerIcon = "#71717A",
            TextPrimary = "#18181B",
            TextSecondary = "#52525B",
            Info = "#2563EB",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = "#D4D4D8",
            Divider = "#E4E4E7"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#60A5FA",
            PrimaryContrastText = "#18181B",
            Secondary = "#A1A1AA",
            SecondaryContrastText = "#18181B",
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "rgba(18,18,18,0.92)",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#121212",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#A1A1AA",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#60A5FA",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            LinesDefault = "#3F3F46",
            Divider = "#27272A"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#FFFFFF", "#2563EB"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#121212", "#60A5FA")
    };

    private static readonly ThemePreset ClassicDark = new()
    {
        ThemeKey = "classic-dark",
        DisplayName = "Dark",
        Description = "Refined dark theme with deep charcoal surfaces and vibrant indigo accents.",
        LightPalette = new ThemePalette
        {
            Primary = "#2563EB",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#64748B",
            SecondaryContrastText = "#FFFFFF",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#0F172A",
            DrawerIcon = "#64748B",
            TextPrimary = "#0F172A",
            TextSecondary = "#475569",
            Info = "#2563EB",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = "#E2E8F0",
            Divider = "#E2E8F0"
        },
        DarkPalette = new ThemePalette
        {
            Primary = "#818CF8",
            PrimaryContrastText = "#0F172A",
            Secondary = "#A1A1AA",
            SecondaryContrastText = "#0F172A",
            Background = "#09090B",
            Surface = "#18181B",
            AppbarBackground = "rgba(9,9,11,0.92)",
            AppbarText = "#FAFAFA",
            DrawerBackground = "#09090B",
            DrawerText = "#FAFAFA",
            DrawerIcon = "#A1A1AA",
            TextPrimary = "#FAFAFA",
            TextSecondary = "#A1A1AA",
            Info = "#60A5FA",
            Success = "#4ADE80",
            Warning = "#FACC15",
            Error = "#F87171",
            LinesDefault = "#27272A",
            Divider = "#27272A"
        },
        LightHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastLightPalette("#F8FAFC", "#2563EB"),
        DarkHighContrastPalette = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#09090B", "#818CF8")
    };

    private static readonly ThemePalette LightHighContrastBasePalette = new()
    {
        Primary = "#0050D8",
        PrimaryContrastText = "#FFFFFF",
        Secondary = "#1E293B",
        SecondaryContrastText = "#FFFFFF",
        Background = "#FFFFFF",
        Surface = "#FFFFFF",
        AppbarBackground = "#FFFFFF",
        AppbarText = "#000000",
        DrawerBackground = "#FFFFFF",
        DrawerText = "#000000",
        DrawerIcon = "#000000",
        TextPrimary = "#000000",
        TextSecondary = "#1E293B",
        Info = "#0050D8",
        Success = "#006600",
        Warning = "#B45309",
        Error = "#B91C1C",
        LinesDefault = "#000000",
        Divider = "#000000"
    };

    private static readonly ThemePalette DarkHighContrastBasePalette = new()
    {
        Primary = "#93C5FD",
        PrimaryContrastText = "#000000",
        Secondary = "#F8FAFC",
        SecondaryContrastText = "#000000",
        Background = "#000000",
        Surface = "#0A0A0A",
        AppbarBackground = "#000000",
        AppbarText = "#FFFFFF",
        DrawerBackground = "#000000",
        DrawerText = "#FFFFFF",
        DrawerIcon = "#FFFFFF",
        TextPrimary = "#FFFFFF",
        TextSecondary = "#E2E8F0",
        Info = "#93C5FD",
        Success = "#6EE7B7",
        Warning = "#FCD34D",
        Error = "#FCA5A5",
        LinesDefault = "#FFFFFF",
        Divider = "#FFFFFF"
    };

    private static readonly ThemePreset LightHighContrast = new()
    {
        ThemeKey = "light-hc",
        DisplayName = "Light High Contrast",
        Description = "WCAG AAA-compliant light theme with maximum text contrast for accessibility.",
        LightPalette = LightHighContrastBasePalette,
        DarkPalette = DarkHighContrastBasePalette,
        LightHighContrastPalette = LightHighContrastBasePalette,
        DarkHighContrastPalette = DarkHighContrastBasePalette
    };

    private static readonly ThemePreset DarkHighContrast = new()
    {
        ThemeKey = "dark-hc",
        DisplayName = "Dark High Contrast",
        Description = "WCAG AAA-compliant dark theme with pure white text on black backgrounds.",
        LightPalette = LightHighContrastBasePalette,
        DarkPalette = DarkHighContrastBasePalette,
        LightHighContrastPalette = LightHighContrastBasePalette,
        DarkHighContrastPalette = DarkHighContrastBasePalette
    };

    private static readonly IReadOnlyList<ThemePreset> Presets = Array.AsReadOnly(new[]
    {
        EnterpriseBlue,
        EmeraldGreen,
        AbyssalDark,
        PureWhite,
        ClassicWhite,
        ClassicDark,
        LightHighContrast,
        DarkHighContrast
    });

    private static readonly Dictionary<string, ThemePreset> ByKey = Presets.ToDictionary(
        static preset => preset.ThemeKey,
        StringComparer.Ordinal);

    public static IReadOnlyList<ThemePreset> All => Presets;

    public static ThemePreset DefaultPreset => EnterpriseBlue;

    public static bool IsValidPresetKey(string? themeKey) =>
        themeKey is not null && ByKey.ContainsKey(themeKey);

    public static bool TryGetPreset(string? themeKey, [NotNullWhen(true)] out ThemePreset? preset)
    {
        if (themeKey is not null && ByKey.TryGetValue(themeKey, out ThemePreset? found))
        {
            preset = found;
            return true;
        }

        preset = null;
        return false;
    }

    public static ThemePreset GetPresetOrDefault(string? themeKey) =>
        TryGetPreset(themeKey, out ThemePreset? preset) ? preset : EnterpriseBlue;

    public static bool IsDarkEffective(ThemePreference preference, bool systemDark)
    {
        ArgumentNullException.ThrowIfNull(preference);
        return preference.ThemeMode switch
        {
            "dark" or "darkhighcontrast" => true,
            "light" or "lighthighcontrast" => false,
            _ => systemDark
        };
    }

    public static bool IsHighContrastEffective(ThemePreference preference, bool systemHighContrast = false)
    {
        ArgumentNullException.ThrowIfNull(preference);
        return preference.ThemeMode is "lighthighcontrast" or "darkhighcontrast"
            || (preference.ThemeMode == "system" && (systemHighContrast || preference.ThemeKey is "light-hc" or "dark-hc"))
            || preference.ThemeKey is "light-hc" or "dark-hc";
    }

    public static ThemePalette ResolvePalette(ThemePreference preference, bool systemDark, bool systemHighContrast = false)
    {
        ArgumentNullException.ThrowIfNull(preference);
        ThemePreset preset = preference.ThemeKey == CustomThemeKey
            && HslColor.TryParseHex(preference.CustomNaturalHex, out _)
            && HslColor.TryParseHex(preference.CustomBrandHex, out _)
            ? ThemePaletteGenerator.CreateCustomPreset(preference.CustomNaturalHex, preference.CustomBrandHex)
            : GetPresetOrDefault(preference.ThemeKey);

        return preference.ThemeMode switch
        {
            "lighthighcontrast" => preset.LightHighContrastPalette,
            "darkhighcontrast" => preset.DarkHighContrastPalette,
            "light" => preset.ThemeKey == "light-hc" ? preset.LightHighContrastPalette : preset.LightPalette,
            "dark" => preset.ThemeKey == "dark-hc" ? preset.DarkHighContrastPalette : preset.DarkPalette,
            _ => ResolveSystemPalette(preset, systemDark, systemHighContrast)
        };
    }

    private static ThemePalette ResolveSystemPalette(ThemePreset preset, bool systemDark, bool systemHighContrast)
    {
        if (systemHighContrast || preset.ThemeKey is "light-hc" or "dark-hc")
        {
            if (preset.ThemeKey == "light-hc" && !systemHighContrast)
                return preset.LightHighContrastPalette;
            if (preset.ThemeKey == "dark-hc" && !systemHighContrast)
                return preset.DarkHighContrastPalette;
            return systemDark ? preset.DarkHighContrastPalette : preset.LightHighContrastPalette;
        }

        return systemDark ? preset.DarkPalette : preset.LightPalette;
    }
}
