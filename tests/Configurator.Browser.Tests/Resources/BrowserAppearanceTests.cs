namespace Configurator.Browser.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Browser.Services;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserAppearanceTests
{
    [Test]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"rtl\",\"answers\":\"synthetic-private-canary\"}")]
    [Arguments("{\"themeMode\":\"<script>synthetic</script>\",\"direction\":\"rtl\"}")]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"rtl\",\"direction\":\"ltr\"}")]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"unknown\"}")]
    [Arguments("{\"themeMode\":1,\"direction\":\"auto\"}")]
    [Arguments("{broken")]
    [Arguments("[]")]
    public async Task CorruptOrAnswerBearingPreferencesFallBackToSystem(string preference)
    {
        await Assert.That(AppearanceState.Parse(preference)).IsEqualTo(new AppearanceState());
    }

    [Test]
    public async Task OversizedPreferencesCannotBecomePersistentConfiguration()
    {
        await Assert.That(AppearanceState.Parse(new string('x', 257))).IsEqualTo(new AppearanceState());
    }

    [Test]
    [Arguments("system")]
    [Arguments("light")]
    [Arguments("dark")]
    [Arguments("lighthighcontrast")]
    [Arguments("darkhighcontrast")]
    public async Task SupportedAppearanceAndDirectionRoundTrip(string mode)
    {
        await Assert.That(AppearanceState.Parse($$"""{"themeMode":"{{mode}}","direction":"rtl"}""")).IsEqualTo(new AppearanceState(mode, "rtl"));
    }

    [Test]
    public async Task OnlyBoundedPreferencesCrossStorageInteropAndSystemChangesStayLocal()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/browser-appearance.js");
        module.Setup<string?>("read", _ => true).SetResult("""{"themeMode":"dark","direction":"rtl","answers":"synthetic-private-canary"}""");
        module.SetupVoid("watch", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
        module.SetupVoid("write", _ => true).SetVoidResult();
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        await using var service = new AppearanceThemeService(context.JSInterop.JSRuntime);
        await service.InitializeAsync();
        await Assert.That(service.Current).IsEqualTo(new AppearanceState());
        service.SystemPreferencesChanged(true, true);
        await Assert.That(service.IsDark).IsTrue();
        await Assert.That(service.IsRtl).IsTrue();
        await service.SetAsync("lighthighcontrast", "ltr");
        await Assert.That(service.IsDark).IsFalse();
        await Assert.That(service.IsRtl).IsFalse();
        await service.SetAsync("synthetic-private-canary", "ltr");
        await Assert.That(service.Current).IsEqualTo(new AppearanceState("lighthighcontrast", "ltr"));
        var writes = module.Invocations.Where(call => call.Identifier == "write").ToArray();
        await Assert.That(writes.Length).IsEqualTo(1);
        await Assert.That(writes[0].Arguments.Cast<string>().ToArray()).IsEquivalentTo(["lighthighcontrast", "ltr", "comfortable", "humanized"]);
    }

    [Test]
    [Arguments("spacious", "humanized")]
    [Arguments("comfortable", "humanized")]
    [Arguments("compact", "technical")]
    [Arguments("dense", "technical")]
    public async Task SupportedDensityAndNamingModeRoundTripWithinByteBudget(string density, string namingMode)
    {
        var state = new AppearanceState("dark", "rtl", density, namingMode);
        string json = state.ToJson();
        await Assert.That(Encoding.UTF8.GetByteCount(json) <= 256).IsTrue();
        using JsonDocument parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
        await Assert.That(parsed.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(AppearanceState.Parse(json)).IsEqualTo(state);
    }

    [Test]
    [Arguments("""{"themeMode":"dark","direction":"rtl","density":"microscopic","namingMode":"humanized"}""")]
    [Arguments("""{"themeMode":"dark","direction":"rtl","density":"compact","namingMode":"secret"}""")]
    [Arguments("""{"themeMode":"dark","direction":"rtl","density":"compact","density":"dense"}""")]
    [Arguments("""{"themeMode":"dark","direction":"rtl","density":{"nested":true}}""")]
    [Arguments("""{"themeMode":"dark","direction":"rtl","density":"dense","namingMode":"technical","answers":"synthetic-canary"}""")]
    public async Task MalformedOrAnswerBearingDensityAndNamingFallBackToDefaults(string preference)
    {
        await Assert.That(AppearanceState.Parse(preference)).IsEqualTo(new AppearanceState());
    }

    [Test]
    public async Task DensityAndNamingPreferencesPersistBoundedAndSyncServiceState()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/browser-appearance.js");
        module.Setup<string?>("read", _ => true).SetResult("""{"themeMode":"light","direction":"ltr","density":"spacious","namingMode":"technical"}""");
        module.SetupVoid("watch", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
        module.SetupVoid("write", _ => true).SetVoidResult();
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        await using var service = new AppearanceThemeService(context.JSInterop.JSRuntime);
        await service.InitializeAsync();
        await Assert.That(service.Current).IsEqualTo(new AppearanceState("light", "ltr", "spacious", "technical"));

        await service.SetDensityAsync("dense");
        await service.SetNamingModeAsync("humanized");
        await service.SetDensityAsync("invalid-density");
        await service.SetNamingModeAsync("invalid-naming");

        await Assert.That(service.Current).IsEqualTo(new AppearanceState("light", "ltr", "dense", "humanized"));
        var writes = module.Invocations.Where(call => call.Identifier == "write").ToArray();
        await Assert.That(writes.Length).IsEqualTo(2);
        await Assert.That(writes[1].Arguments.Cast<string>().ToArray()).IsEquivalentTo(["light", "ltr", "dense", "humanized"]);
    }

    [Test]
    public async Task Material3ThemePalettesMeetContrastTargets()
    {
        await using var context = BrowserFixture.Context();
        var service = context.Services.GetRequiredService<AppearanceThemeService>();

        await service.SetAsync("light", "ltr");
        var lightTheme = service.CreateTheme();
        await Assert.That(ContrastRatio(lightTheme.PaletteLight.TextPrimary, lightTheme.PaletteLight.Surface) >= 4.5).IsTrue();
        await Assert.That(ContrastRatio(lightTheme.PaletteLight.PrimaryContrastText, lightTheme.PaletteLight.Primary) >= 4.5).IsTrue();

        await service.SetAsync("dark", "ltr");
        var darkTheme = service.CreateTheme();
        await Assert.That(ContrastRatio(darkTheme.PaletteDark.TextPrimary, darkTheme.PaletteDark.Surface) >= 4.5).IsTrue();
        await Assert.That(ContrastRatio(darkTheme.PaletteDark.PrimaryContrastText, darkTheme.PaletteDark.Primary) >= 4.5).IsTrue();

        await service.SetAsync("lighthighcontrast", "ltr");
        var hcLightTheme = service.CreateTheme();
        await Assert.That(ContrastRatio(hcLightTheme.PaletteLight.TextPrimary, hcLightTheme.PaletteLight.Surface) >= 7.0).IsTrue();

        await service.SetAsync("darkhighcontrast", "ltr");
        var hcDarkTheme = service.CreateTheme();
        await Assert.That(ContrastRatio(hcDarkTheme.PaletteDark.TextPrimary, hcDarkTheme.PaletteDark.Surface) >= 7.0).IsTrue();
    }

    [Test]
    public async Task TokensCssDefinesNonOverlappingMaterial3SurfacesAndFourStepDensityBounds()
    {
        string tokensPath = Path.Combine(RepositoryRoot(), "src", "Configurator.Browser", "wwwroot", "css", "tokens.css");
        string css = await File.ReadAllTextAsync(tokensPath);

        string[] m3Tokens =
        [
            "--isl-surface-container-lowest",
            "--isl-surface-container-low",
            "--isl-surface-container",
            "--isl-surface-container-high",
            "--isl-surface-container-highest"
        ];
        string darkBlock = ExtractCssBlock(css, ".appearance--dark");
        await Assert.That(darkBlock).IsNotEmpty();
        foreach (string token in m3Tokens)
        {
            string lightValue = ExtractCssCustomProperty(css, ":root", token);
            string darkValue = ExtractCssCustomProperty(darkBlock, token);
            await Assert.That(lightValue).IsNotEmpty();
            await Assert.That(darkValue).IsNotEmpty();
            await Assert.That(string.Equals(lightValue, darkValue, StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
        await Assert.That(css).Contains("--isl-focus-ring-width:");
        await Assert.That(css).Contains("--isl-focus-ring-offset:");

        (string Name, double MinPx)[] densityChecks =
        [
            ("spacious", 44),
            ("comfortable", 44),
            ("compact", 32),
            ("dense", 24)
        ];
        foreach ((string name, double minPx) in densityChecks)
        {
            string block = ExtractCssBlock(css, $"[data-density=\"{name}\"]");
            await Assert.That(block).IsNotEmpty();
            double rowHeight = ParseCssPx(ExtractCssCustomProperty(block, "--density-row-height"));
            double controlMin = ParseCssPx(ExtractCssCustomProperty(block, "--density-control-min-height"));
            await Assert.That(rowHeight >= minPx).IsTrue();
            await Assert.That(controlMin >= minPx).IsTrue();
        }
    }

    [Test]
    public async Task ThemeQuickSwitcherOffersFourStepDensityAndNamingModeControls()
    {
        await using var context = BrowserFixture.Context();
        var service = context.Services.GetRequiredService<AppearanceThemeService>();
        var component = context.Render<ThemeQuickSwitcher>();

        string[] densityOptions = component.FindAll("#appearance-density option").Select(item => item.GetAttribute("value") ?? "").ToArray();
        string[] namingOptions = component.FindAll("#appearance-naming option").Select(item => item.GetAttribute("value") ?? "").ToArray();
        await Assert.That(densityOptions).IsEquivalentTo(["spacious", "comfortable", "compact", "dense"]);
        await Assert.That(namingOptions).IsEquivalentTo(["humanized", "technical"]);

        await component.Find("#appearance-density").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "dense" });
        await component.Find("#appearance-naming").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "technical" });
        await Assert.That(service.Current.Density).IsEqualTo("dense");
        await Assert.That(service.Current.NamingMode).IsEqualTo("technical");
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string ExtractCssBlock(string css, string selector)
    {
        int start = css.IndexOf(selector, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        int open = css.IndexOf('{', start);
        int close = css.IndexOf('}', open + 1);
        return open >= 0 && close > open ? css[(open + 1)..close] : string.Empty;
    }

    private static string ExtractCssCustomProperty(string cssOrBlock, string scopeSelector, string propertyName) =>
        ExtractCssCustomProperty(ExtractCssBlock(cssOrBlock, scopeSelector), propertyName);

    private static string ExtractCssCustomProperty(string block, string propertyName)
    {
        Match match = Regex.Match(block, $@"{Regex.Escape(propertyName)}\s*:\s*([^;]+);");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static double ParseCssPx(string value)
    {
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^2].Trim(), CultureInfo.InvariantCulture, out double px))
            return px;
        if (value.EndsWith("rem", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^3].Trim(), CultureInfo.InvariantCulture, out double rem))
            return rem * 16.0;
        return 0;
    }

    private static double ContrastRatio(MudBlazor.Utilities.MudColor foreground, MudBlazor.Utilities.MudColor background)
    {
        double l1 = RelativeLuminance(foreground);
        double l2 = RelativeLuminance(background);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(MudBlazor.Utilities.MudColor color) =>
        0.2126 * Linearize(color.R / 255.0) + 0.7152 * Linearize(color.G / 255.0) + 0.0722 * Linearize(color.B / 255.0);

    private static double Linearize(double channel) =>
        channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    [Test]
    public async Task LatePreferenceReadCannotReplaceTheUsersCurrentChoice()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/browser-appearance.js");
        var read = module.Setup<string?>("read", _ => true);
        module.SetupVoid("watch", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
        module.SetupVoid("write", _ => true).SetVoidResult();
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        await using var service = new AppearanceThemeService(context.JSInterop.JSRuntime);
        ValueTask initialization = service.InitializeAsync();
        await service.SetAsync("dark", "rtl");
        read.SetResult("""{"themeMode":"light","direction":"ltr"}""");
        await initialization;
        await Assert.That(service.Current).IsEqualTo(new AppearanceState("dark", "rtl"));
    }
}
