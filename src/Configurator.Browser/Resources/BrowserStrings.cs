namespace Configurator.Browser.Resources;

using System.Globalization;
using System.Resources;

/// <summary>Reads both shipped UI languages from the main assembly without satellite loading.</summary>
public static class BrowserStrings
{
    private static readonly ResourceManager English = new("Configurator.Browser.Resources.BrowserStrings", typeof(BrowserStrings).Assembly);
    private static readonly ResourceManager Arabic = new("Configurator.Browser.Resources.BrowserStrings.ar", typeof(BrowserStrings).Assembly);

    public static string Get(string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(key);
        ResourceManager resources = string.Equals(locale, "ar", StringComparison.OrdinalIgnoreCase)
            || locale?.StartsWith("ar-", StringComparison.OrdinalIgnoreCase) == true ? Arabic : English;
        return resources.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new MissingManifestResourceException($"Missing browser resource key: {key}");
    }
}
