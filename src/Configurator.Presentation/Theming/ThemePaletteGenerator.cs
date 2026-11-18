namespace Configurator.Presentation.Theming;

/// <summary>Generates deterministic Light, Dark, and WCAG AAA High-Contrast palettes from Natural and Brand hex colors.</summary>
public static class ThemePaletteGenerator
{
    public static ThemePalette GenerateLightPalette(string naturalHex, string brandHex)
    {
        HslColor natural = HslColor.FromHex(naturalHex);
        HslColor brand = HslColor.FromHex(brandHex);

        string background = natural.ToHex();
        string surface = natural.AdjustLightness(Math.Min(natural.L + 4.0, 100.0)).ToHex();
        HslColor textPrimaryColor = natural.AdjustLightness(natural.L > 50.0 ? natural.L - 65.0 : natural.L + 65.0);
        string textPrimary = EnsureContrast(textPrimaryColor.ToHex(), background, surface, 4.5);
        string textSecondary = natural.AdjustLightness(natural.L > 50.0 ? natural.L - 45.0 : natural.L + 45.0).ToHex();
        string divider = natural.AdjustLightness(natural.L > 50.0 ? natural.L - 10.0 : natural.L + 10.0).WithAlpha(0.12).ToHex();
        string linesDefault = natural.AdjustLightness(natural.L > 50.0 ? natural.L - 15.0 : natural.L + 15.0).ToHex();

        string primary = brand.ToHex();
        string primaryContrast = EnsureContrast(brand.ContrastTextColor(), primary, primary, 4.5);
        HslColor secondaryColor = brand.AdjustSaturation(Math.Max(brand.S - 20.0, 0.0)).AdjustLightness(Math.Max(brand.L - 15.0, 0.0));
        string secondary = secondaryColor.ToHex();
        string secondaryContrast = EnsureContrast(
            brand.AdjustLightness(brand.L > 50.0 ? brand.L - 65.0 : brand.L + 65.0).ToHex(),
            secondary,
            secondary,
            4.5);

        return new ThemePalette
        {
            Primary = primary,
            PrimaryContrastText = primaryContrast,
            Secondary = secondary,
            SecondaryContrastText = secondaryContrast,
            Background = background,
            Surface = surface,
            AppbarBackground = surface,
            AppbarText = textPrimary,
            DrawerBackground = surface,
            DrawerText = textPrimary,
            DrawerIcon = textSecondary,
            TextPrimary = textPrimary,
            TextSecondary = textSecondary,
            Info = "#2563EB",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            LinesDefault = linesDefault,
            Divider = divider
        };
    }

    public static ThemePalette GenerateDarkPalette(string naturalHex, string brandHex)
    {
        HslColor natural = HslColor.FromHex(naturalHex);
        HslColor brand = HslColor.FromHex(brandHex);

        string background = natural.AdjustLightness(Math.Max(natural.L > 50.0 ? 8.0 : natural.L - 40.0, 5.0)).ToHex();
        string surface = natural.AdjustLightness(Math.Max(natural.L > 50.0 ? 14.0 : natural.L - 30.0, 10.0)).ToHex();
        const string textPrimary = "#F8FAFC";
        const string textSecondary = "#94A3B8";
        string divider = natural.AdjustLightness(Math.Max(natural.L > 50.0 ? 20.0 : natural.L + 10.0, 15.0)).WithAlpha(0.12).ToHex();
        const string linesDefault = "#334155";

        HslColor primaryColor = brand.AdjustLightness(Math.Max(brand.L + 10.0, 40.0));
        string primary = primaryColor.ToHex();
        string primaryContrastText = EnsureContrast("#FFFFFF", primary, primary, 4.5);
        HslColor secondaryColor = brand.AdjustSaturation(Math.Max(brand.S - 20.0, 0.0)).AdjustLightness(Math.Max(brand.L - 5.0, 30.0));
        string secondary = secondaryColor.ToHex();
        string secondaryContrastText = EnsureContrast("#F1F5F9", secondary, secondary, 4.5);

        return new ThemePalette
        {
            Primary = primary,
            PrimaryContrastText = primaryContrastText,
            Secondary = secondary,
            SecondaryContrastText = secondaryContrastText,
            Background = background,
            Surface = surface,
            AppbarBackground = background,
            AppbarText = textPrimary,
            DrawerBackground = background,
            DrawerText = textPrimary,
            DrawerIcon = "#CBD5E1",
            TextPrimary = textPrimary,
            TextSecondary = textSecondary,
            Info = "#60A5FA",
            Success = "#10B981",
            Warning = "#F59E0B",
            Error = "#EF4444",
            LinesDefault = linesDefault,
            Divider = divider
        };
    }

