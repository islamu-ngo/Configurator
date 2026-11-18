namespace Configurator.Presentation.Tests.Theming;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Configurator.Presentation;
using Configurator.Presentation.Theming;

public sealed class ThemeCatalogAndGeneratorTests
{
    [Test]
    public async Task PresetCatalogContainsExactEightEventPresetsWithEnterpriseBlueDefault()
    {
        IReadOnlyList<ThemePreset> all = ThemePresetCatalog.All;
        string[] expectedKeys =
        [
            "enterprise-blue",
            "emerald-green",
            "abyssal-dark",
            "pure-white",
            "classic-white",
            "classic-dark",
            "light-hc",
            "dark-hc"
        ];

        await Assert.That(all.Count).IsEqualTo(8);
        await Assert.That(all.Select(preset => preset.ThemeKey).ToArray()).IsEquivalentTo(expectedKeys);
        await Assert.That(ThemePresetCatalog.DefaultThemeKey).IsEqualTo("enterprise-blue");
        await Assert.That(ThemePresetCatalog.DefaultPreset.ThemeKey).IsEqualTo("enterprise-blue");
        await Assert.That(ThemePresetCatalog.DefaultPreset.DisplayName).IsEqualTo("Enterprise Blue");
        await Assert.That(ThemePresetCatalog.DefaultPreset.LightPalette.Primary).IsEqualTo("#18181B");
        await Assert.That(ThemePresetCatalog.DefaultPreset.LightPalette.Background).IsEqualTo("#F5F5F7");
        await Assert.That(ThemePresetCatalog.DefaultPreset.DarkPalette.Primary).IsEqualTo("#FAFAFA");
        await Assert.That(ThemePresetCatalog.DefaultPreset.DarkPalette.Background).IsEqualTo("#1A1A1A");
        await Assert.That(ThemePresetCatalog.DefaultPreset.DarkPalette.Surface).IsEqualTo("#242424");

        ThemePreset emerald = ThemePresetCatalog.GetPresetOrDefault("emerald-green");
        await Assert.That(emerald.DisplayName).IsEqualTo("Emerald Green");
        await Assert.That(emerald.LightPalette.Primary).IsEqualTo("#16A34A");
        await Assert.That(emerald.DarkPalette.Primary).IsEqualTo("#22C55E");

        ThemePreset unknownFallback = ThemePresetCatalog.GetPresetOrDefault("nonexistent-preset");
        await Assert.That(unknownFallback.ThemeKey).IsEqualTo("enterprise-blue");
    }

