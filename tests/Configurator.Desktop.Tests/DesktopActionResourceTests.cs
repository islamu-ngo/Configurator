namespace Configurator.Desktop.Tests;

using System.Globalization;
using System.Resources;
using Configurator.Desktop.ViewModels.Fields;

public sealed class DesktopActionResourceTests
{
    [Test]
    public async Task EveryShippedProviderKeyHasArabicTextAndUnsupportedLocalesFallBack()
    {
        var resources = new ResourceManager("Configurator.Desktop.Resources.DesktopStrings", typeof(FieldViewModel).Assembly);
        try
        {
            using ResourceSet neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
            using ResourceSet arabic = resources.GetResourceSet(CultureInfo.GetCultureInfo("ar"), true, false)!;
            foreach (System.Collections.DictionaryEntry entry in neutral)
                await Assert.That(string.IsNullOrWhiteSpace(arabic.GetString((string)entry.Key))).IsFalse();
            var fallback = new Configurator.Desktop.Resources.DesktopStrings("zz");
            var english = new Configurator.Desktop.Resources.DesktopStrings("en");
            await Assert.That(fallback.ClearLabel).IsEqualTo(english.ClearLabel);
        }
        finally { resources.ReleaseAllResources(); }
    }

    [Test]
    public async Task ArabicActionsExistInTheirExactShippedResourceSet()
    {
        var resources = new ResourceManager("Configurator.Desktop.Resources.DesktopStrings",
            typeof(FieldViewModel).Assembly);
        try
        {
            using ResourceSet? arabic = resources.GetResourceSet(CultureInfo.GetCultureInfo("ar"),
                createIfNotExists: true, tryParents: false);
            await Assert.That(arabic).IsNotNull();
            foreach (string key in new[] { "SupplyLabel", "SupplyName", "SupplyHelp", "ClearLabel", "ClearName", "ClearHelp" })
            {
                string? text = arabic!.GetString(key);
                await Assert.That(string.IsNullOrWhiteSpace(text)).IsFalse();
                await Assert.That(text!.Any(character => character is >= '\u0600' and <= '\u06ff')).IsTrue();
            }
        }
        finally { resources.ReleaseAllResources(); }
    }
}
