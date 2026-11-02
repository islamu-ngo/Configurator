using System.Globalization;
using System.Resources;

namespace Configurator.Cli.Resources;

internal static class CliStrings
{
    private static readonly ResourceManager Resources = new(typeof(CliStrings));

    internal static string Get(string key, CultureInfo culture) =>
        Resources.GetString(key, culture) ?? throw new InvalidOperationException("cli-resource-missing");

    internal static string State(string token, CultureInfo culture) =>
        Resources.GetString("State_" + token.Replace('-', '_'), culture) ?? token;

    internal static string Format(string key, CultureInfo culture, params object[] values) =>
        string.Format(culture, Get(key, culture), values);
}
