namespace Configurator.Desktop.Resources;

using System.Globalization;
using System.Resources;

/// <summary>Shipped container-action copy selected by the current UI culture, independently of layout direction.</summary>
public static class DesktopActionText
{
    private static readonly ResourceManager Resources = new(typeof(DesktopActionText));

    public static string SupplyLabel => Read(nameof(SupplyLabel));
    public static string SupplyHelp => Read(nameof(SupplyHelp));
    public static string ClearLabel => Read(nameof(ClearLabel));
    public static string ClearHelp => Read(nameof(ClearHelp));
    public static string SupplyName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(SupplyName)), title);
    public static string ClearName(string title) => string.Format(CultureInfo.CurrentCulture, Read(nameof(ClearName)), title);

    private static string Read(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture)!;
}
