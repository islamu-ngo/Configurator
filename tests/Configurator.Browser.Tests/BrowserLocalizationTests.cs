namespace Configurator.Browser.Tests;

using System.Collections;
using System.Globalization;
using System.Resources;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;

public sealed class BrowserLocalizationTests
{
    [Test]
    public async Task BothLanguagesShipInMainAssemblyWithCompleteMatchingKeys()
    {
        var assembly = typeof(BrowserStrings).Assembly;
        string[] names = assembly.GetManifestResourceNames();
        await Assert.That(names.Contains("Configurator.Browser.Resources.BrowserStrings.resources", StringComparer.Ordinal)).IsTrue();
        await Assert.That(names.Contains("Configurator.Browser.Resources.BrowserStrings.ar.resources", StringComparer.Ordinal)).IsTrue();
        Dictionary<string, string> english = ReadResources("BrowserStrings");
        Dictionary<string, string> arabic = ReadResources("BrowserStrings.ar");
        await Assert.That(english.Count > 0).IsTrue();
        await Assert.That(english.Keys.Order(StringComparer.Ordinal).SequenceEqual(arabic.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)).IsTrue();
        await Assert.That(english.Values.All(value => !string.IsNullOrWhiteSpace(value))).IsTrue();
        await Assert.That(arabic.Values.All(value => !string.IsNullOrWhiteSpace(value))).IsTrue();
    }

    [Test]
    [Arguments("en")]
    [Arguments("en-GB")]
    [Arguments("fr")]
    [Arguments("")]
    public async Task OtherLocalesReadTheShippedBaseCopy(string locale)
    {
        foreach ((string key, string value) in ReadResources("BrowserStrings"))
            await Assert.That(BrowserStrings.Get(key, locale)).IsEqualTo(value);
    }

    [Test]
    [Arguments("ar")]
    [Arguments("ar-SA")]
    [Arguments("AR-eg")]
    public async Task ArabicLocalesReadTheShippedArabicCopy(string locale)
    {
        foreach ((string key, string value) in ReadResources("BrowserStrings.ar"))
            await Assert.That(BrowserStrings.Get(key, locale)).IsEqualTo(value);
    }

    [Test]
    public async Task CultureRefreshAndReleaseRequireTheCurrentOwnerLease()
    {
        var navigation = new BrowserNavigationState();
        object owner = new();
        (string Locale, string Direction) culture = ("en", "ltr");
        long oldLease = navigation.Register(owner, () => { }, () => Task.CompletedTask, () => culture, () => false);
        long currentLease = navigation.Register(owner, () => { }, () => Task.CompletedTask, () => culture, () => false);
        int changes = 0;
        navigation.Changed += (_, _) => changes++;

        culture = ("ar", "rtl");
        navigation.RefreshCulture(owner, oldLease);
        navigation.RefreshCulture(new object(), currentLease);
        navigation.Release(owner, oldLease);
        await Assert.That(changes).IsEqualTo(0);
        await Assert.That(navigation.Culture).IsEqualTo(("ar", "rtl"));
        navigation.RefreshCulture(owner, currentLease);
        await Assert.That(changes).IsEqualTo(1);
        navigation.Release(owner, currentLease);
        await Assert.That(changes).IsEqualTo(2);
        await Assert.That(navigation.IsActive).IsFalse();
        await Assert.That(navigation.Culture.Locale).IsEqualTo(CultureInfo.CurrentUICulture.Name);
        culture = ("ar-SA", "rtl");
        navigation.RefreshCulture(owner, currentLease);
        await Assert.That(changes).IsEqualTo(2);
        await Assert.That(navigation.Culture.Locale).IsEqualTo(CultureInfo.CurrentUICulture.Name);
    }

    private static Dictionary<string, string> ReadResources(string name)
    {
        using Stream stream = typeof(BrowserStrings).Assembly.GetManifestResourceStream($"Configurator.Browser.Resources.{name}.resources")
            ?? throw new InvalidOperationException("browser-resource-missing");
        using var reader = new ResourceReader(stream);
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in reader)
            resources.Add((string)entry.Key, (string)entry.Value!);
        return resources;
    }
}