    public static ThemePalette GenerateHighContrastLightPalette(string naturalHex, string brandHex)
    {
        _ = HslColor.FromHex(naturalHex);
        HslColor brand = HslColor.FromHex(brandHex).AdjustSaturation(Math.Min(HslColor.FromHex(brandHex).S + 30.0, 100.0));

        // Darken high-contrast light primary until #FFFFFF text meets WCAG AAA (>= 7:1).
        HslColor primaryColor = brand;
        while (HslColor.ContrastRatio(primaryColor, HslColor.FromHex("#FFFFFF")) < 7.0 && primaryColor.L > 0.0)
            primaryColor = primaryColor.AdjustLightness(Math.Max(primaryColor.L - 2.0, 0.0));

        return new ThemePalette
        {
            Primary = primaryColor.ToHex(),
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
    }

    public static ThemePalette GenerateHighContrastDarkPalette(string naturalHex, string brandHex)
    {
        _ = HslColor.FromHex(naturalHex);
        HslColor parsedBrand = HslColor.FromHex(brandHex);
        HslColor brightBrand = parsedBrand
            .AdjustLightness(Math.Max(parsedBrand.L + 20.0, 60.0))
            .AdjustSaturation(Math.Min(parsedBrand.S + 30.0, 100.0));

        // Brighten high-contrast dark primary until #000000 text meets WCAG AAA (>= 7:1).
        while (HslColor.ContrastRatio(brightBrand, HslColor.FromHex("#000000")) < 7.0 && brightBrand.L < 100.0)
            brightBrand = brightBrand.AdjustLightness(Math.Min(brightBrand.L + 2.0, 100.0));

        return new ThemePalette
        {
            Primary = brightBrand.ToHex(),
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
    }

    public static ThemePreset CreateCustomPreset(string naturalHex, string brandHex) =>
        new()
        {
            ThemeKey = ThemePresetCatalog.CustomThemeKey,
            DisplayName = "Custom Theme",
            Description = "Custom palette derived locally from Natural and Brand colors.",
            LightPalette = GenerateLightPalette(naturalHex, brandHex),
            DarkPalette = GenerateDarkPalette(naturalHex, brandHex),
            LightHighContrastPalette = GenerateHighContrastLightPalette(naturalHex, brandHex),
            DarkHighContrastPalette = GenerateHighContrastDarkPalette(naturalHex, brandHex)
        };

    private static string EnsureContrast(string candidateHex, string firstBackgroundHex, string secondBackgroundHex, double minimumRatio)
    {
        if (HslColor.ContrastRatio(candidateHex, firstBackgroundHex) >= minimumRatio
            && HslColor.ContrastRatio(candidateHex, secondBackgroundHex) >= minimumRatio)
        {
            return candidateHex;
        }

        double darkMin = Math.Min(
            HslColor.ContrastRatio("#0F172A", firstBackgroundHex),
            HslColor.ContrastRatio("#0F172A", secondBackgroundHex));
        double lightMin = Math.Min(
            HslColor.ContrastRatio("#FFFFFF", firstBackgroundHex),
            HslColor.ContrastRatio("#FFFFFF", secondBackgroundHex));
        return darkMin >= lightMin ? "#0F172A" : "#FFFFFF";
    }
}