    [Test]
    public async Task AllEightPresetsSatisfyWcagContrastInLightDarkAndHighContrastModes()
    {
        foreach (ThemePreset preset in ThemePresetCatalog.All)
        {
            double lightText = HslColor.ContrastRatio(preset.LightPalette.TextPrimary, preset.LightPalette.Surface);
            double lightBgText = HslColor.ContrastRatio(preset.LightPalette.TextPrimary, preset.LightPalette.Background);
            double lightPrimary = HslColor.ContrastRatio(preset.LightPalette.PrimaryContrastText, preset.LightPalette.Primary);
            await Assert.That(lightText).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} light surface");
            await Assert.That(lightBgText).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} light bg");
            await Assert.That(lightPrimary).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} light primary");

            double darkText = HslColor.ContrastRatio(preset.DarkPalette.TextPrimary, preset.DarkPalette.Surface);
            double darkBgText = HslColor.ContrastRatio(preset.DarkPalette.TextPrimary, preset.DarkPalette.Background);
            double darkPrimary = HslColor.ContrastRatio(preset.DarkPalette.PrimaryContrastText, preset.DarkPalette.Primary);
            await Assert.That(darkText).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} dark surface");
            await Assert.That(darkBgText).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} dark bg");
            await Assert.That(darkPrimary).IsGreaterThanOrEqualTo(4.5).Because($"{preset.ThemeKey} dark primary");

            double lightHcText = HslColor.ContrastRatio(
                preset.LightHighContrastPalette.TextPrimary,
                preset.LightHighContrastPalette.Surface);
            double darkHcText = HslColor.ContrastRatio(
                preset.DarkHighContrastPalette.TextPrimary,
                preset.DarkHighContrastPalette.Surface);
            await Assert.That(lightHcText).IsGreaterThanOrEqualTo(7.0).Because($"{preset.ThemeKey} light-hc");
            await Assert.That(darkHcText).IsGreaterThanOrEqualTo(7.0).Because($"{preset.ThemeKey} dark-hc");
        }
    }

    [Test]
    public async Task PaletteGeneratorComputesDeterministicPalettesAndRejectsMalformedHex()
    {
        ThemePalette light = ThemePaletteGenerator.GenerateLightPalette("#F5F5F7", "#16A34A");
        ThemePalette lightRepeat = ThemePaletteGenerator.GenerateLightPalette("#f5f5f7", "#16a34a");
        ThemePalette dark = ThemePaletteGenerator.GenerateDarkPalette("#475569", "#3B82F6");
        ThemePalette lightHc = ThemePaletteGenerator.GenerateHighContrastLightPalette("#475569", "#3B82F6");
        ThemePalette darkHc = ThemePaletteGenerator.GenerateHighContrastDarkPalette("#475569", "#3B82F6");

        await Assert.That(light).IsEqualTo(lightRepeat);
        await Assert.That(light.Background).IsEqualTo("#F5F5F7");
        await Assert.That(light.Primary).IsEqualTo("#16A34A");
        await Assert.That(HslColor.ContrastRatio(light.TextPrimary, light.Surface)).IsGreaterThanOrEqualTo(4.5);
        await Assert.That(HslColor.ContrastRatio(dark.TextPrimary, dark.Surface)).IsGreaterThanOrEqualTo(4.5);
        await Assert.That(lightHc.Background).IsEqualTo("#FFFFFF");
        await Assert.That(lightHc.TextPrimary).IsEqualTo("#000000");
        await Assert.That(HslColor.ContrastRatio(lightHc.TextPrimary, lightHc.Surface)).IsGreaterThanOrEqualTo(7.0);
        await Assert.That(HslColor.ContrastRatio(lightHc.PrimaryContrastText, lightHc.Primary)).IsGreaterThanOrEqualTo(7.0);
        await Assert.That(darkHc.Background).IsEqualTo("#000000");
        await Assert.That(darkHc.TextPrimary).IsEqualTo("#FFFFFF");
        await Assert.That(HslColor.ContrastRatio(darkHc.TextPrimary, darkHc.Surface)).IsGreaterThanOrEqualTo(7.0);
        await Assert.That(HslColor.ContrastRatio(darkHc.PrimaryContrastText, darkHc.Primary)).IsGreaterThanOrEqualTo(7.0);

        ThemePalette shortHex = ThemePaletteGenerator.GenerateLightPalette("#fff", "#000");
        await Assert.That(shortHex.Background).IsEqualTo("#FFFFFF");
        await Assert.That(shortHex.Primary).IsEqualTo("#000000");

        string[] malformed = ["", "   ", "#", "#12", "#12345", "#GGGGGG", "#1234567", "red", "javascript:alert(1)"];
        foreach (string bad in malformed)
        {
            await Assert.That(() => ThemePaletteGenerator.GenerateLightPalette(bad, "#3B82F6"))
                .Throws<ArgumentException>();
            await Assert.That(() => ThemePaletteGenerator.GenerateDarkPalette("#475569", bad))
                .Throws<ArgumentException>();
            await Assert.That(HslColor.TryParseHex(bad, out _)).IsFalse();
        }
    }

    [Test]
    public async Task PaletteFormattingRemainsInvariantUnderCommaDecimalCultures()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            ThemePalette arLight = ThemePaletteGenerator.GenerateLightPalette("#F5F5F7", "#16A34A");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            ThemePalette deLight = ThemePaletteGenerator.GenerateLightPalette("#F5F5F7", "#16A34A");

            await Assert.That(arLight.Divider).Contains("0.12");
            await Assert.That(arLight.Divider).DoesNotContain("0,12");
            await Assert.That(deLight).IsEqualTo(arLight);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task ThemePreferenceParsesLegacyAndSevenFieldJsonWithinBounds()
    {
        ThemePreference legacyTwo = ThemePreference.Parse("""{"themeMode":"dark","direction":"rtl"}""");
        await Assert.That(legacyTwo).IsEqualTo(new ThemePreference(ThemeMode: "dark", Direction: "rtl"));
        await Assert.That(legacyTwo.ThemeKey).IsEqualTo("enterprise-blue");

        ThemePreference legacyFour = ThemePreference.Parse(
            """{"themeMode":"light","direction":"ltr","density":"compact","namingMode":"technical"}""");
        await Assert.That(legacyFour).IsEqualTo(new ThemePreference(
            ThemeMode: "light",
            Direction: "ltr",
            Density: "compact",
            NamingMode: "technical"));

        ThemePreference sevenField = new(
            ThemeMode: "darkhighcontrast",
            ThemeKey: "custom",
            CustomNaturalHex: "#475569",
            CustomBrandHex: "#16A34A",
            Direction: "rtl",
            Density: "dense",
            NamingMode: "technical");
        string json = sevenField.ToJson();
        await Assert.That(Encoding.UTF8.GetByteCount(json)).IsLessThanOrEqualTo(ThemePreference.MaximumJsonBytes);
        using JsonDocument parsedDoc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
        await Assert.That(parsedDoc.RootElement.EnumerateObject().Count()).IsEqualTo(7);
        await Assert.That(ThemePreference.Parse(json)).IsEqualTo(sevenField);

        string oversized = "{\"themeMode\":\"dark\",\"direction\":\"rtl\",\"themeKey\":\"" + new string('a', 500) + "\"}";
        string[] rejectedPayloads =
        [
            oversized,
            """{"themeMode":"dark","direction":"rtl","answers":"synthetic-private-canary"}""",
            """{"themeMode":"dark","direction":"rtl","themeKey":"unknown-preset"}""",
            """{"themeMode":"dark","direction":"rtl","themeKey":"custom","customNaturalHex":"nothex","customBrandHex":"#3B82F6"}""",
            """{"themeMode":"dark","direction":"rtl","themeKey":"custom","customNaturalHex":"#12","customBrandHex":"#3B82F6"}""",
            """{"themeMode":"dark","direction":"rtl","themeMode":"light"}""",
            """{"themeMode":"dark","direction":"rtl","density":{"nested":true}}""",
            "{broken",
            "[]"
        ];

        foreach (string payload in rejectedPayloads)
        {
            await Assert.That(ThemePreference.Parse(payload)).IsEqualTo(new ThemePreference());
        }
    }

    [Test]
    public async Task ResolvePaletteSelectsCorrectModeAndCustomPair()
    {
        ThemePreference custom = new(
            ThemeMode: "system",
            ThemeKey: "custom",
            CustomNaturalHex: "#475569",
            CustomBrandHex: "#16A34A");

        ThemePalette systemLight = ThemePresetCatalog.ResolvePalette(custom, systemDark: false);
        ThemePalette systemDark = ThemePresetCatalog.ResolvePalette(custom, systemDark: true);
        ThemePalette systemDarkHc = ThemePresetCatalog.ResolvePalette(custom, systemDark: true, systemHighContrast: true);

        await Assert.That(systemLight).IsEqualTo(ThemePaletteGenerator.GenerateLightPalette("#475569", "#16A34A"));
        await Assert.That(systemDark).IsEqualTo(ThemePaletteGenerator.GenerateDarkPalette("#475569", "#16A34A"));
        await Assert.That(systemDarkHc).IsEqualTo(ThemePaletteGenerator.GenerateHighContrastDarkPalette("#475569", "#16A34A"));

        ThemePreference withPreset = custom with { ThemeKey = "emerald-green", ThemeMode = "lighthighcontrast" };
        ThemePalette resolvedHc = ThemePresetCatalog.ResolvePalette(withPreset, systemDark: true);
        await Assert.That(resolvedHc).IsEqualTo(ThemePresetCatalog.GetPresetOrDefault("emerald-green").LightHighContrastPalette);
    }
}
